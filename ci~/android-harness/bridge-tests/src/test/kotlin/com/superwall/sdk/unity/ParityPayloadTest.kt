package com.superwall.sdk.unity

import com.superwall.sdk.analytics.Tier
import com.superwall.sdk.analytics.superwall.AttributionMatchInfo
import com.superwall.sdk.analytics.superwall.SuperwallEvent
import com.superwall.sdk.analytics.superwall.TransactionProduct
import com.superwall.sdk.config.options.SuperwallOptions
import com.superwall.sdk.models.customer.CustomerInfo
import com.superwall.sdk.models.entitlements.Entitlement
import com.superwall.sdk.models.paywall.PaywallPresentationInfo
import com.superwall.sdk.models.paywall.PaywallPresentationStyle
import com.superwall.sdk.models.product.Offer
import com.superwall.sdk.models.product.PlayStoreProduct
import com.superwall.sdk.models.product.ProductItem
import com.superwall.sdk.models.product.StripeProduct
import com.superwall.sdk.models.triggers.Experiment
import com.superwall.sdk.paywall.presentation.internal.PaywallPresentationRequestStatus
import com.superwall.sdk.paywall.presentation.internal.PaywallPresentationRequestStatusReason
import com.superwall.sdk.paywall.view.webview.messaging.PageViewData
import com.superwall.sdk.store.abstractions.product.SubscriptionPeriod
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.math.BigDecimal
import java.util.Date

/**
 * The payloads and options added to close the gaps with the native SDKs, under the keys
 * `BridgeCallbackHandler` reads (Tests/Runtime/ParityTests.cs has the C# half).
 */
class ParityPayloadTest {
    private val bridge = SuperwallUnityBridge()

    private fun eventPayload(event: SuperwallEvent): JSONObject {
        val method = SuperwallUnityBridge::class.java.getDeclaredMethod(
            "appendEventPayload", SuperwallEvent::class.java, JSONObject::class.java,
        )
        method.isAccessible = true
        return JSONObject().also { method.invoke(bridge, event, it) }
    }

    private val experiment = Experiment("exp", "group", Experiment.Variant("var", Experiment.Variant.VariantType.HOLDOUT, null))

    @Test
    fun playStoreProduct_carriesBasePlanAndOffer_andBothIdentifiers() {
        val json = bridge.serializeProductItem(
            ProductItem(
                compositeId = "pro:monthly:intro",
                name = "primary",
                type = ProductItem.StoreProductType.PlayStore(
                    PlayStoreProduct(productIdentifier = "pro", basePlanIdentifier = "monthly", offer = Offer.Specified(offerIdentifier = "intro")),
                ),
                entitlements = setOf(Entitlement("gold")),
            ),
        )
        // `id` is still the composite identifier; `productIdentifier` is the bare one, as on iOS.
        assertEquals("pro:monthly:intro", json.getString("id"))
        assertEquals("pro", json.getString("productIdentifier"))
        assertEquals("playStore", json.getString("store"))
        val playStore = json.getJSONObject("playStoreProduct")
        assertEquals("pro", playStore.getString("id"))
        assertEquals("monthly", playStore.getString("basePlanIdentifier"))
        assertEquals("specified", playStore.getString("offerType"))
        assertEquals("intro", playStore.getString("offerIdentifier"))
        assertEquals("pro:monthly:intro", playStore.getString("fullIdentifier"))
    }

    @Test
    fun stripeProduct_carriesEnvironmentAndTrialDays() {
        val json = bridge.serializeProductItem(
            ProductItem(
                compositeId = "price_1",
                name = "secondary",
                type = ProductItem.StoreProductType.Stripe(StripeProduct(environment = "live", productIdentifier = "price_1", trialDays = 7)),
                entitlements = emptySet(),
            ),
        )
        assertEquals("stripe", json.getString("store"))
        val stripe = json.getJSONObject("stripeProduct")
        assertEquals("price_1", stripe.getString("id"))
        assertEquals("live", stripe.getString("environment"))
        assertEquals(7, stripe.getInt("trialDays"))
        assertFalse(json.has("playStoreProduct"))
    }

