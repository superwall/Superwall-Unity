package com.superwall.sdk.unity

import android.app.Activity
import com.android.billingclient.api.Purchase
import com.superwall.sdk.config.options.PaywallOptions
import com.superwall.sdk.models.attribution.AttributionProvider
import com.superwall.sdk.models.triggers.Experiment
import com.superwall.sdk.paywall.presentation.internal.PaywallPresentationRequestStatusReason
import com.superwall.sdk.paywall.presentation.internal.state.PaywallSkippedReason
import com.unity3d.player.UnityPlayer
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.json.JSONObject
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertSame
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

/**
 * The logic of the bridge calls that can't be watched without a device: embedded paywalls, game controller input,
 * the observer mode calls, teardown, the back press decision and the loading colour lookup. Each is the part that
 * runs without an Android runtime; what a view, a key event or Google Play does with it is not covered here.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class BridgeLogicTest {
    private val bridge = SuperwallUnityBridge()

    @Before
    fun setUp() {
        // The bridge launches on the main dispatcher, which a JVM test doesn't have.
        Dispatchers.setMain(UnconfinedTestDispatcher())
        UnityPlayer.sentMessages.clear()
        UnityPlayer.currentActivity = null
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    private fun sentToUnity(): List<JSONObject> = UnityPlayer.sentMessages.toList().map { JSONObject(it) }

    // --- Embedded paywall ---

    @Test
    fun placementParams_areNullWhenThereAreNone_orTheyCannotBeRead() {
        assertNull(parsePlacementParams(null))
        // What C# sends an embedded paywall with no parameters.
        assertNull(parsePlacementParams(""))
        assertNull(parsePlacementParams("not json"))
        assertNull(parsePlacementParams("[1, 2]"))
    }

    @Test
    fun placementParams_becomeMapsAndLists_withoutNulls() {
        val params = parsePlacementParams("""{"level": 3, "source": "shop", "gone": null, "tags": ["a", null, "b"], "user": {"vip": true, "none": null}}""")!!
        assertEquals(3, params["level"])
        assertEquals("shop", params["source"])
        assertFalse(params.containsKey("gone"))
        assertEquals(listOf("a", "b"), params["tags"])
        assertEquals(mapOf("vip" to true), params["user"])
    }

    private class RecordingTeardown : EmbeddedPaywallLifecycle.Teardown<String> {
        val steps = mutableListOf<String>()
        override fun cancelLoad() { steps += "cancelLoad" }
        override fun removeHost() { steps += "removeHost" }
        override fun detach(view: String) { steps += "detach $view" }
        override fun beforeDestroy(view: String) { steps += "beforeDestroy $view" }
        override fun destroy(view: String) { steps += "destroy $view" }
    }

    @Test
    fun embeddedPaywall_isTornDownInOrder_once() {
        val teardown = RecordingTeardown()
        val lifecycle = EmbeddedPaywallLifecycle(teardown)
        val view = "paywall"
        lifecycle.attach(view)
        assertTrue(lifecycle.owns(view))
        assertFalse(lifecycle.isRemoved)

        lifecycle.remove()

        // The view leaves its host before the SDK is told it's going, and is destroyed last.
        assertEquals(listOf("cancelLoad", "removeHost", "detach paywall", "beforeDestroy paywall", "destroy paywall"), teardown.steps)
        assertTrue(lifecycle.isRemoved)
        assertNull(lifecycle.view)
        // A paywall it has let go of is no longer its to report on.
        assertFalse(lifecycle.owns(view))

        // The SDK may hand the same view to someone else by now: it must not be torn down twice.
        lifecycle.remove()
        assertEquals(5, teardown.steps.size)
    }

    @Test
    fun embeddedPaywall_removedWhileLoading_onlyStopsTheLoad() {
        val teardown = RecordingTeardown()
        val lifecycle = EmbeddedPaywallLifecycle(teardown)

        lifecycle.remove()

        assertEquals(listOf("cancelLoad", "removeHost"), teardown.steps)
        assertTrue(lifecycle.isRemoved)
    }

    @Test
    fun embeddedPaywall_ownsOnlyTheViewItShows() {
        val lifecycle = EmbeddedPaywallLifecycle(RecordingTeardown())
        val shown = String(charArrayOf('p'))
        val other = String(charArrayOf('p'))
        assertFalse("nothing is shown yet", lifecycle.owns(shown))
        lifecycle.attach(shown)
        assertTrue(lifecycle.owns(shown))
        // The same paywall by value, but another instance: identity is what counts.
        assertFalse(lifecycle.owns(other))
    }

    @Test
    fun embeddedPaywall_givenAViewAnotherShows_findsThatOne() {
        // The SDK hands every getPaywall for a paywall the same cached view, and takes it out of whatever shows it
        // first, so its parent can't tell that another embedded paywall shows it.
        val view = String(charArrayOf('p'))
        val first = EmbeddedPaywallLifecycle(RecordingTeardown()).apply { attach(view) }
        val second = EmbeddedPaywallLifecycle(RecordingTeardown())
        val live = listOf(first, second)

        assertSame(first, embeddedPaywallShowing(second, live) { it.owns(view) })
        // Not the one that just got it, which hasn't attached it.
        second.attach(view)
        assertSame(first, embeddedPaywallShowing(second, live) { it.owns(view) })
        // Another view equal by value isn't the same view.
        assertNull(embeddedPaywallShowing(second, live) { it.owns(String(charArrayOf('p'))) })
        // One that was removed shows nothing.
        first.remove()
        assertNull(embeddedPaywallShowing(second, live) { it.owns(view) })
    }

    @Test
    fun embeddedPaywall_thatCannotBeBuilt_isASkipOrAnError() {
        val experiment = Experiment("exp", "group", Experiment.Variant("var", Experiment.Variant.VariantType.HOLDOUT, null))

        val (holdoutMethod, holdout) = bridge.embeddedPaywallLoadFailure("view-1", PaywallSkippedReason.Holdout(experiment))
        assertEquals("embeddedPaywallOnSkip", holdoutMethod)
        assertEquals("view-1", holdout.getString("viewId"))
        assertEquals("holdout", holdout.getString("reason"))
        assertEquals("exp", holdout.getJSONObject("experiment").getString("id"))

        val (_, noMatch) = bridge.embeddedPaywallLoadFailure("view-1", PaywallSkippedReason.NoAudienceMatch())
        assertEquals("noAudienceMatch", noMatch.getString("reason"))
        assertFalse(noMatch.has("experiment"))
        assertEquals("placementNotFound", bridge.embeddedPaywallLoadFailure("v", PaywallSkippedReason.PlacementNotFound()).second.getString("reason"))
        assertEquals("userIsSubscribed", bridge.embeddedPaywallLoadFailure("v", PaywallSkippedReason.UserIsSubscribed()).second.getString("reason"))

        val (errorMethod, error) = bridge.embeddedPaywallLoadFailure("view-2", IllegalStateException("No internet"))
        assertEquals("embeddedPaywallOnError", errorMethod)
        assertEquals("view-2", error.getString("viewId"))
        assertEquals("No internet", error.getString("error"))

        // The SDK's own reasons carry their explanation in `info`, not in the message.
        val reason = PaywallPresentationRequestStatusReason.NoConfig()
        val (reasonMethod, noConfig) = bridge.embeddedPaywallLoadFailure("view-3", reason)
        assertEquals("embeddedPaywallOnError", reasonMethod)
        assertEquals(reason.info, noConfig.getString("error"))

        // An error with no message still says what it was.
        assertTrue(bridge.embeddedPaywallLoadFailure("v", IllegalStateException()).second.getString("error").contains("IllegalStateException"))
    }

    @Test
    fun embeddedPaywall_beforeConfigure_reportsAnError_andKeepsNothing() {
        bridge.showEmbeddedPaywall("view-1", "campaign", "", 0f, 0.5f, 1f, 0.5f)

        val sent = sentToUnity().single()
        assertEquals("embeddedPaywallOnError", sent.getString("method"))
        assertEquals("view-1", sent.getJSONObject("data").getString("viewId"))
        assertTrue(sent.getJSONObject("data").getString("error").isNotEmpty())
        assertEquals(0, bridge.embeddedPaywallCount)

        // Moving or removing a view the bridge doesn't have does nothing.
        bridge.setEmbeddedPaywallRect("view-1", 0f, 0f, 1f, 1f)
        bridge.removeEmbeddedPaywall("view-1")
        assertEquals(1, sentToUnity().size)
    }

    @Test
    fun embeddedPaywall_withNothingShown_canBeMovedAndRemoved() {
        // Before its paywall has loaded there is no view and no host in the activity: neither call may assume one.
        val embedded = bridge.UnityEmbeddedPaywall("view-1", Activity(), floatArrayOf(0f, 0f, 1f, 1f))
        embedded.setRect(floatArrayOf(0f, 0.5f, 1f, 0.5f))
        embedded.remove()
        embedded.remove()
        embedded.setRect(floatArrayOf(0f, 0f, 1f, 1f))
        assertTrue(sentToUnity().isEmpty())
    }

    // --- Game controller ---

    @Test
    fun gameControllerButton_isAKeyDownOrAKeyUp() {
        assertEquals(android.view.KeyEvent.ACTION_DOWN, gameControllerKeyAction(isPressed = true))
        assertEquals(android.view.KeyEvent.ACTION_UP, gameControllerKeyAction(isPressed = false))
        // The values the SDK's game controller manager compares against.
        assertEquals(0, gameControllerKeyAction(isPressed = true))
        assertEquals(1, gameControllerKeyAction(isPressed = false))
    }

    @Test
    fun gameControllerInput_beforeConfigure_isDropped() {
        // `Superwall.instance` throws before configure; input that arrives early must not.
        bridge.dispatchGameControllerKeyEvent(96, true)
        bridge.dispatchGameControllerMotionEvent(0.5f, -0.5f)
    }

    // --- Observer mode ---

    private class Observed {
        var configured = true
        val lookups = mutableListOf<String>()
        val events = mutableListOf<String>()
        var products = mutableMapOf("pro" to "details:pro")
        var gate: CompletableDeferred<Unit>? = null

        val observer = UnityPurchaseObserver<String>(
            isConfigured = { configured },
            findProduct = { id ->
                lookups += id
                gate?.await()
                products[id]
            },
            onStart = { events += "start $it" },
            onError = { product, error -> events += "error $product ${error.message}" },
        )
    }

    @Test
    fun observedPurchase_startsAndFails_withTheSdksProductDetails_fetchedOnce() = runTest {
        val observed = Observed()
        observed.observer.start("pro")
        observed.observer.error("pro", "Card declined")
        observed.observer.result { observed.events += "result" }

        assertEquals(listOf("start details:pro", "error details:pro Card declined", "result"), observed.events)
        assertEquals(listOf("pro"), observed.lookups)
    }

    @Test
    fun observedPurchase_ofAProductTheStoreDoesNotKnow_isNotObserved_andIsLookedUpAgain() = runTest {
        val observed = Observed()
        observed.observer.start("missing")
        observed.observer.error("missing", "boom")
        assertTrue(observed.events.isEmpty())
        // Not remembered as missing: the store may know it by the next purchase.
        assertEquals(listOf("missing", "missing"), observed.lookups)

        observed.products["missing"] = "details:missing"
        observed.observer.start("missing")
        assertEquals(listOf("start details:missing"), observed.events)
    }

    @Test
    fun observedResult_waitsForTheStartWhoseProductIsStillLoading() = runTest(UnconfinedTestDispatcher()) {
        val observed = Observed()
        observed.gate = CompletableDeferred()

        launch { observed.observer.start("pro") }
        launch { observed.observer.result { observed.events += "result" } }
        assertTrue("the result must not overtake its start", observed.events.isEmpty())

        observed.gate!!.complete(Unit)
        assertEquals(listOf("start details:pro", "result"), observed.events)
    }

    @Test
    fun observedPurchases_beforeConfigure_areDropped() = runTest {
        val observed = Observed()
        observed.configured = false
        observed.observer.start("pro")
        observed.observer.error("pro", "boom")
        observed.observer.result { observed.events += "result" }
        assertTrue(observed.events.isEmpty())
        assertTrue(observed.lookups.isEmpty())
    }

    @Test
    fun observedProducts_areForgottenWhenCleared() = runTest {
        val observed = Observed()
        observed.observer.start("pro")
        observed.observer.clear()
        observed.observer.start("pro")
        assertEquals(listOf("pro", "pro"), observed.lookups)
    }

    @Test
    fun observedPurchase_isRebuiltFromUnityIapsJsonAndSignature() {
        // The `json` and `signature` values of a Unity IAP Google Play receipt's payload.
        val originalJson = JSONObject()
            .put("orderId", "GPA.3345-1234-5678-90123")
            .put("packageName", "com.example.game")
            .put("productId", "pro_monthly")
            .put("purchaseTime", 1793275200000L)
            .put("purchaseState", 0)
            .put("purchaseToken", "token-abc")
            .put("quantity", 1)
            .put("autoRenewing", true)
            .put("acknowledged", false)
            .toString()
        val purchasesJson = org.json.JSONArray()
            .put(JSONObject().put("originalJson", originalJson).put("signature", "c2lnbmF0dXJl"))
            .put(JSONObject().put("originalJson", "{broken").put("signature", "x"))
            .toString()

        val purchase: Purchase = bridge.parseObservedPurchases(purchasesJson).single()

        assertEquals(originalJson, purchase.originalJson)
        assertEquals("c2lnbmF0dXJl", purchase.signature)
        assertEquals("token-abc", purchase.purchaseToken)
        assertEquals("GPA.3345-1234-5678-90123", purchase.orderId)
        assertEquals(listOf("pro_monthly"), purchase.products)
        assertEquals(1793275200000L, purchase.purchaseTime)
        assertEquals("com.example.game", purchase.packageName)
        assertEquals(1, purchase.quantity)
        assertTrue(purchase.isAutoRenewing)
        assertFalse(purchase.isAcknowledged)
    }

    @Test
    fun observerCalls_beforeConfigure_doNothing() {
        bridge.observePurchaseStart("pro")
        bridge.observePurchaseError("pro", "declined")
        bridge.observePurchaseResult(0, "", "[]")
        assertTrue(sentToUnity().isEmpty())
    }

    // --- Teardown ---

    private fun staticField(name: String) = SuperwallUnityBridge::class.java.getDeclaredField(name).apply { isAccessible = true }

    @Test
    fun teardown_forgetsWhatTheBridgeHeldForTheOldInstance() {
        // The integration attributes last sent: kept, they would be merged into the next instance's.
        staticField("latestIntegrationAttributes").set(null, mapOf(AttributionProvider.entries.first() to "adjust-1"))
        bridge.setDelegateForTest()

        bridge.resetBridgeState()

        assertNull(staticField("latestIntegrationAttributes").get(null))
        assertNull(staticField("unityDelegate").get(null))
        assertNull(staticField("unityPurchaseController").get(null))
        assertEquals(0, bridge.embeddedPaywallCount)
        // And it can be reset again, as a second teardown would.
        bridge.resetBridgeState()
    }

    @Test
    fun teardown_removesEmbeddedPaywallsOnTheMainThreadOnly() {
        // Unity calls teardown on its own thread. A view can only be taken off the screen on the main thread.
        val main = StandardTestDispatcher()
        Dispatchers.setMain(main)
        val embeddedPaywalls = SuperwallUnityBridge::class.java.getDeclaredField("embeddedPaywalls").apply { isAccessible = true }
        @Suppress("UNCHECKED_CAST")
        val map = embeddedPaywalls.get(bridge) as MutableMap<String, Any>
        map["view-1"] = bridge.UnityEmbeddedPaywall("view-1", Activity(), floatArrayOf(0f, 0f, 1f, 1f))

        bridge.resetBridgeState()
        assertNull("cleared at once, off the main thread", staticField("unityDelegate").get(null))
        assertEquals("the embedded paywall is left to the main thread", 1, bridge.embeddedPaywallCount)

        main.scheduler.advanceUntilIdle()
        assertEquals(0, bridge.embeddedPaywallCount)
    }

    private fun SuperwallUnityBridge.setDelegateForTest() {
        val delegate = Class.forName("com.superwall.sdk.unity.SuperwallUnityBridge\$SuperwallUnityDelegateImpl")
            .declaredConstructors.first().apply { isAccessible = true }.newInstance(this)
        staticField("unityDelegate").set(null, delegate)
    }

    // --- Back press ---

    @Test
    fun backPress_isUnitysOnlyWhileUnityRuns() {
        assertTrue(backPressConsumedByUnity(PaywallOptions.PresentationHost.IN_WINDOW))
        assertFalse(backPressConsumedByUnity(PaywallOptions.PresentationHost.ACTIVITY))
    }

    @Test
    fun onBackPressed_isOnlySetWhenCSharpHasAHandler() {
        assertNull(bridge.parseOptions("""{"paywalls": {}}""")!!.paywalls.onBackPressed)
        assertNull(bridge.parseOptions("""{"paywalls": {"hasOnBackPressed": false}}""")!!.paywalls.onBackPressed)
    }

    @Test
    fun onBackPressed_tellsCSharp_andConsumesThePressInWindow() {
        val options = bridge.parseOptions("""{"paywalls": {"hasOnBackPressed": true}}""")!!
        val onBackPressed = options.paywalls.onBackPressed!!

        // The paywall's info isn't always known when back is pressed.
        assertTrue(onBackPressed(null))

        val sent = sentToUnity().single()
        assertEquals("onBackPressed", sent.getString("method"))
        assertFalse(sent.getJSONObject("data").has("paywallInfo"))
    }

    @Test
    fun onBackPressed_inAnActivityPaywall_tellsCSharp_butLeavesThePressAlone() {
        val options = bridge.parseOptions("""{"paywalls": {"hasOnBackPressed": true, "androidPresentation": "Activity"}}""")!!
        assertFalse(options.paywalls.onBackPressed!!(null))
        assertEquals("onBackPressed", sentToUnity().single().getString("method"))
    }

    // --- Loading colour ---

    @Test
    fun loadingColour_isTheResourceCSharpNamed() {
        val asked = mutableListOf<String>()
        val id = resolveLoadingColor("""{"paywalls": {"loadingColorResourceName": "brand_primary"}}""") { name ->
            asked += name
            0x7f060042
        }
        assertEquals(0x7f060042, id)
        assertEquals(listOf("brand_primary"), asked)
    }

    @Test
    fun loadingColour_isLeftAlone_whenThereIsNoNameOrNoSuchResource() {
        val neverAsked: (String) -> Int = { throw AssertionError("nothing to look up") }
        assertNull(resolveLoadingColor(null, neverAsked))
        assertNull(resolveLoadingColor("not json", neverAsked))
        assertNull(resolveLoadingColor("{}", neverAsked))
        assertNull(resolveLoadingColor("""{"paywalls": {}}""", neverAsked))
        assertNull(resolveLoadingColor("""{"paywalls": {"loadingColorResourceName": null}}""", neverAsked))
        assertNull(resolveLoadingColor("""{"paywalls": {"loadingColorResourceName": ""}}""", neverAsked))
        // `Resources.getIdentifier` answers 0 for a name the app doesn't have.
        assertNull(resolveLoadingColor("""{"paywalls": {"loadingColorResourceName": "missing"}}""") { 0 })
        assertSame(null, loadingColorResourceName("""{"logging": {}}"""))
        assertEquals("brand", loadingColorResourceName("""{"paywalls": {"loadingColorResourceName": "brand"}}"""))
    }
}