    @Test
    fun presentation_carriesTheStyleAndTheSizesThatStyleHas() {
        val drawer = bridge.serializePresentation(PaywallPresentationInfo(PaywallPresentationStyle.Drawer(height = 70.0, cornerRadius = 15.0), delay = 300))
        assertEquals("drawer", drawer.getString("style"))
        assertEquals(70.0, drawer.getDouble("height"), 0.0)
        assertEquals(15.0, drawer.getDouble("cornerRadius"), 0.0)
        assertFalse(drawer.has("width"))
        assertEquals(300L, drawer.getLong("delay"))

        val popup = bridge.serializePresentation(PaywallPresentationInfo(PaywallPresentationStyle.Popup(height = 50.0, width = 80.0, cornerRadius = 8.0), delay = 0))
        assertEquals("popup", popup.getString("style"))
        assertEquals(80.0, popup.getDouble("width"), 0.0)

        val fullscreen = bridge.serializePresentation(PaywallPresentationInfo(PaywallPresentationStyle.FullscreenNoAnimation, delay = 0))
        assertEquals("fullscreenNoAnimation", fullscreen.getString("style"))
        assertFalse(fullscreen.has("height"))
    }

    @Test
    fun attributionMatch_goesOutUnderTheSdkNames() {
        val json = eventPayload(
            SuperwallEvent.AttributionMatch(
                AttributionMatchInfo(
                    provider = AttributionMatchInfo.Provider.MMP,
                    matched = true,
                    source = "meta",
                    confidence = AttributionMatchInfo.Confidence.HIGH,
                    matchScore = 97.5,
                ),
            ),
        ).getJSONObject("attributionMatchInfo")
        assertEquals("mmp", json.getString("provider"))
        assertTrue(json.getBoolean("matched"))
        assertEquals("meta", json.getString("source"))
        assertEquals("high", json.getString("confidence"))
        assertEquals(97.5, json.getDouble("matchScore"), 0.0)
        assertFalse(json.has("reason"))
    }

    @Test
    fun pageViewData_leavesUnsetValuesOut() {
        val json = bridge.serializePageViewData(
            PageViewData(
                pageNodeId = "node-2",
                flowPosition = 1,
                pageName = "Plans",
                navigationNodeId = "nav-1",
                previousPageNodeId = "node-1",
                previousFlowPosition = 0,
                navigationType = "forward",
                timeOnPreviousPageMs = 1200,
            ),
        )
        assertEquals("node-2", json.getString("pageNodeId"))
        assertEquals(1, json.getInt("flowPosition"))
        assertEquals("Plans", json.getString("pageName"))
        assertEquals("forward", json.getString("navigationType"))
        assertEquals(1200, json.getInt("timeOnPreviousPageMs"))

        val entry = bridge.serializePageViewData(PageViewData("node-1", 0, "Intro", "nav-1", null, null, "entry", null))
        assertFalse(entry.has("previousPageNodeId"))
        assertFalse(entry.has("previousFlowPosition"))
        assertFalse(entry.has("timeOnPreviousPageMs"))
    }

    @Test
    fun transactionProduct_carriesPricePeriodsAndCurrency() {
        val json = bridge.serializeTransactionProduct(
            TransactionProduct(
                id = "coins_100",
                price = TransactionProduct.Price(BigDecimal("199.99"), "€199,99", "€0,55", "€3,85", "€16,67", "€199,99"),
                trialPeriod = TransactionProduct.TrialPeriod(7, 1, 0, 0, "7-day", Date(1793275200000L)),
                period = TransactionProduct.Period("year", "yearly", SubscriptionPeriod.Unit.year, 365, 52, 12, 1),
                locale = "de_DE",
                languageCode = "de",
                currency = TransactionProduct.Currency("EUR", "€"),
            ),
        )
        assertEquals("coins_100", json.getString("id"))
        // A string, so that C# parses it the same whatever the device's decimal separator.
        assertEquals("199.99", json.getJSONObject("price").getString("raw"))
        assertEquals("€199,99", json.getJSONObject("price").getString("localized"))
        assertEquals(7, json.getJSONObject("trialPeriod").getInt("days"))
        assertEquals(1793275200000L, json.getJSONObject("trialPeriod").getLong("endAt"))
        assertEquals("year", json.getJSONObject("period").getString("unit"))
        assertEquals("yearly", json.getJSONObject("period").getString("ly"))
        assertEquals("EUR", json.getJSONObject("currency").getString("code"))
    }

    @Test
    fun presentationRequest_carriesWhyNothingWasPresented() {
        val json = eventPayload(
            SuperwallEvent.PaywallPresentationRequest(
                status = PaywallPresentationRequestStatus.NoPresentation,
                reason = PaywallPresentationRequestStatusReason.Holdout(experiment),
            ),
        )
        assertEquals("noPresentation", json.getString("status"))
        val reason = json.getJSONObject("statusReason")
        assertEquals("holdout", reason.getString("type"))
        assertEquals("exp", reason.getJSONObject("experiment").getString("id"))

        val presented = eventPayload(SuperwallEvent.PaywallPresentationRequest(PaywallPresentationRequestStatus.Presentation, null))
        assertFalse(presented.has("statusReason"))
    }

    @Test
    fun smallEventValues_areSent() {
        assertEquals(850.0, eventPayload(SuperwallEvent.ShimmerViewComplete(duration = 850.0)).getDouble("shimmerDuration"), 0.0)

        val resource = eventPayload(SuperwallEvent.PaywallResourceLoadFail(url = "https://cdn.example.com/font.woff", error = "404"))
        assertEquals("https://cdn.example.com/font.woff", resource.getString("resourceUrl"))
        assertEquals("404", resource.getString("error"))

        assertEquals(2, eventPayload(SuperwallEvent.ReviewGranted(count = 2)).getInt("reviewCount"))
        assertEquals(3, eventPayload(SuperwallEvent.ReviewDenied(count = 3)).getInt("reviewCount"))
        val requested = eventPayload(SuperwallEvent.ReviewRequested(count = 1))
        assertEquals(1, requested.getInt("reviewCount"))
        assertEquals(1, requested.getInt("reviewRequestedCount"))
    }

    @Test
    fun customerInfoDidChangeEvent_carriesBothCustomerInfos() {
        val info = { userId: String -> CustomerInfo(subscriptions = emptyList(), nonSubscriptions = emptyList(), userId = userId, entitlements = emptyList()) }
        val json = eventPayload(SuperwallEvent.CustomerInfoDidChange(from = info("before"), to = info("after")))
        assertEquals("before", json.getJSONObject("previousCustomerInfo").getString("userId"))
        assertEquals("after", json.getJSONObject("customerInfo").getString("userId"))
    }

    @Test
    fun customNetworkEnvironment_takesItsPartsFromTheDomain_orFromEachField() {
        val fromDomain = bridge.parseCustomNetworkEnvironment(JSONObject("""{"domain":"http://10.0.2.2:3000"}""")) as SuperwallOptions.NetworkEnvironment.Custom
        assertEquals("10.0.2.2", fromDomain.baseHost)
        assertEquals("http", fromDomain.scheme)
        assertEquals(3000, fromDomain.port)
        // As SuperwallKit does for a custom environment.
        assertEquals("collector.superwall.dev", fromDomain.collectorHost)

        val explicit = bridge.parseCustomNetworkEnvironment(
            JSONObject("""{"baseHost":"api.example.com","collectorHost":"collector.example.com","scheme":"https","mmpHost":"mmp.example.com"}"""),
        ) as SuperwallOptions.NetworkEnvironment.Custom
        assertEquals("api.example.com", explicit.baseHost)
        assertEquals("collector.example.com", explicit.collectorHost)
        assertNull(explicit.port)
        assertEquals("mmp.example.com", explicit.mmpHost)
        // Left unset: the SDK's own default.
        assertEquals(SuperwallOptions.NetworkEnvironment.Custom("a", "b", "https", null).subscriptionHost, explicit.subscriptionHost)

        // Nothing to connect to.
        assertNull(bridge.parseCustomNetworkEnvironment(JSONObject("{}")))
    }

    @Test
    fun deviceTiers_parseTheNamesCSharpSends() {
        assertEquals(Tier.ULTRA_LOW, bridge.parseTier("ultraLow"))
        assertEquals(Tier.LOW, bridge.parseTier("low"))
        assertEquals(Tier.MID, bridge.parseTier("mid"))
        assertEquals(Tier.HIGH, bridge.parseTier("high"))
        assertEquals(Tier.ULTRA_HIGH, bridge.parseTier("ultraHigh"))
        assertEquals(Tier.UNKNOWN, bridge.parseTier("unknown"))
        assertNull(bridge.parseTier("fast"))
    }

    @Test
    fun observedPurchases_keepOnlyTheOnesBillingCanRead() {
        assertTrue(bridge.parseObservedPurchases("[]").isEmpty())
        assertTrue(bridge.parseObservedPurchases("not json").isEmpty())
        // No originalJson: left out rather than failing the whole result.
        assertTrue(bridge.parseObservedPurchases("""[{"signature":"sig"}]""").isEmpty())
    }
}
