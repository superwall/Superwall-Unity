package com.superwall.sdk.unity

import android.app.Activity
import android.net.Uri
import com.unity3d.player.UnityPlayer
import com.superwall.sdk.Superwall
import com.superwall.sdk.config.options.AdConsent
import com.superwall.sdk.config.options.AdConsentStatus
import com.superwall.sdk.config.options.EventTrackingBehavior
import com.superwall.sdk.config.options.SuperwallOptions
import com.superwall.sdk.customercenter.CustomerCenterAction
import com.superwall.sdk.customercenter.CustomerCenterConfiguration
import com.superwall.sdk.customercenter.CustomerCenterDelegate
import com.superwall.sdk.customercenter.CustomerCenterPurchase
import com.superwall.sdk.customercenter.CustomerCenterRefundStatus
import com.superwall.sdk.customercenter.CustomerCenterScreenType
import com.superwall.sdk.delegate.SuperwallDelegate
import com.superwall.sdk.delegate.PurchaseResult
import com.superwall.sdk.delegate.RestorationResult
import com.superwall.sdk.delegate.subscription_controller.PurchaseController
import com.superwall.sdk.models.attribution.AttributionProvider
import kotlinx.coroutines.CompletableDeferred
import com.superwall.sdk.identity.IdentityOptions
import com.superwall.sdk.identity.identify
import com.superwall.sdk.identity.setUserAttributes
import com.superwall.sdk.logger.LogLevel
import com.superwall.sdk.logger.LogScope
import com.superwall.sdk.models.entitlements.Entitlement
import com.superwall.sdk.models.product.Store
import com.superwall.sdk.store.abstractions.product.receipt.LatestPeriodType
import com.superwall.sdk.store.abstractions.product.receipt.LatestSubscriptionState
import com.superwall.sdk.models.entitlements.SubscriptionStatus
import com.superwall.sdk.models.triggers.Experiment
import com.superwall.sdk.models.customer.CustomerInfo
import com.superwall.sdk.models.customer.NonSubscriptionTransaction
import com.superwall.sdk.models.customer.SubscriptionTransaction
import com.superwall.sdk.network.device.InterfaceStyle
import com.superwall.sdk.paywall.presentation.CustomCallback
import com.superwall.sdk.paywall.presentation.CustomCallbackResult
import com.superwall.sdk.paywall.presentation.PaywallInfo
import com.superwall.sdk.paywall.presentation.PaywallPresentationHandler
import com.superwall.sdk.paywall.presentation.register
import com.superwall.sdk.paywall.presentation.dismiss
import com.superwall.sdk.paywall.presentation.dismissForNextPaywall
import com.superwall.sdk.paywall.presentation.dismissSync
import com.superwall.sdk.game.dispatchKeyEvent
import com.superwall.sdk.game.dispatchMotionEvent
import com.superwall.sdk.web.openRestoreOnWeb
import com.superwall.sdk.analytics.Tier
import com.superwall.sdk.analytics.superwall.AttributionMatchInfo
import com.superwall.sdk.analytics.superwall.TransactionProduct
import com.superwall.sdk.models.paywall.PaywallPresentationStyle
import com.superwall.sdk.models.product.Offer
import com.superwall.sdk.models.product.ProductItem
import com.superwall.sdk.paywall.presentation.internal.PaywallPresentationRequestStatusReason
import com.superwall.sdk.paywall.view.webview.messaging.PageViewData
import com.superwall.sdk.store.abstractions.product.SubscriptionPeriod
import com.android.billingclient.api.BillingResult
import com.android.billingclient.api.ProductDetails
import com.android.billingclient.api.Purchase
import android.view.View
import android.view.ViewGroup
import android.widget.FrameLayout
import com.superwall.sdk.paywall.presentation.get_paywall.builder.PaywallBuilder
import com.superwall.sdk.paywall.view.PaywallView
import com.superwall.sdk.paywall.view.delegate.PaywallViewCallback
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import com.superwall.sdk.paywall.presentation.get_presentation_result.getPresentationResult
import com.superwall.sdk.paywall.presentation.result.PresentationResult
import com.superwall.sdk.analytics.superwall.SuperwallEvent
import com.superwall.sdk.analytics.superwall.SuperwallEventInfo
import java.net.URI
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone
import com.superwall.sdk.models.internal.RedemptionResult
import com.superwall.sdk.store.abstractions.product.StoreProduct
import com.superwall.sdk.store.abstractions.product.StoreProductType
import com.superwall.sdk.store.abstractions.transactions.StoreTransaction
import com.superwall.sdk.store.abstractions.transactions.StoreTransactionState
import com.superwall.sdk.store.abstractions.transactions.StoreTransactionType
import com.superwall.sdk.store.transactions.RestoreType
import com.superwall.sdk.store.ProductReplacement
import kotlinx.coroutines.*
import kotlin.time.Duration.Companion.seconds
import org.json.JSONArray
import org.json.JSONException
import org.json.JSONObject

/**
 * Holds each paywall custom callback until C# replies. The paywall waits on [request], and
 * C# answers through [respond] with the same callbackId.
 */
internal class UnityCustomCallbacks(private val sendToUnity: (String, JSONObject) -> Boolean) {
    private val pending = java.util.concurrent.ConcurrentHashMap<String, CompletableDeferred<CustomCallbackResult>>()

    // An activity-hosted paywall pauses Unity's player loop, so C# can't reply until it closes.
    @Volatile
    var unityPausedWhilePresenting = false

    suspend fun request(handlerId: String, callback: CustomCallback): CustomCallbackResult {
        if (unityPausedWhilePresenting) {
            android.util.Log.e(
                TAG,
                "Custom callback '${callback.name}' failed: Unity is paused while the paywall is open. " +
                    "Custom callbacks need AndroidPaywallPresentation.InWindow."
            )
            return CustomCallbackResult.failure()
        }

        val callbackId = java.util.UUID.randomUUID().toString()
        val deferred = CompletableDeferred<CustomCallbackResult>()
        pending[callbackId] = deferred

        return try {
            // The name and variables come straight from the paywall, so a bad value must fail
            // this request rather than leave it waiting for a reply C# was never asked for.
            val sent = sendToUnity("onCustomCallback", JSONObject().apply {
                put("handlerId", handlerId)
                put("callbackId", callbackId)
                put("customCallback", JSONObject().apply {
                    put("name", callback.name)
                    callback.variables?.let { put("variables", JSONObject(it)) }
                })
            })
            // No timeout, same as the native SDKs: a handler can legitimately take as long as it needs.
            if (sent) deferred.await() else CustomCallbackResult.failure()
        } catch (e: JSONException) {
            android.util.Log.e(TAG, "Could not send custom callback '${callback.name}' to Unity", e)
            CustomCallbackResult.failure()
        } finally {
            pending.remove(callbackId)
        }
    }

    fun respond(callbackId: String, resultJson: String) {
        val deferred = pending.remove(callbackId)
        if (deferred == null) {
            android.util.Log.w(TAG, "Ignoring custom callback result for finished request $callbackId")
            return
        }
        deferred.complete(parseResult(resultJson))
    }

    private fun parseResult(resultJson: String): CustomCallbackResult = try {
        val json = JSONObject(resultJson)
        @Suppress("UNCHECKED_CAST")
        val data = (json.optJSONObject("data")?.let(::fromJson)) as Map<String, Any>?
        if (json.optString("status") == "success") {
            CustomCallbackResult.success(data)
        } else {
            CustomCallbackResult.failure(data)
        }
    } catch (e: JSONException) {
        android.util.Log.e(TAG, "Invalid custom callback result JSON", e)
        CustomCallbackResult.failure()
    }

    private fun fromJson(value: Any?): Any? = when (value) {
        is JSONObject -> {
            val map = mutableMapOf<String, Any>()
            value.keys().forEach { key -> fromJson(value.get(key))?.let { map[key] = it } }
            map
        }
        is JSONArray -> (0 until value.length()).mapNotNull { fromJson(value.get(it)) }
        JSONObject.NULL -> null
        else -> value
    }

    private companion object {
        const val TAG = "SuperwallUnity"
    }
}

/**
 * The Customer Center presentation made through this bridge that is still open, if any. The SDK presents one at a
 * time and, asked for another, only logs: it never calls that one's onDismiss, so C# would keep its delegate and
 * never run its onDismiss. A second `presentCustomerCenter` can arrive before the first activity pauses Unity, from
 * a double tap. Only touched on the main thread.
 *
 * A Customer Center the app presents natively, outside this bridge, isn't seen here: one asked for through the bridge
 * meanwhile is refused by the SDK and would stay marked presented. [clear] runs whenever a dismissal finishes, when
 * nothing is on screen, and hands back such a presentation so its onDismiss can still run.
 */
internal class UnityCustomerCenterPresentations {
    private var presentedId: String? = null

    /** Whether [presentationId] can be presented, marking it presented if so. */
    fun begin(presentationId: String): Boolean {
        if (presentedId != null) return false
        presentedId = presentationId
        return true
    }

    fun end(presentationId: String) {
        if (presentedId == presentationId) presentedId = null
    }

    /** Forgets the presentation marked presented, returning it. A presentation that did open has ended by now. */
    fun clear(): String? = presentedId.also { presentedId = null }
}

@Suppress("unused")
class SuperwallUnityBridge {

    companion object {
        private var unityDelegate: SuperwallUnityDelegateImpl? = null
        private var unityPurchaseController: UnityPurchaseController? = null
        private var latestIntegrationAttributes: Map<AttributionProvider, String>? = null
        private val customCallbacks = UnityCustomCallbacks { method, data -> sendToUnity(method, data) }
        private val customerCenters = UnityCustomerCenterPresentations()
        private val scope = CoroutineScope(Dispatchers.Main + SupervisorJob())

        private const val TAG = "SuperwallUnity"

        // Returns false if the message could not be handed to Unity. Most callers ignore
        // this; the purchase controller uses it so it never awaits a request C# never got.
        private fun sendToUnity(method: String, data: JSONObject): Boolean {
            return try {
                val payload = JSONObject()
                payload.put("method", method)
                payload.put("data", data)
                UnityPlayer.UnitySendMessage("SuperwallBridge", "OnCallback", payload.toString())
                true
            } catch (e: Exception) {
                android.util.Log.w(TAG, "Failed to send '$method' to Unity", e)
                false
            } catch (e: LinkageError) {
                // UnsatisfiedLinkError when the Unity player library isn't loaded.
                android.util.Log.w(TAG, "Failed to send '$method' to Unity", e)
                false
            }
        }

        private fun sendAsyncResponse(callbackId: String, data: JSONObject) {
            try {
                data.put("callbackId", callbackId)
                sendToUnity("asyncResponse", data)
            } catch (_: Exception) {}
        }

    }

    fun configure(apiKey: String, optionsJson: String?, hasPurchaseController: Boolean, completionCallbackId: String?) {
        val activity = UnityPlayer.currentActivity ?: return
        // In-window is the Unity default: an activity-hosted paywall pauses Unity's player loop,
        // so a C# purchase controller would never receive its purchase request.
        val options =
            (if (optionsJson != null) parseOptions(optionsJson) else null)
                ?: SuperwallOptions().apply {
                    paywalls.presentationHost = com.superwall.sdk.config.options.PaywallOptions.PresentationHost.IN_WINDOW
                }

        customCallbacks.unityPausedWhilePresenting =
            options.paywalls.presentationHost == com.superwall.sdk.config.options.PaywallOptions.PresentationHost.ACTIVITY

        loadingColorResourceName(optionsJson)?.let { name ->
            // The SDK takes a colour resource, so C# names one and it's looked up in the app's resources.
            val id = activity.resources.getIdentifier(name, "color", activity.packageName)
            if (id != 0) {
                options.paywalls.loadingColor = id
            } else {
                android.util.Log.w(TAG, "No colour resource named '$name' for PaywallOptions.LoadingColorResourceName")
            }
        }

        val unityActivityProvider = object : com.superwall.sdk.misc.ActivityProvider {
            override fun getCurrentActivity(): Activity? = UnityPlayer.currentActivity
        }

        val purchaseController: PurchaseController? = if (hasPurchaseController) {
            UnityPurchaseController().also { unityPurchaseController = it }
        } else {
            unityPurchaseController = null
            null
        }

        Superwall.configure(activity.application, apiKey, purchaseController, options, unityActivityProvider) { result ->
            completionCallbackId?.let { cbId ->
                val response = JSONObject()
                result.fold(
                    onSuccess = { response.put("success", true) },
                    onFailure = { error ->
                        response.put("success", false)
                        response.put("error", error.message ?: "Unknown error")
                    }
                )
                sendAsyncResponse(cbId, response)
            }
        }
    }

    fun reset() { Superwall.instance.reset() }

    fun setDelegate(hasDelegate: Boolean) {
        if (hasDelegate) {
            unityDelegate = SuperwallUnityDelegateImpl()
            Superwall.instance.delegate = unityDelegate
        } else {
            unityDelegate = null
            Superwall.instance.delegate = null
        }
    }

    fun identify(userId: String, identityOptionsJson: String?) {
        var options: IdentityOptions? = null
        if (identityOptionsJson != null) {
            try {
                val json = JSONObject(identityOptionsJson)
                options = IdentityOptions(restorePaywallAssignments = json.optBoolean("restorePaywallAssignments", false))
            } catch (_: JSONException) {}
        }
        Superwall.instance.identify(userId, options)
    }

    fun getUserId(): String = Superwall.instance.userId
    fun getIsLoggedIn(): Boolean = Superwall.instance.isLoggedIn
    fun getIsInitialized(): Boolean = Superwall.initialized

    fun setUserAttributes(attributesJson: String) {
        try {
            // A null value removes the attribute, as it does natively; JSONObject.NULL would be stored instead.
            Superwall.instance.setUserAttributes(jsonToMap(JSONObject(attributesJson)))
        } catch (_: JSONException) {}
    }

    fun getUserAttributes(): String = JSONObject(Superwall.instance.userAttributes).toString()

    fun setIntegrationAttribute(attribute: String, value: String?) {
        val provider = parseAttributionProvider(attribute) ?: return
        mergeIntegrationAttributes(mapOf(provider to value))
    }

    fun setIntegrationAttributes(attributesJson: String) {
        try {
            val json = JSONObject(attributesJson)
            val map = mutableMapOf<AttributionProvider, String?>()
            json.keys().forEach { key ->
                val provider = parseAttributionProvider(key) ?: return@forEach
                // optString would turn a JSON null into the string "null".
                map[provider] = if (json.isNull(key)) null else json.optString(key)
            }
            mergeIntegrationAttributes(map)
        } catch (_: JSONException) {}
    }

    // The Android SDK replaces every integration attribute on each call, while iOS (and the C# API) set
    // only the ones passed, and a null value removes one. Merge into the current set to match. The SDK
    // stores a new set after a short delay, so the last set sent is kept here to merge into.
    private fun mergeIntegrationAttributes(changes: Map<AttributionProvider, String?>) {
        if (changes.isEmpty()) return
        val merged = (latestIntegrationAttributes ?: Superwall.instance.integrationAttributes
            .mapNotNull { (rawName, value) -> parseAttributionProvider(rawName)?.let { it to value } }
            .toMap()).toMutableMap()
        changes.forEach { (provider, value) ->
            if (value == null) merged.remove(provider) else merged[provider] = value
        }
        latestIntegrationAttributes = merged
        Superwall.instance.setIntegrationAttributes(merged)
    }

    private fun parseAttributionProvider(name: String): AttributionProvider? =
        AttributionProvider.entries.firstOrNull { it.rawName == name }

    fun getDeviceAttributes(callbackId: String) {
        scope.launch {
            val attrs = Superwall.instance.deviceAttributes()
            sendAsyncResponse(callbackId, JSONObject(attrs))
        }
    }

    fun getLocaleIdentifier(): String? = Superwall.instance.localeIdentifier
    fun setLocaleIdentifier(localeIdentifier: String?) { Superwall.instance.localeIdentifier = localeIdentifier }

    fun getLogLevel(): String = when (Superwall.instance.logLevel) {
        LogLevel.debug -> "debug"
        LogLevel.info -> "info"
        LogLevel.warn -> "warn"
        LogLevel.error -> "error"
        LogLevel.none -> "none"
        else -> "warn"
    }

    fun setLogLevel(logLevel: String) {
        Superwall.instance.logLevel = when (logLevel.lowercase()) {
            "debug" -> LogLevel.debug; "info" -> LogLevel.info
            "warn" -> LogLevel.warn; "error" -> LogLevel.error
            "none" -> LogLevel.none; else -> LogLevel.warn
        }
    }

    fun getEntitlements(): String {
        val e = Superwall.instance.entitlements
        return JSONObject().apply {
            put("active", serializeEntitlementSet(e.active))
            put("inactive", serializeEntitlementSet(e.inactive))
            put("all", serializeEntitlementSet(e.all))
            put("web", serializeEntitlementSet(e.web))
        }.toString()
    }

    fun getEntitlementsByProductIds(productIdsJson: String): String {
        return try {
            val arr = JSONArray(productIdsJson)
            val ids = (0 until arr.length()).map { arr.getString(it) }.toSet()
            val result = Superwall.instance.entitlements.byProductIds(ids)
            serializeEntitlementSet(result).toString()
        } catch (_: JSONException) {
            "[]"
        }
    }

    fun getCustomerInfo(callbackId: String) {
        scope.launch {
            val info = Superwall.instance.getCustomerInfo()
            sendAsyncResponse(callbackId, serializeCustomerInfo(info))
        }
    }

    private fun serializeSubscriptionStatus(status: SubscriptionStatus) = JSONObject().apply {
        when (status) {
            is SubscriptionStatus.Active -> {
                put("type", "active")
                put("entitlements", serializeEntitlementSet(status.entitlements))
            }
            is SubscriptionStatus.Inactive -> put("type", "inactive")
            is SubscriptionStatus.Unknown -> put("type", "unknown")
        }
    }

    fun getSubscriptionStatus(): String = serializeSubscriptionStatus(Superwall.instance.subscriptionStatus.value).toString()

    fun setSubscriptionStatus(statusJson: String) {
        try {
            val json = JSONObject(statusJson)
            val type = json.optString("type", "unknown")
            val status: SubscriptionStatus = when (type.lowercase()) {
                "active" -> SubscriptionStatus.Active(parseEntitlementsFromJson(json.optJSONArray("entitlements")))
                "inactive" -> SubscriptionStatus.Inactive
                else -> SubscriptionStatus.Unknown
            }
            Superwall.instance.setSubscriptionStatus(status)
        } catch (_: JSONException) {}
    }

    private fun parseEntitlementsFromJson(arr: JSONArray?): Set<Entitlement> {
        if (arr == null) return emptySet()
        val out = mutableSetOf<Entitlement>()
        for (i in 0 until arr.length()) {
            val o = arr.optJSONObject(i) ?: continue
            val id = o.optString("id", null) ?: o.optString("identifier", null) ?: continue
            out.add(Entitlement(id = id))
        }
        return out
    }

    fun getConfigurationStatus(): String = when (Superwall.instance.configurationState) {
        is com.superwall.sdk.config.models.ConfigurationStatus.Configured -> "configured"
        is com.superwall.sdk.config.models.ConfigurationStatus.Failed -> "failed"
        is com.superwall.sdk.config.models.ConfigurationStatus.Pending -> "pending"
    }
    fun getIsConfigured(): Boolean = Superwall.instance.configurationState is com.superwall.sdk.config.models.ConfigurationStatus.Configured
    fun getIsPaywallPresented(): Boolean = Superwall.instance.isPaywallPresented

    fun preloadAllPaywalls() { Superwall.instance.preloadAllPaywalls() }

    fun preloadPaywallsForPlacements(placementNamesJson: String) {
        try {
            val arr = JSONArray(placementNamesJson)
            val names = mutableSetOf<String>()
            for (i in 0 until arr.length()) names.add(arr.getString(i))
            Superwall.instance.preloadPaywalls(names)
        } catch (_: JSONException) {}
    }

    fun handleDeepLink(url: String): Boolean {
        // The static call works before configure: the SDK keeps the link until it is configured.
        return Superwall.handleDeepLink(Uri.parse(url)).getOrDefault(false)
    }

    fun togglePaywallSpinner(isHidden: Boolean) { Superwall.instance.togglePaywallSpinner(isHidden) }

    fun getLatestPaywallInfo(): String? {
        return Superwall.instance.latestPaywallInfo?.let { serializePaywallInfo(it).toString() }
    }

    fun registerPlacement(placement: String, paramsJson: String?, handlerId: String?, featureId: String?, callbackId: String?) {
        val params = paramsJson?.let {
            try {
                @Suppress("UNCHECKED_CAST")
                (jsonToMap(JSONObject(it)).filterValues { value -> value != null } as Map<String, Any>)
            } catch (_: JSONException) { null }
        }

        var handler: PaywallPresentationHandler? = null
        if (handlerId != null) {
            handler = PaywallPresentationHandler()
            handler.onPresent { info ->
                sendToUnity("onPresent", JSONObject().apply {
                    put("handlerId", handlerId)
                    put("paywallInfo", serializePaywallInfo(info))
                })
            }
            handler.onDismiss { info, result ->
                sendToUnity("onDismiss", JSONObject().apply {
                    put("handlerId", handlerId)
                    put("paywallInfo", serializePaywallInfo(info))
                    put("result", serializePaywallResult(result))
                })
            }
            handler.onError { error ->
                sendToUnity("onError", JSONObject().apply {
                    put("handlerId", handlerId)
                    put("error", error.localizedMessage ?: "Unknown error")
                })
            }
            handler.onSkip { reason ->
                val reasonStr = skippedReasonName(reason)
                sendToUnity("onSkip", JSONObject().apply {
                    put("handlerId", handlerId)
                    put("reason", reasonStr)
                    (reason as? com.superwall.sdk.paywall.presentation.internal.state.PaywallSkippedReason.Holdout)
                        ?.let { put("experiment", serializeExperiment(it.experiment)) }
                })
            }
            handler.onCustomCallback { callback -> customCallbacks.request(handlerId, callback) }
        }

        val feature: (() -> Unit)? = featureId?.let { fId ->
            { sendToUnity("onFeature", JSONObject().put("handlerId", fId)) }
        }

        if (feature != null) {
            Superwall.instance.register(placement, params, handler, feature)
        } else {
            Superwall.instance.register(placement, params, handler)
        }

        callbackId?.let { sendAsyncResponse(it, JSONObject().put("success", true)) }
    }

    internal fun serializePaywallResult(result: com.superwall.sdk.paywall.presentation.internal.state.PaywallResult) = JSONObject().apply {
        when (result) {
            is com.superwall.sdk.paywall.presentation.internal.state.PaywallResult.Purchased -> {
                put("type", "purchased")
                put("productId", result.productId)
            }
            is com.superwall.sdk.paywall.presentation.internal.state.PaywallResult.Declined -> put("type", "declined")
            is com.superwall.sdk.paywall.presentation.internal.state.PaywallResult.Restored -> put("type", "restored")
        }
    }

    internal fun skippedReasonName(reason: com.superwall.sdk.paywall.presentation.internal.state.PaywallSkippedReason) = when (reason) {
        is com.superwall.sdk.paywall.presentation.internal.state.PaywallSkippedReason.Holdout -> "holdout"
        is com.superwall.sdk.paywall.presentation.internal.state.PaywallSkippedReason.NoAudienceMatch -> "noAudienceMatch"
        is com.superwall.sdk.paywall.presentation.internal.state.PaywallSkippedReason.PlacementNotFound -> "placementNotFound"
        is com.superwall.sdk.paywall.presentation.internal.state.PaywallSkippedReason.UserIsSubscribed -> "userIsSubscribed"
        else -> "noAudienceMatch"
    }

    fun dismiss() { Superwall.instance.dismissSync() }

    fun dismissWithCompletion(callbackId: String?) {
        scope.launch {
            if (Superwall.initialized) Superwall.instance.dismiss()
            callbackId?.let { sendAsyncResponse(it, JSONObject()) }
        }
    }

    fun dismissForNextPaywall(callbackId: String?) {
        scope.launch {
            if (Superwall.initialized) Superwall.instance.dismissForNextPaywall()
            callbackId?.let { sendAsyncResponse(it, JSONObject()) }
        }
    }

    fun openRestoreOnWeb() {
        if (!Superwall.initialized) return
        // Starts an activity, so from the main thread.
        scope.launch { Superwall.instance.openRestoreOnWeb() }
    }

    fun getExternalAccountId(): String? = if (Superwall.initialized) Superwall.instance.externalAccountId else null

    // --- Observer mode ---

    // The SDK's observe calls take Google Play Billing's own objects, which C# doesn't have. The product details
    // are the SDK's (fetched once per product), and a purchase is rebuilt from its original JSON and signature.
    // Each call waits for the one before it, so a result can't overtake the start whose product is still loading.
    private val observerLock = Mutex()
    private val observedProductDetails = mutableMapOf<String, ProductDetails>()

    private suspend fun productDetailsFor(productId: String): ProductDetails? {
        observedProductDetails[productId]?.let { return it }
        val product = Superwall.instance.getProducts(productId).getOrNull()?.values?.firstOrNull()
        val details = product?.rawStoreProduct?.underlyingProductDetails
        if (details == null) {
            android.util.Log.w(TAG, "No Google Play product details for $productId; the purchase is not observed.")
        } else {
            observedProductDetails[productId] = details
        }
        return details
    }

    fun observePurchaseStart(productId: String) {
        scope.launch {
            observerLock.withLock {
                if (!Superwall.initialized) return@withLock
                productDetailsFor(productId)?.let { Superwall.instance.observePurchaseStart(it) }
            }
        }
    }

    fun observePurchaseError(productId: String, error: String) {
        scope.launch {
            observerLock.withLock {
                if (!Superwall.initialized) return@withLock
                productDetailsFor(productId)?.let { Superwall.instance.observePurchaseError(it, Throwable(error)) }
            }
        }
    }

    fun observePurchaseResult(billingResponseCode: Int, debugMessage: String, purchasesJson: String) {
        val purchases = parseObservedPurchases(purchasesJson)
        scope.launch {
            observerLock.withLock {
                if (!Superwall.initialized) return@withLock
                val result = BillingResult.newBuilder()
                    .setResponseCode(billingResponseCode)
                    .setDebugMessage(debugMessage)
                    .build()
                Superwall.instance.observePurchaseResult(result, purchases)
            }
        }
    }

    // A purchase whose JSON Google Play Billing can't read is left out.
    internal fun parseObservedPurchases(purchasesJson: String): List<Purchase> = try {
        val array = JSONArray(purchasesJson)
        (0 until array.length()).mapNotNull { index ->
            val item = array.optJSONObject(index) ?: return@mapNotNull null
            try {
                Purchase(item.getString("originalJson"), item.optString("signature"))
            } catch (e: JSONException) {
                android.util.Log.w(TAG, "Ignoring an observed purchase whose JSON is invalid", e)
                null
            }
        }
    } catch (_: JSONException) {
        emptyList()
    }

    // --- Game controller ---

    fun dispatchGameControllerKeyEvent(keyCode: Int, isPressed: Boolean) {
        if (!Superwall.initialized) return
        val action = if (isPressed) android.view.KeyEvent.ACTION_DOWN else android.view.KeyEvent.ACTION_UP
        Superwall.instance.dispatchKeyEvent(android.view.KeyEvent(action, keyCode))
    }

    fun dispatchGameControllerMotionEvent(x: Float, y: Float) {
        if (!Superwall.initialized) return
        val now = android.os.SystemClock.uptimeMillis()
        // The SDK reads AXIS_X and AXIS_Y of a move event, later, on the main thread: the event isn't recycled here.
        Superwall.instance.dispatchMotionEvent(
            android.view.MotionEvent.obtain(now, now, android.view.MotionEvent.ACTION_MOVE, x, y, 0)
        )
    }

    // --- Embedded paywall ---

    // x, y, width and height are fractions of the activity's content view, with y measured from the top.
    fun showEmbeddedPaywall(viewId: String, placement: String, paramsJson: String?, x: Float, y: Float, width: Float, height: Float) {
        val params = paramsJson?.let {
            try {
                @Suppress("UNCHECKED_CAST")
                (jsonToMap(JSONObject(it)).filterValues { value -> value != null } as Map<String, Any>)
            } catch (_: JSONException) { null }
        }
        scope.launch {
            val activity = UnityPlayer.currentActivity
            if (!Superwall.initialized || activity == null) {
                sendToUnity("embeddedPaywallOnError", JSONObject().apply {
                    put("viewId", viewId)
                    put("error", "Superwall must be configured, in a running activity, before an embedded paywall is shown.")
                })
                return@launch
            }
            val embedded = UnityEmbeddedPaywall(viewId, activity, floatArrayOf(x, y, width, height))
            embeddedPaywalls[viewId] = embedded
            embedded.load(placement, params)
        }
    }

    fun setEmbeddedPaywallRect(viewId: String, x: Float, y: Float, width: Float, height: Float) {
        scope.launch { embeddedPaywalls[viewId]?.setRect(floatArrayOf(x, y, width, height)) }
    }

    fun removeEmbeddedPaywall(viewId: String) {
        scope.launch { embeddedPaywalls.remove(viewId)?.remove() }
    }

    // Only touched on the main thread.
    private val embeddedPaywalls = mutableMapOf<String, UnityEmbeddedPaywall>()

    /**
     * One paywall shown inside the game's activity, built with the SDK's [PaywallBuilder]. Its host is laid over the
     * activity's content, sized and placed from the rectangle C# gives as fractions of that content, so it stays
     * right when the content is resized. The SDK caches and shares PaywallView instances, so the paywall is torn
     * down only when this presentation ends, and that teardown isn't cancelled part-way.
     */
    private inner class UnityEmbeddedPaywall(
        private val viewId: String,
        private val activity: Activity,
        private var rect: FloatArray,
    ) : PaywallViewCallback {
        private val host = FrameLayout(activity)
        private var paywallView: PaywallView? = null
        private var removed = false
        private var loadJob: Job? = null
        private val content: View? get() = activity.findViewById(android.R.id.content)
        private val contentLayoutListener = View.OnLayoutChangeListener { _, _, _, _, _, _, _, _, _ -> layoutHost() }

        fun load(placement: String, params: Map<String, Any>?) {
            loadJob = scope.launch {
                val result = PaywallBuilder(placement).params(params).delegate(this@UnityEmbeddedPaywall).activity(activity).build()
                if (removed) return@launch
                result.fold(
                    onSuccess = { show(it) },
                    onFailure = { error ->
                        if (error is com.superwall.sdk.paywall.presentation.internal.state.PaywallSkippedReason) {
                            sendToUnity("embeddedPaywallOnSkip", JSONObject().apply {
                                put("viewId", viewId)
                                put("reason", skippedReasonName(error))
                                (error as? com.superwall.sdk.paywall.presentation.internal.state.PaywallSkippedReason.Holdout)
                                    ?.let { put("experiment", serializeExperiment(it.experiment)) }
                            })
                        } else {
                            reportError((error as? PaywallPresentationRequestStatusReason)?.info ?: error.localizedMessage ?: error.toString())
                        }
                    },
                )
            }
        }

        fun setRect(newRect: FloatArray) {
            rect = newRect
            layoutHost()
        }

        fun remove() {
            if (removed) return
            removed = true
            loadJob?.cancel()
            content?.removeOnLayoutChangeListener(contentLayoutListener)
            (host.parent as? ViewGroup)?.removeView(host)
            val paywall = paywallView ?: return
            paywallView = null
            host.removeView(paywall)
            paywall.beforeOnDestroy(forceCleanup = true)
            paywall.encapsulatingActivity = null
            // On the bridge's scope, which outlives this view: the SDK can hand the same instance out again.
            scope.launch {
                paywall.destroyed(forceCleanup = true)
                paywall.cleanup()
            }
        }

        private fun show(paywall: PaywallView) {
            if (paywall.parent != null) {
                reportError("The paywall is already shown elsewhere. Use a different placement for each embedded paywall.")
                return
            }
            activity.addContentView(host, FrameLayout.LayoutParams(0, 0))
            content?.addOnLayoutChangeListener(contentLayoutListener)
            layoutHost()
            paywallView = paywall
            host.addView(paywall, FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT))
            paywall.onViewCreated()
            sendToUnity("embeddedPaywallOnPresent", JSONObject().apply {
                put("viewId", viewId)
                put("paywallInfo", serializePaywallInfo(paywall.info))
            })
        }

        private fun layoutHost() {
            val container = content ?: return
            val params = host.layoutParams as? FrameLayout.LayoutParams ?: return
            val frame = embeddedPaywallFrame(rect, container.width, container.height)
            if (params.leftMargin == frame[0] && params.topMargin == frame[1] && params.width == frame[2] && params.height == frame[3]) return
            params.leftMargin = frame[0]
            params.topMargin = frame[1]
            params.width = frame[2]
            params.height = frame[3]
            host.layoutParams = params
        }

        private fun reportError(message: String) {
            sendToUnity("embeddedPaywallOnError", JSONObject().put("viewId", viewId).put("error", message))
        }

        override fun onFinished(
            paywall: PaywallView,
            result: com.superwall.sdk.paywall.presentation.internal.state.PaywallResult,
            shouldDismiss: Boolean,
        ) {
            if (paywall !== paywallView) return
            val data = JSONObject().apply {
                put("viewId", viewId)
                put("paywallInfo", serializePaywallInfo(paywall.info))
                put("result", serializePaywallResult(result))
                put("shouldDismiss", shouldDismiss)
            }
            if (shouldDismiss) {
                embeddedPaywalls.remove(viewId)
                remove()
            }
            sendToUnity("embeddedPaywallOnFinished", data)
        }
    }

    // --- Teardown ---

    fun teardown() {
        Superwall.teardown()
        unityDelegate = null
        unityPurchaseController = null
        latestIntegrationAttributes = null
        observedProductDetails.clear()
    }

    fun getPresentationResult(placement: String, paramsJson: String?, callbackId: String) {
        val params = paramsJson?.let {
            try {
                @Suppress("UNCHECKED_CAST")
                (jsonToMap(JSONObject(it)).filterValues { value -> value != null } as Map<String, Any>)
            } catch (_: JSONException) { null }
        }
        scope.launch {
            // Always reply, or the C# completion never runs. iOS reports a failure as paywallNotAvailable.
            val data = Superwall.instance.getPresentationResult(placement, params).fold(
                onSuccess = { serializePresentationResult(it) },
                onFailure = { error ->
                    JSONObject().put("type", "paywallNotAvailable").put("error", error.message ?: "Unknown error")
                }
            )
            sendAsyncResponse(callbackId, data)
        }
    }

    fun confirmAllAssignments(callbackId: String) {
        scope.launch {
            val assignments = Superwall.instance.confirmAllAssignments().getOrNull() ?: emptyList()
            val arr = JSONArray()
            assignments.forEach { a ->
                arr.put(JSONObject().apply {
                    put("experimentId", a.experimentId)
                    put("variant", JSONObject().apply {
                        put("id", a.variant.id)
                        put("type", if (a.variant.type == Experiment.Variant.VariantType.TREATMENT) "treatment" else "holdout")
                        put("paywallId", a.variant.paywallId)
                    })
                })
            }
            sendAsyncResponse(callbackId, JSONObject().put("assignments", arr))
        }
    }

    fun restorePurchases(callbackId: String) {
        scope.launch {
            // restorePurchases() returns Result<RestorationResult> since superwall-android 2.7.x
            val result = Superwall.instance.restorePurchases().getOrElse { RestorationResult.Failed(it) }
            when (result) {
                is RestorationResult.Restored -> sendAsyncResponse(callbackId, JSONObject().put("type", "restored"))
                is RestorationResult.Failed -> sendAsyncResponse(callbackId, JSONObject().put("type", "failed").put("error", result.error?.message ?: ""))
            }
        }
    }

    fun getOverrideProductsByName(): String? {
        val overrides = Superwall.instance.overrideProductsByName
        if (overrides.isEmpty()) return null
        return JSONObject(overrides as Map<*, *>).toString()
    }

    fun setOverrideProductsByName(productsJson: String?) {
        if (productsJson == null) {
            Superwall.instance.overrideProductsByName = emptyMap()
            return
        }
        try {
            val json = JSONObject(productsJson)
            val map = mutableMapOf<String, String>()
            json.keys().forEach { key -> map[key] = json.optString(key, "") }
            Superwall.instance.overrideProductsByName = map
        } catch (_: JSONException) {}
    }

    fun consume(purchaseToken: String, callbackId: String) {
        scope.launch {
            val result = Superwall.instance.consume(purchaseToken)
            val data = JSONObject()
            result.fold(
                onSuccess = { token ->
                    data.put("result", "success")
                    data.put("token", token)
                },
                onFailure = { error ->
                    data.put("result", "failed")
                    data.put("error", error.message ?: "Unknown error")
                }
            )
            sendAsyncResponse(callbackId, data)
        }
    }
    fun respondToPurchaseController(callbackId: String, resultJson: String) {
        val controller = unityPurchaseController ?: return
        val result = try {
            val json = JSONObject(resultJson)
            when (json.optString("type")) {
                "purchased" -> PurchaseResult.Purchased()
                "cancelled" -> PurchaseResult.Cancelled()
                "pending" -> PurchaseResult.Pending()
                "failed" -> PurchaseResult.Failed(json.optString("error", ""))
                else -> PurchaseResult.Failed("Unknown purchase result type")
            }
        } catch (e: JSONException) {
            PurchaseResult.Failed("Invalid purchase result JSON: ${e.message}")
        }
        controller.completePurchase(callbackId, result)
    }

    fun respondToRestorePurchases(callbackId: String, resultJson: String) {
        val controller = unityPurchaseController ?: return
        val result = try {
            val json = JSONObject(resultJson)
            when (json.optString("type")) {
                "restored" -> RestorationResult.Restored()
                "failed" -> RestorationResult.Failed(Throwable(json.optString("error", "")))
                else -> RestorationResult.Failed(Throwable("Unknown restoration result type"))
            }
        } catch (e: JSONException) {
            RestorationResult.Failed(Throwable("Invalid restoration result JSON: ${e.message}"))
        }
        controller.completeRestore(callbackId, result)
    }

    fun respondToCustomCallback(callbackId: String, resultJson: String) {
        customCallbacks.respond(callbackId, resultJson)
    }

    // Called synchronously by C# before it runs a purchase/restore request. A request can sit
    // in Unity's message queue after native gave up on it; C# must not start a store
    // transaction for it then.
    fun isPurchaseControllerRequestPending(callbackId: String): Boolean {
        return unityPurchaseController?.isPending(callbackId) ?: false
    }

    fun purchase(productId: String, callbackId: String) {
        scope.launch {
            val result = Superwall.instance.purchase(productId)
            result.fold(
                onSuccess = { purchaseResult ->
                    val data = JSONObject()
                    when (purchaseResult) {
                        is PurchaseResult.Purchased -> data.put("type", "purchased")
                        is PurchaseResult.Cancelled -> data.put("type", "cancelled")
                        is PurchaseResult.Pending -> data.put("type", "pending")
                        is PurchaseResult.Failed -> {
                            data.put("type", "failed")
                            data.put("error", purchaseResult.errorMessage)
                        }
                    }
                    sendAsyncResponse(callbackId, data)
                },
                onFailure = { error ->
                    sendAsyncResponse(callbackId, JSONObject().apply {
                        put("type", "failed")
                        put("error", error.message ?: "Unknown error")
                    })
                }
            )
        }
    }

    fun getProducts(productIdsJson: String, callbackId: String) {
        scope.launch {
            try {
                val arr = JSONArray(productIdsJson)
                val ids = Array(arr.length()) { arr.getString(it) }
                val result = Superwall.instance.getProducts(*ids)
                result.fold(
                    onSuccess = { productsMap ->
                        val productsJson = JSONObject()
                        productsMap.forEach { (id, product) ->
                            productsJson.put(id, serializeStoreProduct(product))
                        }
                        sendAsyncResponse(callbackId, JSONObject().put("products", productsJson))
                    },
                    onFailure = { error ->
                        sendAsyncResponse(callbackId, JSONObject().apply {
                            put("error", error.message ?: "Unknown error")
                        })
                    }
                )
            } catch (e: JSONException) {
                sendAsyncResponse(callbackId, JSONObject().put("error", "Invalid JSON: ${e.message}"))
            }
        }
    }

    fun getAssignments(callbackId: String) {
        val assignments = Superwall.instance.getAssignments().getOrNull() ?: emptyList()
        val arr = JSONArray()
        assignments.forEach { a ->
            arr.put(JSONObject().apply {
                put("experimentId", a.experimentId)
                put("variant", JSONObject().apply {
                    put("id", a.variant.id)
                    put("type", if (a.variant.type == Experiment.Variant.VariantType.TREATMENT) "treatment" else "holdout")
                    put("paywallId", a.variant.paywallId)
                })
            })
        }
        sendAsyncResponse(callbackId, JSONObject().put("assignments", arr))
    }

    fun showAlert(paramsJson: String) {
        try {
            val json = JSONObject(paramsJson)
            val title = json.optString("title", null)
            val message = json.optString("message", null)
            val actionTitle = json.optString("actionTitle", null)
            val closeActionTitle = json.optString("closeActionTitle", "Done")
            val actionCallbackId = json.optString("onActionCallbackId", null)
            val closeCallbackId = json.optString("onCloseCallbackId", null)

            val action: (() -> Unit)? = actionCallbackId?.let {
                { sendAsyncResponse(it, JSONObject().put("action", "performed")) }
            }
            val onClose: (() -> Unit)? = closeCallbackId?.let {
                { sendAsyncResponse(it, JSONObject().put("action", "closed")) }
            }

            Superwall.instance.showAlert(
                title = title,
                message = message,
                actionTitle = actionTitle,
                closeActionTitle = closeActionTitle,
                action = action,
                onClose = onClose
            )
        } catch (_: JSONException) {}
    }

    fun refreshConfiguration() {
        Superwall.instance.refreshConfiguration()
    }

    fun setLocalResources(resourcesJson: String?) {
        if (resourcesJson == null) {
            Superwall.instance.localResources = emptyMap()
            return
        }
        try {
            val json = JSONObject(resourcesJson)
            val map = mutableMapOf<String, com.superwall.sdk.paywall.view.webview.PaywallResource>()
            json.keys().forEach { key ->
                val path = json.getString(key)
                map[key] = com.superwall.sdk.paywall.view.webview.PaywallResource.FromUri(android.net.Uri.parse(path))
            }
            Superwall.instance.localResources = map
        } catch (_: JSONException) {}
    }

    fun setPlatformWrapper(wrapper: String, version: String) {
        // configure() returns early, leaving no instance, when Unity has no activity yet.
        if (!Superwall.initialized) return
        Superwall.instance.setPlatformWrapper(wrapper, version)
    }

    fun getIntegrationAttributes(): String = JSONObject(Superwall.instance.integrationAttributes as Map<*, *>).toString()

    // The Android SDK has no call that fetches customer info again; it keeps it current itself.
    fun refreshCustomerInfo(callbackId: String) = getCustomerInfo(callbackId)

    fun getEventTrackingBehavior(): String = Superwall.instance.eventTrackingBehavior.description

    fun setEventTrackingBehavior(behavior: String) {
        Superwall.instance.eventTrackingBehavior = parseEventTrackingBehavior(behavior) ?: return
    }

    // `Unspecified` (never set) reports both as granted, as the SDK does.
    fun getAdConsent(): String {
        val consent = Superwall.instance.adConsent as? AdConsent.Custom ?: AdConsent.Custom()
        return JSONObject()
            .put("adUserData", consent.adUserData.raw)
            .put("adPersonalization", consent.adPersonalization.raw)
            .toString()
    }

    fun setAdConsent(adConsentJson: String) {
        try {
            Superwall.instance.adConsent = parseAdConsent(JSONObject(adConsentJson))
        } catch (_: JSONException) {}
    }

    fun setInterfaceStyle(style: String?) {
        // C# passes null to follow the system setting again.
        Superwall.instance.setInterfaceStyle(when (style) {
            "light" -> InterfaceStyle.LIGHT
            "dark" -> InterfaceStyle.DARK
            else -> null
        })
    }

    fun cancelAllScheduledNotifications() { Superwall.instance.cancelAllScheduledNotifications() }

    fun setPushToken(token: String) { Superwall.instance.setPushToken(token) }

    fun setNotificationsEnabled(enabled: Boolean) {
        if (enabled) Superwall.instance.optInToNotifications() else Superwall.instance.optOutOfNotifications()
    }

    fun handleNotificationOpen(dataJson: String) {
        try {
            val json = JSONObject(dataJson)
            val data = json.keys().asSequence().filter { !json.isNull(it) }.associateWith { json.getString(it) }
            Superwall.instance.handleNotificationOpen(data)
        } catch (_: JSONException) {}
    }

    fun presentCustomerCenter(configurationJson: String?, presentationId: String, hasDelegate: Boolean) {
        val configuration = configurationJson?.let {
            try { parseCustomerCenterConfiguration(JSONObject(it)) } catch (_: JSONException) { null }
        }
        val onDismiss: () -> Unit = {
            sendToUnity("customerCenterOnDismiss", JSONObject().put("presentationId", presentationId))
        }
        // The SDK presents it from the main thread; Unity calls in on its own.
        scope.launch {
            if (!Superwall.initialized) {
                onDismiss()
                return@launch
            }
            if (!customerCenters.begin(presentationId)) {
                onDismiss()
                return@launch
            }
            val delegate = if (hasDelegate) UnityCustomerCenterDelegate(presentationId) else null
            Superwall.instance.presentCustomerCenter(configuration, delegate) {
                customerCenters.end(presentationId)
                onDismiss()
            }
        }
    }

    fun dismissCustomerCenter(callbackId: String?) {
        val completion: () -> Unit = {
            customerCenters.clear()?.let { sendToUnity("customerCenterOnDismiss", JSONObject().put("presentationId", it)) }
            callbackId?.let { sendAsyncResponse(it, JSONObject()) }
        }
        scope.launch {
            if (!Superwall.initialized) {
                completion()
                return@launch
            }
            Superwall.instance.dismissCustomerCenter(completion)
        }
    }

    // C# only answers ShouldRestorePurchases on iOS: see UnityCustomerCenterDelegate.
    fun respondToCustomerCenterShouldRestore(callbackId: String, proceed: Boolean) {}

    /**
     * Forwards one presentation's Customer Center events to C#, tagged with the id C# registered its delegate under.
     * `customerCenterShouldRestorePurchases` keeps the SDK's default, which restores: the Customer Center is its own
     * activity, which pauses Unity, so C# couldn't answer until it closed and the restore would wait for ever.
     */
    private inner class UnityCustomerCenterDelegate(private val presentationId: String) : CustomerCenterDelegate {
        override fun customerCenterDidSelectAction(action: CustomerCenterAction, pathId: String, purchase: CustomerCenterPurchase?) {
            sendToUnity("customerCenterDidSelectAction", JSONObject().apply {
                put("presentationId", presentationId)
                put("action", serializeCustomerCenterAction(action))
                put("pathId", pathId)
                purchase?.let { put("purchase", serializeCustomerCenterPurchase(it)) }
            })
        }

        override fun customerCenterDidCompleteSurvey(surveyId: String, optionId: String, action: CustomerCenterAction, pathId: String) {
            sendToUnity("customerCenterDidCompleteSurvey", JSONObject().apply {
                put("presentationId", presentationId)
                put("surveyId", surveyId)
                put("optionId", optionId)
                put("action", serializeCustomerCenterAction(action))
                put("pathId", pathId)
            })
        }

        override fun customerCenterDidCompleteRefundRequest(productId: String, status: CustomerCenterRefundStatus) {
            sendToUnity("customerCenterDidCompleteRefundRequest", JSONObject().apply {
                put("presentationId", presentationId)
                put("productId", productId)
                put("status", serializeCustomerCenterRefundStatus(status))
            })
        }

        override fun customerCenterDidDismiss() {
            sendToUnity("customerCenterDidDismiss", JSONObject().put("presentationId", presentationId))
        }
    }

    // The configuration C# sends (Superwall.SerializeCustomerCenterConfiguration). A path whose type can't be built,
    // such as a URL path with no URL, is left out.
    internal fun parseCustomerCenterConfiguration(json: JSONObject): CustomerCenterConfiguration {
        val defaults = CustomerCenterConfiguration.default
        val support = json.optJSONObject("support") ?: JSONObject()
        val accent = json.optJSONObject("accent")?.let { pair ->
            val light = pair.stringOrNull("light")
            val dark = pair.stringOrNull("dark")
            if (light != null && dark != null) CustomerCenterConfiguration.Appearance.ColorPair(light, dark) else null
        }
        return CustomerCenterConfiguration(
            managementScreen = json.optJSONObject("managementScreen")?.let(::parseCustomerCenterScreen) ?: defaults.managementScreen,
            noPurchasesScreen = json.optJSONObject("noPurchasesScreen")?.let(::parseCustomerCenterScreen) ?: defaults.noPurchasesScreen,
            support = CustomerCenterConfiguration.Support(
                email = support.stringOrNull("email"),
                latestAppVersion = support.stringOrNull("latestAppVersion"),
                warnsAboutUpdates = support.optBoolean("warnsAboutUpdates", true),
                webManagementUrl = support.stringOrNull("webManagementUrl"),
            ),
            appearance = CustomerCenterConfiguration.Appearance(accent),
            showsAccountDetails = json.optBoolean("showsAccountDetails", true),
            warnsAboutDuplicateSubscriptions = json.optBoolean("warnsAboutDuplicateSubscriptions", true),
        )
    }

    private fun parseCustomerCenterScreen(json: JSONObject): CustomerCenterConfiguration.Screen {
        val paths = json.optJSONArray("paths")
        return CustomerCenterConfiguration.Screen(
            title = json.stringOrNull("title"),
            subtitle = json.stringOrNull("subtitle"),
            paths = (0 until (paths?.length() ?: 0)).mapNotNull { i -> paths?.optJSONObject(i)?.let(::parseCustomerCenterPath) },
        )
    }

    private fun parseCustomerCenterPath(json: JSONObject): CustomerCenterConfiguration.Path? {
        val type = when (json.optString("type")) {
            "restore" -> CustomerCenterConfiguration.PathType.Restore
            "manageSubscription" -> CustomerCenterConfiguration.PathType.ManageSubscription
            "refund" -> CustomerCenterConfiguration.PathType.Refund(if (json.isNull("windowMillis")) null else json.getLong("windowMillis"))
            "changePlan" -> CustomerCenterConfiguration.PathType.ChangePlan(
                json.optJSONArray("productIds")?.let { ids -> (0 until ids.length()).map { ids.getString(it) } }
            )
            "contactSupport" -> CustomerCenterConfiguration.PathType.ContactSupport
            "url" -> CustomerCenterConfiguration.PathType.Url(
                json.stringOrNull("url") ?: return null,
                if (json.optString("openMethod") == "external") CustomerCenterConfiguration.OpenMethod.EXTERNAL
                else CustomerCenterConfiguration.OpenMethod.IN_APP,
            )
            "custom" -> CustomerCenterConfiguration.PathType.Custom(json.stringOrNull("identifier") ?: return null)
            else -> return null
        }
        val survey = json.optJSONObject("survey")?.let { surveyJson ->
            val id = surveyJson.stringOrNull("id") ?: return@let null
            val options = surveyJson.optJSONArray("options")
            CustomerCenterConfiguration.FeedbackSurvey(
                id = id,
                title = surveyJson.stringOrNull("title"),
                options = (0 until (options?.length() ?: 0)).mapNotNull { i ->
                    options?.optJSONObject(i)?.let { option ->
                        option.stringOrNull("id")?.let { CustomerCenterConfiguration.FeedbackSurvey.Option(it, option.stringOrNull("title")) }
                    }
                },
            )
        }
        return CustomerCenterConfiguration.Path(
            type = type,
            title = json.stringOrNull("title"),
            survey = survey,
            id = json.stringOrNull("id") ?: type.defaultId,
        )
    }

    internal fun serializeCustomerCenterAction(action: CustomerCenterAction) = JSONObject().apply {
        when (action) {
            CustomerCenterAction.Restore -> put("type", "restore")
            CustomerCenterAction.ManageSubscription -> put("type", "manageSubscription")
            CustomerCenterAction.Refund -> put("type", "refund")
            CustomerCenterAction.ChangePlan -> put("type", "changePlan")
            CustomerCenterAction.ContactSupport -> put("type", "contactSupport")
            is CustomerCenterAction.Url -> put("type", "url").put("url", action.url)
            is CustomerCenterAction.Custom -> put("type", "custom").put("identifier", action.identifier)
        }
    }

    internal fun serializeCustomerCenterRefundStatus(status: CustomerCenterRefundStatus) = when (status) {
        CustomerCenterRefundStatus.SUCCESS -> "success"
        CustomerCenterRefundStatus.USER_CANCELLED -> "userCancelled"
        CustomerCenterRefundStatus.ERROR -> "error"
    }

    internal fun serializeCustomerCenterPurchase(purchase: CustomerCenterPurchase) = JSONObject().apply {
        purchase.productId?.let { put("productId", it) }
        put("store", serializeStore(purchase.store))
        put("entitlements", serializeEntitlementSet(purchase.entitlements))
        purchase.subscription?.let { put("subscription", serializeSubscriptionTransaction(it)) }
        purchase.nonSubscription?.let { put("nonSubscription", serializeNonSubscriptionTransaction(it)) }
    }

    // --- Serialization ---

    private fun serializePaywallInfo(info: PaywallInfo) = JSONObject().apply {
        put("identifier", info.identifier)
        put("name", info.name)
        put("url", info.url.toString())
        put("productIds", JSONArray(info.productIds))
        put("products", JSONArray().apply {
            info.products.forEach { p ->
                put(serializeProductItem(p))
            }
        })
        info.experiment?.let { put("experiment", serializeExperiment(it)) }
        info.presentedByEventWithName?.let { put("presentedByPlacementWithName", it) }
        info.presentedByEventWithId?.let { put("presentedByPlacementWithId", it) }
        info.presentedByEventAt?.let { put("presentedByPlacementAt", it) }
        put("presentedBy", info.presentedBy)
        info.presentationSourceType?.let { put("presentationSourceType", it) }
        info.responseLoadStartTime?.let { put("responseLoadStartTime", it) }
        info.responseLoadCompleteTime?.let { put("responseLoadCompleteTime", it) }
        info.responseLoadFailTime?.let { put("responseLoadFailTime", it) }
        info.responseLoadDuration?.let { put("responseLoadDuration", it) }
        info.webViewLoadStartTime?.let { put("webViewLoadStartTime", it) }
        info.webViewLoadCompleteTime?.let { put("webViewLoadCompleteTime", it) }
        info.webViewLoadFailTime?.let { put("webViewLoadFailTime", it) }
        info.webViewLoadDuration?.let { put("webViewLoadDuration", it) }
        info.productsLoadStartTime?.let { put("productsLoadStartTime", it) }
        info.productsLoadCompleteTime?.let { put("productsLoadCompleteTime", it) }
        info.productsLoadFailTime?.let { put("productsLoadFailTime", it) }
        info.productsLoadDuration?.let { put("productsLoadDuration", it) }
        info.paywalljsVersion?.let { put("paywalljsVersion", it) }
        put("isFreeTrialAvailable", info.isFreeTrialAvailable)
        put("featureGatingBehavior", when (info.featureGatingBehavior) {
            is com.superwall.sdk.models.config.FeatureGatingBehavior.Gated -> "gated"
            is com.superwall.sdk.models.config.FeatureGatingBehavior.NonGated -> "nonGated"
        })
        put("closeReason", when (info.closeReason) {
            is com.superwall.sdk.paywall.presentation.PaywallCloseReason.SystemLogic -> "systemLogic"
            is com.superwall.sdk.paywall.presentation.PaywallCloseReason.ForNextPaywall -> "forNextPaywall"
            is com.superwall.sdk.paywall.presentation.PaywallCloseReason.WebViewFailedToLoad -> "webViewFailedToLoad"
            is com.superwall.sdk.paywall.presentation.PaywallCloseReason.ManualClose -> "manualClose"
            is com.superwall.sdk.paywall.presentation.PaywallCloseReason.None -> "none"
        })
        put("state", JSONObject(info.state))
        put("localNotifications", JSONArray().apply {
            info.localNotifications.forEach { n ->
                put(JSONObject().apply {
                    put("id", n.id)
                    put("type", when (n.type) {
                        is com.superwall.sdk.models.paywall.LocalNotificationType.TrialStarted -> "trialStarted"
                        else -> "unsupported"
                    })
                    put("title", n.title)
                    n.subtitle?.let { put("subtitle", it) }
                    put("body", n.body)
                    put("delay", n.delay)
                })
            }
        })
        put("computedPropertyRequests", JSONArray().apply {
            info.computedPropertyRequests.forEach { c ->
                put(JSONObject().apply {
                    put("type", when (c.type) {
                        com.superwall.sdk.models.config.ComputedPropertyRequest.ComputedPropertyRequestType.MINUTES_SINCE -> "minutesSince"
                        com.superwall.sdk.models.config.ComputedPropertyRequest.ComputedPropertyRequestType.HOURS_SINCE -> "hoursSince"
                        com.superwall.sdk.models.config.ComputedPropertyRequest.ComputedPropertyRequestType.DAYS_SINCE -> "daysSince"
                        com.superwall.sdk.models.config.ComputedPropertyRequest.ComputedPropertyRequestType.MONTHS_SINCE -> "monthsSince"
                        com.superwall.sdk.models.config.ComputedPropertyRequest.ComputedPropertyRequestType.YEARS_SINCE -> "yearsSince"
                        com.superwall.sdk.models.config.ComputedPropertyRequest.ComputedPropertyRequestType.PLACEMENTS_IN_HOUR -> "placementsInHour"
                        com.superwall.sdk.models.config.ComputedPropertyRequest.ComputedPropertyRequestType.PLACEMENTS_IN_DAY -> "placementsInDay"
                        com.superwall.sdk.models.config.ComputedPropertyRequest.ComputedPropertyRequestType.PLACEMENTS_IN_WEEK -> "placementsInWeek"
                        com.superwall.sdk.models.config.ComputedPropertyRequest.ComputedPropertyRequestType.PLACEMENTS_IN_MONTH -> "placementsInMonth"
                        com.superwall.sdk.models.config.ComputedPropertyRequest.ComputedPropertyRequestType.PLACEMENTS_SINCE_INSTALL -> "placementsSinceInstall"
                    })
                    put("eventName", c.eventName)
                })
            }
        })
        put("surveys", JSONArray().apply {
            info.surveys.forEach { put(serializeSurvey(it)) }
        })
        put("cacheKey", info.cacheKey)
        put("buildId", info.buildId)
        info.shimmerLoadStartTime?.let { put("shimmerLoadStartTime", it) }
        info.shimmerLoadCompleteTime?.let { put("shimmerLoadCompleteTime", it) }
        put("isScrollEnabled", info.isScrollEnabled)
        info.presentationId?.let { put("presentationId", it) }
        put("presentation", serializePresentation(info.presentation))
        put("databaseId", info.databaseId)
        put("customerInfo", serializeCustomerInfo(info.customerInfo))
    }

    // `id` stays the composite identifier C# has always had here; `productIdentifier` is the bare one, as on iOS.
    internal fun serializeProductItem(p: ProductItem) = JSONObject().apply {
        put("id", p.compositeId)
        put("name", p.name)
        put("entitlements", serializeEntitlementSet(p.entitlements))
        when (val type = p.type) {
            is ProductItem.StoreProductType.PlayStore -> {
                put("store", "playStore")
                put("productIdentifier", type.product.productIdentifier)
                put("playStoreProduct", JSONObject().apply {
                    put("id", type.product.productIdentifier)
                    put("basePlanIdentifier", type.product.basePlanIdentifier)
                    when (val offer = type.product.offer) {
                        is Offer.Automatic -> put("offerType", "automatic")
                        is Offer.NoOffer -> put("offerType", "noOffer")
                        is Offer.Specified -> put("offerType", "specified").put("offerIdentifier", offer.offerIdentifier)
                    }
                    put("fullIdentifier", type.product.fullIdentifier)
                })
            }
            is ProductItem.StoreProductType.AppStore -> {
                put("store", "appStore")
                put("productIdentifier", type.product.productIdentifier)
                put("appStoreProduct", JSONObject().put("id", type.product.productIdentifier))
            }
            is ProductItem.StoreProductType.Stripe -> {
                put("store", "stripe")
                put("productIdentifier", type.product.productIdentifier)
                put("stripeProduct", JSONObject().apply {
                    put("id", type.product.productIdentifier)
                    put("environment", type.product.environment)
                    type.product.trialDays?.let { put("trialDays", it) }
                })
            }
            is ProductItem.StoreProductType.Paddle -> {
                put("store", "paddle")
                put("productIdentifier", type.product.productIdentifier)
                put("paddleProduct", JSONObject().apply {
                    put("id", type.product.productIdentifier)
                    put("environment", type.product.environment)
                    type.product.trialDays?.let { put("trialDays", it) }
                })
            }
            is ProductItem.StoreProductType.Custom -> {
                put("store", "custom")
                put("productIdentifier", type.product.productIdentifier)
                put("customProduct", JSONObject().put("id", type.product.productIdentifier))
            }
            is ProductItem.StoreProductType.Other -> {
                put("store", "other")
                put("productIdentifier", type.product.productIdentifier)
            }
        }
    }

    internal fun serializePresentation(presentation: com.superwall.sdk.models.paywall.PaywallPresentationInfo) = JSONObject().apply {
        when (val style = presentation.style) {
            is PaywallPresentationStyle.Modal -> put("style", "modal")
            is PaywallPresentationStyle.Fullscreen -> put("style", "fullscreen")
            is PaywallPresentationStyle.FullscreenNoAnimation -> put("style", "fullscreenNoAnimation")
            is PaywallPresentationStyle.Push -> put("style", "push")
            is PaywallPresentationStyle.Drawer -> {
                put("style", "drawer")
                put("height", style.height)
                put("cornerRadius", style.cornerRadius)
            }
            is PaywallPresentationStyle.Popup -> {
                put("style", "popup")
                put("height", style.height)
                put("width", style.width)
                put("cornerRadius", style.cornerRadius)
            }
            is PaywallPresentationStyle.None -> put("style", "none")
        }
        put("delay", presentation.delay)
    }

    private fun serializeSurvey(s: com.superwall.sdk.config.models.Survey) = JSONObject().apply {
        put("id", s.id)
        put("assignmentKey", s.assignmentKey)
        put("title", s.title)
        put("message", s.message)
        put("options", JSONArray().apply {
            s.options.forEach { put(serializeSurveyOption(it)) }
        })
        put("presentationCondition", when (s.presentationCondition) {
            com.superwall.sdk.config.models.SurveyShowCondition.ON_MANUAL_CLOSE -> "onManualClose"
            com.superwall.sdk.config.models.SurveyShowCondition.ON_PURCHASE -> "onPurchase"
        })
        put("presentationProbability", s.presentationProbability)
        put("includeOtherOption", s.includeOtherOption)
        put("includeCloseOption", s.includeCloseOption)
    }

    private fun serializeSurveyOption(o: com.superwall.sdk.config.models.SurveyOption) = JSONObject().apply {
        put("id", o.id)
        put("text", o.title)
    }

    private fun serializeExperiment(exp: Experiment) = JSONObject().apply {
        put("id", exp.id)
        put("groupId", exp.groupId)
        put("variant", JSONObject().apply {
            put("id", exp.variant.id)
            put("type", if (exp.variant.type == Experiment.Variant.VariantType.TREATMENT) "treatment" else "holdout")
            put("paywallId", exp.variant.paywallId)
        })
    }

    // Mirrors every field of the C# `Entitlement` model (see Runtime/Models/Entitlement.cs), which
    // `BridgeCallbackHandler.DeserializeEntitlement` already reads. Enum values are emitted under the
    // C# member names (parsed case-insensitively), so the wire shape matches the iOS bridge and a
    // consumer sees one vocabulary. Dates are epoch milliseconds. Optional fields are omitted rather
    // than sent as null so the C# nullable stays null.
    private fun serializeEntitlement(e: Entitlement) = JSONObject().apply {
        put("id", e.id)
        put("type", serializeEntitlementType(e.type))
        put("isActive", e.isActive)
        put("productIds", JSONArray(e.productIds.toList()))
        e.latestProductId?.let { put("latestProductId", it) }
        e.store?.let { put("store", serializeStore(it)) }
        e.startsAt?.let { put("startsAt", it.time) }
        e.renewedAt?.let { put("renewedAt", it.time) }
        e.expiresAt?.let { put("expiresAt", it.time) }
        e.isLifetime?.let { put("isLifetime", it) }
        e.willRenew?.let { put("willRenew", it) }
        serializeSubscriptionState(e.state)?.let { put("state", it) }
        serializeOfferType(e.offerType)?.let { put("offerType", it) }
        e.isRevoked?.let { put("isRevoked", it) }
        e.renewsAt?.let { put("renewsAt", it.time) }
        e.isInGracePeriod?.let { put("isInGracePeriod", it) }
    }

    private fun serializeEntitlementType(type: Entitlement.Type) = when (type) {
        Entitlement.Type.SERVICE_LEVEL -> "serviceLevel"
    }

    private fun serializeStore(store: Store) = when (store) {
        Store.PLAY_STORE -> "playStore"
        Store.APP_STORE -> "appStore"
        Store.STRIPE -> "stripe"
        Store.PADDLE -> "paddle"
        Store.SUPERWALL -> "superwall"
        Store.CUSTOM -> "custom"
        Store.OTHER -> "other"
    }

    private fun serializeSubscriptionState(state: LatestSubscriptionState?) = when (state) {
        LatestSubscriptionState.GRACE_PERIOD -> "inGracePeriod"
        LatestSubscriptionState.SUBSCRIBED -> "subscribed"
        LatestSubscriptionState.EXPIRED -> "expired"
        LatestSubscriptionState.BILLING_RETRY -> "inBillingRetryPeriod"
        LatestSubscriptionState.REVOKED -> "revoked"
        LatestSubscriptionState.UNKNOWN -> "unknown"
        null -> null
    }

    // `subscription` (a paid period, no offer) and `revoked` are Android's own; C# has members for them.
    private fun serializeOfferType(type: LatestPeriodType?) = when (type) {
        LatestPeriodType.TRIAL -> "trial"
        LatestPeriodType.CODE -> "code"
        LatestPeriodType.PROMOTIONAL -> "promotional"
        LatestPeriodType.WINBACK -> "winback"
        LatestPeriodType.SUBSCRIPTION -> "subscription"
        LatestPeriodType.REVOKED -> "revoked"
        null -> null
    }

    private fun serializeEntitlementSet(set: Set<Entitlement>) = JSONArray().apply {
        set.forEach { put(serializeEntitlement(it)) }
    }

    // java.time needs API 26 and this module's minSdk is 25, hence SimpleDateFormat.
    private val iso8601Formatter = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.US).apply {
        timeZone = TimeZone.getTimeZone("UTC")
    }

    private fun iso8601String(date: Date): String = iso8601Formatter.format(date)

    // Mirrors the C# `StoreTransaction` model (Runtime/Models/StoreTransaction.cs). Dates are ISO 8601
    // strings, matching the model's string fields. Optional values are omitted rather than sent as null.
    private fun serializeStoreTransaction(t: StoreTransactionType) = JSONObject().apply {
        if (t is StoreTransaction) {
            put("configRequestId", t.configRequestId)
            put("appSessionId", t.appSessionId)
        }
        t.transactionDate?.let { put("transactionDate", iso8601String(it)) }
        t.originalTransactionIdentifier?.let { put("originalTransactionIdentifier", it) }
        t.storeTransactionId?.let { put("storeTransactionId", it) }
        t.originalTransactionDate?.let { put("originalTransactionDate", iso8601String(it)) }
        t.webOrderLineItemID?.let { put("webOrderLineItemID", it) }
        t.appBundleId?.let { put("appBundleId", it) }
        t.subscriptionGroupId?.let { put("subscriptionGroupId", it) }
        t.isUpgraded?.let { put("isUpgraded", it) }
        t.expirationDate?.let { put("expirationDate", iso8601String(it)) }
        t.offerId?.let { put("offerId", it) }
        t.revocationDate?.let { put("revocationDate", iso8601String(it)) }
        put("state", when (t.state) {
            StoreTransactionState.Purchasing -> "purchasing"
            StoreTransactionState.Purchased -> "purchased"
            StoreTransactionState.Failed -> "failed"
            StoreTransactionState.Restored -> "restored"
            StoreTransactionState.Deferred -> "deferred"
        })
        t.payment?.let { payment ->
            put("payment", JSONObject().apply {
                put("productIdentifier", payment.productIdentifier)
                put("quantity", payment.quantity)
                payment.discountIdentifier?.let { put("discountIdentifier", it) }
            })
        }
        t.appAccountToken?.let { put("appAccountToken", it) }
        put("purchaseToken", t.purchaseToken)
        t.signature?.let { put("signature", it) }
    }

    // The fields of a `SuperwallEvent` are what a consumer needs to act on a purchase (the product,
    // the store transaction, the paywall). `params` carries flattened analytics values only, so
    // without this the C# `SuperwallEventInfo.Product` / `.Transaction` / `.PaywallInfo` were always
    // null. Keys match the fields `BridgeCallbackHandler.DeserializeSuperwallEventInfo` reads.
    private fun appendEventPayload(event: SuperwallEvent, json: JSONObject) {
        when (event) {
            is SuperwallEvent.DeviceAttributes -> json.put("deviceAttributes", jsonObjectOf(event.attributes))
            is SuperwallEvent.IntegrationAttributes -> json.put("integrationAttributes", jsonObjectOf(event.audienceFilterParams))
            is SuperwallEvent.DeepLink -> json.put("deepLinkUrl", event.uri.toString())
            is SuperwallEvent.TriggerFire -> {
                json.put("placementName", event.placementName)
                json.put("result", serializeTriggerResult(event.result))
            }
            is SuperwallEvent.UserAttributes -> json.put("userAttributes", jsonObjectOf(event.attributes))
            is SuperwallEvent.Restore.Fail -> json.put("message", event.reason)
            is SuperwallEvent.PaywallResponseLoadStart -> putTriggeredPlacementName(json, event.triggeredPlacementName)
            is SuperwallEvent.PaywallResponseLoadNotFound -> putTriggeredPlacementName(json, event.triggeredPlacementName)
            is SuperwallEvent.PaywallResponseLoadFail -> putTriggeredPlacementName(json, event.triggeredPlacementName)
            is SuperwallEvent.PaywallResponseLoadComplete -> {
                putTriggeredPlacementName(json, event.triggeredPlacementName)
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            }
            is SuperwallEvent.PaywallWebviewLoadStart -> json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            is SuperwallEvent.PaywallWebviewLoadFail -> {
                event.errorMessage?.let { json.put("error", it.toString()) }
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            }
            is SuperwallEvent.PaywallWebviewLoadComplete -> json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            is SuperwallEvent.PaywallWebviewLoadTimeout -> json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            is SuperwallEvent.PaywallWebviewLoadFallback -> json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            is SuperwallEvent.PaywallProductsLoadStart -> {
                putTriggeredPlacementName(json, event.triggeredPlacementName)
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            }
            is SuperwallEvent.PaywallProductsLoadFail -> {
                event.errorMessage?.let { json.put("error", it) }
                putTriggeredPlacementName(json, event.triggeredPlacementName)
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            }
            is SuperwallEvent.PaywallProductsLoadComplete -> {
                putTriggeredPlacementName(json, event.triggeredPlacementName)
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            }
            is SuperwallEvent.PaywallResourceLoadFail -> {
                json.put("error", event.error)
                json.put("resourceUrl", event.url)
            }
            is SuperwallEvent.PaywallPresentationRequest -> {
                json.put("status", when (event.status) {
                    is com.superwall.sdk.paywall.presentation.internal.PaywallPresentationRequestStatus.Presentation -> "presentation"
                    is com.superwall.sdk.paywall.presentation.internal.PaywallPresentationRequestStatus.NoPresentation -> "noPresentation"
                    is com.superwall.sdk.paywall.presentation.internal.PaywallPresentationRequestStatus.Timeout -> "timeout"
                })
                event.reason?.let { json.put("statusReason", serializeStatusReason(it)) }
            }
            is SuperwallEvent.AttributionMatch -> json.put("attributionMatchInfo", serializeAttributionMatchInfo(event.info))
            is SuperwallEvent.ShimmerViewComplete -> json.put("shimmerDuration", event.duration)
            is SuperwallEvent.ReviewGranted -> json.put("reviewCount", event.count)
            is SuperwallEvent.ReviewDenied -> json.put("reviewCount", event.count)
            is SuperwallEvent.CustomerInfoDidChange -> {
                json.put("previousCustomerInfo", serializeCustomerInfo(event.from))
                json.put("customerInfo", serializeCustomerInfo(event.to))
            }
            is SuperwallEvent.SurveyResponse -> {
                json.put("survey", serializeSurvey(event.survey))
                json.put("selectedOption", serializeSurveyOption(event.selectedOption))
                event.customResponse?.let { json.put("customResponse", it) }
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            }
            is SuperwallEvent.CustomPlacement -> {
                json.put("name", event.placementName)
                json.put("placementParams", jsonObjectOf(event.params))
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            }
            is SuperwallEvent.EnrichmentComplete -> {
                json.put("userEnrichment", jsonObjectOf(event.userEnrichment))
                json.put("deviceEnrichment", jsonObjectOf(event.deviceEnrichment))
            }
            is SuperwallEvent.ReviewRequested -> {
                json.put("reviewRequestedCount", event.count)
                json.put("reviewCount", event.count)
            }
            is SuperwallEvent.PaywallPageView -> {
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
                json.put("pageViewData", serializePageViewData(event.data))
            }
            is SuperwallEvent.PaywallOpen -> json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            is SuperwallEvent.PaywallClose -> json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            is SuperwallEvent.PaywallDecline -> json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            is SuperwallEvent.TransactionTimeout -> json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            is SuperwallEvent.TransactionStart -> {
                json.put("product", serializeStoreProduct(event.product))
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            }
            is SuperwallEvent.TransactionAbandon -> {
                json.put("product", serializeStoreProduct(event.product))
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            }
            is SuperwallEvent.TransactionComplete -> {
                event.transaction?.let { json.put("transaction", serializeStoreTransaction(it)) }
                json.put("product", serializeStoreProduct(event.product))
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            }
            is SuperwallEvent.SubscriptionStart -> {
                json.put("product", serializeStoreProduct(event.product))
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            }
            is SuperwallEvent.FreeTrialStart -> {
                json.put("product", serializeStoreProduct(event.product))
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            }
            is SuperwallEvent.TransactionFail -> {
                json.put("error", event.error.message ?: "")
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            }
            is SuperwallEvent.TransactionRestore -> {
                when (val restoreType = event.restoreType) {
                    is RestoreType.ViaPurchase -> {
                        json.put("restoreType", "viaPurchase")
                        restoreType.transaction?.let { json.put("transaction", serializeStoreTransaction(it)) }
                    }
                    is RestoreType.ViaRestore -> json.put("restoreType", "viaRestore")
                }
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            }
            is SuperwallEvent.NonRecurringProductPurchase -> {
                // `TransactionProduct` is a slimmer type than `StoreProduct`; the identifier is what it shares.
                json.put("product", JSONObject().put("productIdentifier", event.product.id))
                json.put("transactionProduct", serializeTransactionProduct(event.product))
                json.put("paywallInfo", serializePaywallInfo(event.paywallInfo))
            }
            is SuperwallEvent.PermissionRequested -> putPermission(json, event.permissionName, event.paywallIdentifier)
            is SuperwallEvent.PermissionGranted -> putPermission(json, event.permissionName, event.paywallIdentifier)
            is SuperwallEvent.PermissionDenied -> putPermission(json, event.permissionName, event.paywallIdentifier)
            is SuperwallEvent.PaywallPreloadStart -> json.put("paywallCount", event.paywallCount)
            is SuperwallEvent.PaywallPreloadComplete -> json.put("paywallCount", event.paywallCount)
            is SuperwallEvent.CustomerCenterOpen -> json.put("customerCenterScreen", when (event.screen) {
                CustomerCenterScreenType.MANAGEMENT -> "management"
                CustomerCenterScreenType.NO_PURCHASES -> "noPurchases"
            })
            is SuperwallEvent.CustomerCenterAction -> {
                json.put("customerCenterAction", serializeCustomerCenterAction(event.action))
                json.put("customerCenterPathId", event.pathId)
                event.productId?.let { json.put("customerCenterProductId", it) }
            }
            is SuperwallEvent.CustomerCenterSurveyResponse -> {
                json.put("customerCenterSurveyId", event.surveyId)
                json.put("customerCenterSurveyOptionId", event.optionId)
                json.put("customerCenterAction", serializeCustomerCenterAction(event.action))
                json.put("customerCenterPathId", event.pathId)
                event.productId?.let { json.put("customerCenterProductId", it) }
            }
            is SuperwallEvent.CustomerCenterRefundRequest -> {
                json.put("customerCenterProductId", event.productId)
                json.put("customerCenterRefundStatus", serializeCustomerCenterRefundStatus(event.status))
            }
            else -> {}
        }
    }

    // The provider and confidence go out under the SDK's raw names (`mmp`, `high`), as from the iOS bridge.
    internal fun serializeAttributionMatchInfo(info: AttributionMatchInfo) = JSONObject().apply {
        put("provider", info.provider.rawName)
        put("matched", info.matched)
        info.source?.let { put("source", it) }
        info.confidence?.let { put("confidence", it.rawName) }
        info.matchScore?.let { put("matchScore", it) }
        info.reason?.let { put("reason", it) }
    }

    internal fun serializePageViewData(data: PageViewData) = JSONObject().apply {
        put("pageNodeId", data.pageNodeId)
        put("flowPosition", data.flowPosition)
        put("pageName", data.pageName)
        put("navigationNodeId", data.navigationNodeId)
        data.previousPageNodeId?.let { put("previousPageNodeId", it) }
        data.previousFlowPosition?.let { put("previousFlowPosition", it) }
        put("navigationType", data.navigationType)
        data.timeOnPreviousPageMs?.let { put("timeOnPreviousPageMs", it) }
    }

    // The raw price is a string, like StoreProduct's: C# parses it with the invariant culture.
    internal fun serializeTransactionProduct(product: TransactionProduct) = JSONObject().apply {
        put("id", product.id)
        put("price", JSONObject().apply {
            put("raw", product.price.raw.toString())
            put("localized", product.price.localized)
            put("daily", product.price.daily)
            put("weekly", product.price.weekly)
            put("monthly", product.price.monthly)
            put("yearly", product.price.yearly)
        })
        product.trialPeriod?.let { trial ->
            put("trialPeriod", JSONObject().apply {
                put("days", trial.days)
                put("weeks", trial.weeks)
                put("months", trial.months)
                put("years", trial.years)
                put("text", trial.text)
                trial.endAt?.let { put("endAt", it.time) }
            })
        }
        product.period?.let { period ->
            put("period", JSONObject().apply {
                put("alt", period.alt)
                put("ly", period.ly)
                put("unit", period.unit.name)
                put("days", period.days)
                put("weeks", period.weeks)
                put("months", period.months)
                put("years", period.years)
            })
        }
        put("locale", product.locale)
        product.languageCode?.let { put("languageCode", it) }
        put("currency", JSONObject().apply {
            product.currency.code?.let { put("code", it) }
            product.currency.symbol?.let { put("symbol", it) }
        })
    }

    internal fun serializeStatusReason(reason: PaywallPresentationRequestStatusReason) = JSONObject().apply {
        put("type", when (reason) {
            is PaywallPresentationRequestStatusReason.DebuggerPresented -> "debuggerPresented"
            is PaywallPresentationRequestStatusReason.PaywallAlreadyPresented -> "paywallAlreadyPresented"
            is PaywallPresentationRequestStatusReason.Holdout -> "holdout"
            is PaywallPresentationRequestStatusReason.NoAudienceMatch -> "noAudienceMatch"
            is PaywallPresentationRequestStatusReason.PlacementNotFound -> "placementNotFound"
            is PaywallPresentationRequestStatusReason.NoPaywallView -> "noPaywallViewController"
            is PaywallPresentationRequestStatusReason.NoPresenter -> "noPresenter"
            is PaywallPresentationRequestStatusReason.NoConfig -> "noConfig"
            is PaywallPresentationRequestStatusReason.SubscriptionStatusTimeout -> "subscriptionStatusTimeout"
        })
        (reason as? PaywallPresentationRequestStatusReason.Holdout)?.let { put("experiment", serializeExperiment(it.experiment)) }
        put("info", reason.info)
    }

    private fun serializeSubscriptionPeriod(period: SubscriptionPeriod) = JSONObject().apply {
        put("value", period.value)
        put("unit", period.unit.name)
    }

    private fun putPermission(json: JSONObject, permissionName: String, paywallIdentifier: String) {
        json.put("permissionName", permissionName)
        json.put("paywallIdentifier", paywallIdentifier)
    }

    private fun putTriggeredPlacementName(json: JSONObject, name: String?) {
        name?.let { json.put("triggeredPlacementName", it) }
    }

    private fun serializeTriggerResult(result: com.superwall.sdk.models.triggers.TriggerResult) = JSONObject().apply {
        when (result) {
            is com.superwall.sdk.models.triggers.TriggerResult.PlacementNotFound -> put("type", "placementNotFound")
            is com.superwall.sdk.models.triggers.TriggerResult.NoAudienceMatch -> put("type", "noAudienceMatch")
            is com.superwall.sdk.models.triggers.TriggerResult.Paywall -> {
                put("type", "paywall")
                put("experiment", serializeExperiment(result.experiment))
            }
            is com.superwall.sdk.models.triggers.TriggerResult.Holdout -> {
                put("type", "holdout")
                put("experiment", serializeExperiment(result.experiment))
            }
            is com.superwall.sdk.models.triggers.TriggerResult.Error -> {
                put("type", "error")
                put("error", result.error.message ?: result.error.toString())
            }
        }
    }

    // Dates are epoch milliseconds, as for entitlements. Optional values are omitted rather than sent as null.
    internal fun serializeCustomerInfo(info: CustomerInfo) = JSONObject().apply {
        put("userId", info.userId)
        put("entitlements", JSONArray().apply { info.entitlements.forEach { put(serializeEntitlement(it)) } })
        put("activeSubscriptionProductIds", JSONArray(info.activeSubscriptionProductIds.toList()))
        put("subscriptions", JSONArray().apply { info.subscriptions.forEach { put(serializeSubscriptionTransaction(it)) } })
        put("nonSubscriptions", JSONArray().apply { info.nonSubscriptions.forEach { put(serializeNonSubscriptionTransaction(it)) } })
    }

    private fun serializeSubscriptionTransaction(t: SubscriptionTransaction) = JSONObject().apply {
        put("transactionId", t.transactionId)
        put("productId", t.productId)
        put("purchaseDate", t.purchaseDate.time)
        put("willRenew", t.willRenew)
        put("isRevoked", t.isRevoked)
        put("isInGracePeriod", t.isInGracePeriod)
        put("isInBillingRetryPeriod", t.isInBillingRetryPeriod)
        put("isActive", t.isActive)
        t.expirationDate?.let { put("expirationDate", it.time) }
        serializeOfferType(t.offerType)?.let { put("offerType", it) }
        t.subscriptionGroupId?.let { put("subscriptionGroupId", it) }
        put("store", serializeStore(t.store))
    }

    private fun serializeNonSubscriptionTransaction(t: NonSubscriptionTransaction) = JSONObject().apply {
        put("transactionId", t.transactionId)
        put("productId", t.productId)
        put("purchaseDate", t.purchaseDate.time)
        put("isConsumable", t.isConsumable)
        put("isRevoked", t.isRevoked)
        put("store", serializeStore(t.store))
    }

    // Every field of the C# `StoreProduct` model that the Android SDK has. `subscriptionGroupIdentifier`,
    // `isFamilyShareable` and `entitlements` are App Store only and stay at their C# defaults.
    internal fun serializeStoreProduct(product: StoreProductType) = JSONObject().apply {
        put("productIdentifier", product.productIdentifier)
        put("fullIdentifier", product.fullIdentifier)
        put("localizedPrice", product.localizedPrice)
        put("localizedSubscriptionPeriod", product.localizedSubscriptionPeriod)
        put("period", product.period)
        put("periodly", product.periodly)
        put("periodDays", product.periodDays)
        put("periodDaysString", product.periodDaysString)
        put("periodWeeks", product.periodWeeks)
        put("periodWeeksString", product.periodWeeksString)
        put("periodMonths", product.periodMonths)
        put("periodMonthsString", product.periodMonthsString)
        put("periodYears", product.periodYears)
        put("periodYearsString", product.periodYearsString)
        put("dailyPrice", product.dailyPrice)
        put("weeklyPrice", product.weeklyPrice)
        put("monthlyPrice", product.monthlyPrice)
        put("yearlyPrice", product.yearlyPrice)
        put("hasFreeTrial", product.hasFreeTrial)
        product.trialPeriodEndDate?.let { put("trialPeriodEndDate", iso8601String(it)) }
        put("trialPeriodEndDateString", product.trialPeriodEndDateString)
        put("trialPeriodDays", product.trialPeriodDays)
        put("trialPeriodDaysString", product.trialPeriodDaysString)
        put("trialPeriodWeeks", product.trialPeriodWeeks)
        put("trialPeriodWeeksString", product.trialPeriodWeeksString)
        put("trialPeriodMonths", product.trialPeriodMonths)
        put("trialPeriodMonthsString", product.trialPeriodMonthsString)
        put("trialPeriodYears", product.trialPeriodYears)
        put("trialPeriodYearsString", product.trialPeriodYearsString)
        put("trialPeriodText", product.trialPeriodText)
        put("trialPeriodPrice", product.trialPeriodPrice.toString())
        put("localizedTrialPeriodPrice", product.localizedTrialPeriodPrice)
        put("currencyCode", product.currencyCode)
        put("currencySymbol", product.currencySymbol)
        put("locale", product.locale)
        put("languageCode", product.languageCode)
        product.regionCode?.let { put("regionCode", it) }
        put("price", product.price.toString())
        put("productType", product.productType)
        product.storeFrontCountryCode?.let { put("storeFrontCountryCode", it) }
        // Custom products and their transaction ids exist on StoreProduct, not the StoreProductType of other products.
        (product as? StoreProduct)?.let { storeProduct ->
            put("isCustomProduct", storeProduct.isCustomProduct)
            storeProduct.customTransactionId?.let { put("customTransactionId", it) }
        }
        put("attributes", JSONObject(product.attributes))
        product.subscriptionPeriod?.let { put("subscriptionPeriod", serializeSubscriptionPeriod(it)) }
    }

    private fun serializeRedemptionResult(result: RedemptionResult): JSONObject = JSONObject().apply {
        when (result) {
            is RedemptionResult.Success -> {
                put("type", "success")
                put("code", result.code)
                put("redemptionInfo", serializeRedemptionInfo(result.redemptionInfo))
            }
            is RedemptionResult.Error -> {
                put("type", "error")
                put("code", result.code)
                put("error", result.error.message ?: "")
            }
            is RedemptionResult.Expired -> {
                put("type", "expiredCode")
                put("code", result.code)
                put("resent", result.expired.resent)
                put("obfuscatedEmail", result.expired.obfuscatedEmail ?: JSONObject.NULL)
            }
            is RedemptionResult.InvalidCode -> {
                put("type", "invalidCode")
                put("code", result.code)
            }
            is RedemptionResult.ExpiredSubscription -> {
                put("type", "expiredSubscription")
                put("code", result.code)
                put("redemptionInfo", serializeRedemptionInfo(result.redemptionInfo))
            }
        }
    }

    // Same shape as the iOS bridge: `type` tells the ownership and store identifier cases apart.
    internal fun serializeRedemptionInfo(info: com.superwall.sdk.models.internal.RedemptionInfo) = JSONObject().apply {
        put("ownership", when (val ownership = info.ownership) {
            is com.superwall.sdk.models.internal.RedemptionOwnership.AppUser ->
                JSONObject().put("type", "appUser").put("appUserId", ownership.appUserId)
            is com.superwall.sdk.models.internal.RedemptionOwnership.Device ->
                JSONObject().put("type", "device").put("deviceId", ownership.deviceId)
        })
        put("purchaserInfo", JSONObject().apply {
            put("appUserId", info.purchaserInfo.appUserId)
            info.purchaserInfo.email?.let { put("email", it) }
            put("storeIdentifiers", when (val ids = info.purchaserInfo.storeIdentifiers) {
                is com.superwall.sdk.models.internal.StoreIdentifiers.Stripe -> JSONObject().apply {
                    put("type", "stripe")
                    put("customerId", ids.stripeCustomerId)
                    put("subscriptionIds", JSONArray(ids.subscriptionIds))
                }
                is com.superwall.sdk.models.internal.StoreIdentifiers.Paddle -> JSONObject().apply {
                    put("type", "paddle")
                    put("customerId", ids.paddleCustomerId)
                    put("subscriptionIds", JSONArray(ids.paddleSubscriptionIds))
                }
                is com.superwall.sdk.models.internal.StoreIdentifiers.Unknown -> JSONObject().apply {
                    val properties = jsonObjectOf(ids.properties)
                    put("type", "unknown")
                    properties.optString("store", "").takeIf { it.isNotEmpty() }?.let { put("store", it) }
                    properties.remove("store")
                    put("additionalInfo", properties)
                }
            })
        })
        info.paywallInfo?.let { paywall ->
            put("paywallInfo", JSONObject().apply {
                put("identifier", paywall.identifier)
                put("placementName", paywall.placementName)
                put("placementParams", jsonObjectOf(paywall.placementParams))
                put("variantId", paywall.variantId)
                put("experimentId", paywall.experimentId)
                paywall.productIdentifier?.let { put("productIdentifier", it) }
            })
        }
        put("entitlements", serializeEntitlementSet(info.entitlements.toSet()))
    }

    private fun serializePresentationResult(result: PresentationResult) = JSONObject().apply {
        when (result) {
            is PresentationResult.PlacementNotFound -> put("type", "placementNotFound")
            is PresentationResult.NoAudienceMatch -> put("type", "noAudienceMatch")
            is PresentationResult.Paywall -> { put("type", "paywall"); put("experiment", serializeExperiment(result.experiment)) }
            is PresentationResult.Holdout -> { put("type", "holdout"); put("experiment", serializeExperiment(result.experiment)) }
            is PresentationResult.PaywallNotAvailable -> put("type", "paywallNotAvailable")
        }
    }

    private fun parseOptions(optionsJson: String): SuperwallOptions? = try {
        val json = JSONObject(optionsJson)
        SuperwallOptions().apply {
            paywalls.presentationHost = com.superwall.sdk.config.options.PaywallOptions.PresentationHost.IN_WINDOW
            if (json.has("localeIdentifier")) localeIdentifier = json.getString("localeIdentifier")
            @Suppress("DEPRECATION")
            if (json.has("isExternalDataCollectionEnabled")) isExternalDataCollectionEnabled = json.getBoolean("isExternalDataCollectionEnabled")
            // After the deprecated flag above, which it replaces.
            parseEventTrackingBehavior(json.optString("eventTrackingBehavior"))?.let { eventTrackingBehavior = it }
            // Only when C# set one, so an unset consent still falls back to a TCF banner's answer.
            json.optJSONObject("adConsent")?.let { adConsent = parseAdConsent(it) }
            json.optJSONObject("customerCenter")?.let { customerCenter = parseCustomerCenterConfiguration(it) }
            if (json.has("enableExperimentalDeviceVariables")) enableExperimentalDeviceVariables = json.getBoolean("enableExperimentalDeviceVariables")
            if (json.has("isGameControllerEnabled")) isGameControllerEnabled = json.getBoolean("isGameControllerEnabled")
            if (json.has("passIdentifiersToPlayStore")) passIdentifiersToPlayStore = json.getBoolean("passIdentifiersToPlayStore")
            if (json.has("shouldObservePurchases")) shouldObservePurchases = json.getBoolean("shouldObservePurchases")
            if (json.has("useMockReviews")) useMockReviews = json.getBoolean("useMockReviews")
            if (json.has("testModeBehavior")) {
                testModeBehavior = when (json.getString("testModeBehavior").lowercase()) {
                    "always" -> com.superwall.sdk.store.testmode.TestModeBehavior.ALWAYS
                    "never" -> com.superwall.sdk.store.testmode.TestModeBehavior.NEVER
                    "whenenabledforuser" -> com.superwall.sdk.store.testmode.TestModeBehavior.WHEN_ENABLED_FOR_USER
                    else -> com.superwall.sdk.store.testmode.TestModeBehavior.AUTOMATIC
                }
            }
            if (json.has("logging")) {
                val loggingJson = json.getJSONObject("logging")
                if (loggingJson.has("level")) this.logging.level = when (loggingJson.getString("level").lowercase()) {
                    "debug" -> LogLevel.debug; "info" -> LogLevel.info; "warn" -> LogLevel.warn
                    "error" -> LogLevel.error; "none" -> LogLevel.none; else -> LogLevel.warn
                }
                if (loggingJson.has("scopes")) {
                    val scopesArray = loggingJson.getJSONArray("scopes")
                    val scopeSet = java.util.EnumSet.noneOf(LogScope::class.java)
                    for (i in 0 until scopesArray.length()) {
                        val scope = parseLogScope(scopesArray.getString(i))
                        if (scope != null) scopeSet.add(scope)
                    }
                    if (scopeSet.isNotEmpty()) this.logging.scopes = scopeSet
                }
            }
            if (json.has("networkEnvironment")) {
                networkEnvironment = when (json.getString("networkEnvironment").lowercase()) {
                    "release" -> SuperwallOptions.NetworkEnvironment.Release()
                    "releasecandidate" -> SuperwallOptions.NetworkEnvironment.ReleaseCandidate()
                    "developer" -> SuperwallOptions.NetworkEnvironment.Developer()
                    "custom" -> json.optJSONObject("customNetworkEnvironment")?.let(::parseCustomNetworkEnvironment)
                        ?: SuperwallOptions.NetworkEnvironment.Release()
                    // `local` is iOS only.
                    else -> SuperwallOptions.NetworkEnvironment.Release()
                }
            }
            if (json.has("paywalls")) {
                val paywallsJson = json.getJSONObject("paywalls")
                if (paywallsJson.has("isHapticFeedbackEnabled")) paywalls.isHapticFeedbackEnabled = paywallsJson.getBoolean("isHapticFeedbackEnabled")
                if (paywallsJson.has("shouldShowPurchaseFailureAlert")) paywalls.shouldShowPurchaseFailureAlert = paywallsJson.getBoolean("shouldShowPurchaseFailureAlert")
                if (paywallsJson.has("shouldPreload")) paywalls.shouldPreload = paywallsJson.getBoolean("shouldPreload")
                if (paywallsJson.has("automaticallyDismiss")) paywalls.automaticallyDismiss = paywallsJson.getBoolean("automaticallyDismiss")
                if (paywallsJson.has("transactionBackgroundView")) {
                    paywalls.transactionBackgroundView = when (paywallsJson.getString("transactionBackgroundView").lowercase()) {
                        "spinner" -> com.superwall.sdk.config.options.PaywallOptions.TransactionBackgroundView.SPINNER
                        "none" -> null
                        else -> com.superwall.sdk.config.options.PaywallOptions.TransactionBackgroundView.SPINNER
                    }
                }
                if (paywallsJson.has("useCachedTemplates")) paywalls.useCachedTemplates = paywallsJson.getBoolean("useCachedTemplates")
                if (paywallsJson.has("optimisticLoading")) paywalls.optimisticLoading = paywallsJson.getBoolean("optimisticLoading")
                paywallsJson.optJSONObject("preloadDeviceOverrides")?.let { overrides ->
                    paywalls.preloadDeviceOverrides = overrides.keys().asSequence()
                        .mapNotNull { key -> parseTier(key)?.let { it to overrides.optBoolean(key) } }
                        .toMap()
                }
                if (paywallsJson.optBoolean("hasOnBackPressed")) {
                    val paywallOptions = paywalls
                    paywalls.onBackPressed = { info ->
                        sendToUnity("onBackPressed", JSONObject().apply {
                            info?.let { put("paywallInfo", serializePaywallInfo(it)) }
                        })
                        // C# decides what the press does, and can only do so while Unity runs: an in-window paywall.
                        // An activity-hosted one pauses Unity, so there the press keeps its default behaviour.
                        paywallOptions.presentationHost == com.superwall.sdk.config.options.PaywallOptions.PresentationHost.IN_WINDOW
                    }
                }
                if (paywallsJson.has("timeoutAfter")) paywalls.timeoutAfter = paywallsJson.getDouble("timeoutAfter").seconds
                if (paywallsJson.has("androidPresentation")) {
                    paywalls.presentationHost = when (paywallsJson.getString("androidPresentation").lowercase()) {
                        "activity" -> com.superwall.sdk.config.options.PaywallOptions.PresentationHost.ACTIVITY
                        else -> com.superwall.sdk.config.options.PaywallOptions.PresentationHost.IN_WINDOW
                    }
                }
                paywallsJson.optJSONObject("overrideProductsByName")?.let { overrides ->
                    paywalls.overrideProductsByName = overrides.keys().asSequence()
                        .filter { !overrides.isNull(it) }
                        .associateWith { overrides.getString(it) }
                }
                if (paywallsJson.has("restoreFailed")) {
                    val restoreJson = paywallsJson.getJSONObject("restoreFailed")
                    if (restoreJson.has("title")) paywalls.restoreFailed.title = restoreJson.getString("title")
                    if (restoreJson.has("message")) paywalls.restoreFailed.message = restoreJson.getString("message")
                    if (restoreJson.has("closeButtonTitle")) paywalls.restoreFailed.closeButtonTitle = restoreJson.getString("closeButtonTitle")
                }
            }
        }
    } catch (_: JSONException) { null }

    // C# sends the tier names in camelCase: ultraLow, low, mid, high, ultraHigh, unknown.
    internal fun parseTier(name: String): Tier? = when (name.lowercase()) {
        "ultralow" -> Tier.ULTRA_LOW
        "low" -> Tier.LOW
        "mid" -> Tier.MID
        "high" -> Tier.HIGH
        "ultrahigh" -> Tier.ULTRA_HIGH
        "unknown" -> Tier.UNKNOWN
        else -> null
    }

    // `domain` is what iOS takes: a host or a URL. It supplies the base host, scheme and port left unset, and the
    // collector defaults to the one SuperwallKit uses for a custom environment.
    internal fun parseCustomNetworkEnvironment(json: JSONObject): SuperwallOptions.NetworkEnvironment? {
        val domain = json.stringOrNull("domain")
        val url = domain?.takeIf { it.contains("://") }?.let { try { URI(it) } catch (_: Exception) { null } }
        val baseHost = json.stringOrNull("baseHost") ?: url?.host ?: domain ?: return null
        val scheme = json.stringOrNull("scheme") ?: url?.scheme ?: "https"
        val port = if (json.isNull("port")) url?.port?.takeIf { it != -1 } else json.optInt("port")
        val collectorHost = json.stringOrNull("collectorHost") ?: "collector.superwall.dev"
        val defaults = SuperwallOptions.NetworkEnvironment.Custom(baseHost, collectorHost, scheme, port)
        return SuperwallOptions.NetworkEnvironment.Custom(
            baseHost = baseHost,
            collectorHost = collectorHost,
            scheme = scheme,
            port = port,
            subscriptionHost = json.stringOrNull("subscriptionHost") ?: defaults.subscriptionHost,
            enrichmentHost = json.stringOrNull("enrichmentHost") ?: defaults.enrichmentHost,
            mmpHost = json.stringOrNull("mmpHost") ?: defaults.mmpHost,
        )
    }

    private fun loadingColorResourceName(optionsJson: String?): String? = try {
        optionsJson?.let { JSONObject(it).optJSONObject("paywalls")?.stringOrNull("loadingColorResourceName") }
    } catch (_: JSONException) {
        null
    }

    internal fun parseEventTrackingBehavior(name: String?): EventTrackingBehavior? =
        EventTrackingBehavior.entries.firstOrNull { it.description == name }

    internal fun parseAdConsent(json: JSONObject): AdConsent.Custom {
        fun status(key: String) = if (json.optString(key) == "denied") AdConsentStatus.Denied else AdConsentStatus.Granted
        return AdConsent.Custom(adUserData = status("adUserData"), adPersonalization = status("adPersonalization"))
    }

    // Every `LogScope` C# has an Android counterpart for. The view controller scopes have Android names
    // of their own, and `StoreKitManager` turns on `billing`, which replaced Android's `storeKitManager`.
    internal fun parseLogScope(name: String): LogScope? = when (name.lowercase()) {
        "all" -> LogScope.all
        "localizationmanager" -> LogScope.localizationManager
        "bouncebutton" -> LogScope.bounceButton
        "coredata" -> LogScope.coreData
        "configmanager" -> LogScope.configManager
        "identitymanager" -> LogScope.identityManager
        "debugmanager" -> LogScope.debugManager
        "debugviewcontroller" -> LogScope.debugView
        "localizationviewcontroller" -> LogScope.localizationView
        "gamecontrollermanager" -> LogScope.gameControllerManager
        "device" -> LogScope.device
        "network" -> LogScope.network
        "paywallevents" -> LogScope.paywallEvents
        "productsmanager" -> LogScope.productsManager
        "storekitmanager", "billing" -> LogScope.billing
        "placements" -> LogScope.placements
        "receipts" -> LogScope.receipts
        "superwallcore" -> LogScope.superwallCore
        "paywallpresentation" -> LogScope.paywallPresentation
        "transactions" -> LogScope.transactions
        "paywallviewcontroller" -> LogScope.paywallView
        "cache" -> LogScope.cache
        "webentitlements" -> LogScope.webEntitlements
        "customercenter" -> LogScope.customerCenter
        "customerinfo" -> LogScope.customerInfo
        "jsevaluator" -> LogScope.jsEvaluator
        "paywalltransactions" -> LogScope.paywallTransactions
        "nativepurchasecontroller" -> LogScope.nativePurchaseController
        "deeplinks" -> LogScope.deepLinks
        else -> null
    }

    // --- Delegate ---

    private inner class SuperwallUnityDelegateImpl : SuperwallDelegate {
        override fun subscriptionStatusDidChange(from: SubscriptionStatus, to: SubscriptionStatus) {
            sendToUnity("subscriptionStatusDidChange", JSONObject().apply {
                put("from", serializeSubscriptionStatus(from))
                put("to", serializeSubscriptionStatus(to))
            })
        }

        override fun handleSuperwallEvent(withInfo: SuperwallEventInfo) {
            sendToUnity("handleSuperwallEvent", JSONObject().apply {
                put("eventType", withInfo.event.rawName)
                put("params", JSONObject(withInfo.params))
                appendEventPayload(withInfo.event, this)
            })
        }

        override fun handleCustomPaywallAction(withName: String) {
            sendToUnity("handleCustomPaywallAction", JSONObject().put("name", withName))
        }

        override fun willDismissPaywall(withInfo: PaywallInfo) {
            sendToUnity("willDismissPaywall", serializePaywallInfo(withInfo))
        }

        override fun willPresentPaywall(withInfo: PaywallInfo) {
            sendToUnity("willPresentPaywall", serializePaywallInfo(withInfo))
        }

        override fun didDismissPaywall(withInfo: PaywallInfo) {
            sendToUnity("didDismissPaywall", serializePaywallInfo(withInfo))
        }

        override fun didPresentPaywall(withInfo: PaywallInfo) {
            sendToUnity("didPresentPaywall", serializePaywallInfo(withInfo))
        }

        override fun paywallWillOpenURL(url: URI) {
            sendToUnity("paywallWillOpenURL", JSONObject().put("url", url.toString()))
        }

        override fun paywallWillOpenDeepLink(url: Uri) {
            sendToUnity("paywallWillOpenDeepLink", JSONObject().put("url", url.toString()))
        }

        override fun handleLog(level: String, scope: String, message: String?, info: Map<String, Any>?, error: Throwable?) {
            sendToUnity("handleLog", JSONObject().apply {
                put("level", level); put("scope", scope)
                put("message", message); put("error", error?.message)
                info?.let { put("info", jsonObjectOf(it)) }
            })
        }

        override fun willRedeemLink() {
            sendToUnity("willRedeemLink", JSONObject())
        }

        override fun didRedeemLink(result: RedemptionResult) {
            sendToUnity("didRedeemLink", serializeRedemptionResult(result))
        }

        override fun userAttributesDidChange(newAttributes: Map<String, Any>) {
            sendToUnity("userAttributesDidChange", JSONObject().apply {
                put("newAttributes", JSONObject(newAttributes))
            })
        }

        override fun customerInfoDidChange(from: CustomerInfo, to: CustomerInfo) {
            sendToUnity("customerInfoDidChange", JSONObject().apply {
                put("from", serializeCustomerInfo(from))
                put("to", serializeCustomerInfo(to))
            })
        }
    }

    private class UnityPurchaseController : PurchaseController {
        private val pendingPurchases = java.util.concurrent.ConcurrentHashMap<String, CompletableDeferred<PurchaseResult>>()
        private val pendingRestores = java.util.concurrent.ConcurrentHashMap<String, CompletableDeferred<RestorationResult>>()
        // Bounded, insertion-ordered record of callbackIds whose awaiting coroutine was cancelled.
        private val abandonedCallbackIds = object : LinkedHashMap<String, Unit>() {
            override fun removeEldestEntry(eldest: MutableMap.MutableEntry<String, Unit>?): Boolean = size > 32
        }

        // The SDK calls this overload for every purchase (2.9.0+); the older ones are never
        // reached once it is overridden. `replacement` is non-null only when the paywall's
        // Purchase action replaces an existing subscription the user has.
        override suspend fun purchase(
            activity: Activity,
            product: StoreProduct,
            basePlanId: String?,
            offerId: String?,
            replacement: ProductReplacement?
        ): PurchaseResult {
            val callbackId = java.util.UUID.randomUUID().toString()
            // Custom products (store "Custom" on the dashboard) have no ProductDetails: C# buys
            // them through its own payment system with PurchaseCustomProduct.
            val (method, request) = if (product.isCustomProduct) {
                "purchaseCustomProduct" to customProductPurchaseRequest(
                    callbackId, product.fullIdentifier, product.customTransactionId, replacement
                )
            } else {
                val productDetails = product.rawStoreProduct?.underlyingProductDetails
                    ?: return PurchaseResult.Failed(
                        "No Google Play product details for ${product.fullIdentifier}."
                    )
                "purchaseFromGooglePlay" to googlePlayPurchaseRequest(
                    callbackId, productDetails.productId, basePlanId, offerId, replacement
                )
            }

            val deferred = CompletableDeferred<PurchaseResult>()
            pendingPurchases[callbackId] = deferred

            val sent = sendToUnity(method, request)
            if (!sent) {
                // C# never got the request, so no reply will ever come.
                pendingPurchases.remove(callbackId)
                return PurchaseResult.Failed("Could not deliver purchase request to Unity")
            }

            // No timeout on purpose: a store transaction can legitimately take minutes.
            return try {
                deferred.await()
            } catch (t: Throwable) {
                if (pendingPurchases.remove(callbackId) != null) {
                    abandon(callbackId, "purchaseCancelled")
                }
                PurchaseResult.Failed(t.message ?: "Purchase cancelled")
            }
        }

        override suspend fun restorePurchases(): RestorationResult {
            val callbackId = java.util.UUID.randomUUID().toString()
            val deferred = CompletableDeferred<RestorationResult>()
            pendingRestores[callbackId] = deferred

            val sent = sendToUnity("restorePurchases", JSONObject().apply {
                put("callbackId", callbackId)
            })
            if (!sent) {
                pendingRestores.remove(callbackId)
                return RestorationResult.Failed(Throwable("Could not deliver restore request to Unity"))
            }

            return try {
                deferred.await()
            } catch (t: Throwable) {
                if (pendingRestores.remove(callbackId) != null) {
                    abandon(callbackId, "restoreCancelled")
                }
                RestorationResult.Failed(t)
            }
        }

        fun completePurchase(callbackId: String, result: PurchaseResult) {
            val deferred = pendingPurchases.remove(callbackId)
            if (deferred == null) {
                logUnmatchedReply("purchase", callbackId)
                return
            }
            deferred.complete(result)
        }

        fun completeRestore(callbackId: String, result: RestorationResult) {
            val deferred = pendingRestores.remove(callbackId)
            if (deferred == null) {
                logUnmatchedReply("restore", callbackId)
                return
            }
            deferred.complete(result)
        }

        fun isPending(callbackId: String): Boolean =
            pendingPurchases.containsKey(callbackId) || pendingRestores.containsKey(callbackId)

        // The awaiting coroutine was cancelled (e.g. paywall dismissed). Remember the id so a
        // late reply is recognised, and tell C# so it neither starts nor reports this request.
        private fun abandon(callbackId: String, method: String) {
            synchronized(abandonedCallbackIds) { abandonedCallbackIds[callbackId] = Unit }
            sendToUnity(method, JSONObject().apply { put("callbackId", callbackId) })
        }

        private fun logUnmatchedReply(kind: String, callbackId: String) {
            val abandoned = synchronized(abandonedCallbackIds) {
                abandonedCallbackIds.remove(callbackId) != null
            }
            if (abandoned) {
                android.util.Log.w(TAG, "Ignoring late $kind result for abandoned request $callbackId")
            } else {
                android.util.Log.w(TAG, "Ignoring $kind result for unknown request $callbackId")
            }
        }
    }
}

/**
 * Where an embedded paywall goes: left, top, width and height in pixels, from a rectangle given as fractions of a
 * container that is [containerWidth] by [containerHeight]. Clamped to the container, so a rectangle that hangs off
 * the screen is cut rather than laid out with a negative size.
 */
internal fun embeddedPaywallFrame(rect: FloatArray, containerWidth: Int, containerHeight: Int): IntArray {
    fun edge(fraction: Float, size: Int) = (fraction.coerceIn(0f, 1f) * size).toInt()
    val left = edge(rect[0], containerWidth)
    val top = edge(rect[1], containerHeight)
    val right = edge(rect[0] + rect[2], containerWidth)
    val bottom = edge(rect[1] + rect[3], containerHeight)
    return intArrayOf(left, top, maxOf(0, right - left), maxOf(0, bottom - top))
}

/** The `purchaseFromGooglePlay` request BridgeCallbackHandler.HandlePurchaseFromGooglePlay reads. */
internal fun googlePlayPurchaseRequest(
    callbackId: String,
    productId: String,
    basePlanId: String?,
    offerId: String?,
    replacement: ProductReplacement?
): JSONObject = JSONObject().apply {
    put("productId", productId)
    // Null means there is none: a product with no base plan, or the base plan with no offer. C# gets null, not "".
    put("basePlanId", basePlanId ?: JSONObject.NULL)
    put("offerId", offerId ?: JSONObject.NULL)
    put("callbackId", callbackId)
    putReplacement(replacement)
}

/** The `purchaseCustomProduct` request BridgeCallbackHandler.HandlePurchaseCustomProduct reads. */
internal fun customProductPurchaseRequest(
    callbackId: String,
    productId: String,
    customTransactionId: String?,
    replacement: ProductReplacement?
): JSONObject = JSONObject().apply {
    put("productId", productId)
    put("customTransactionId", customTransactionId ?: JSONObject.NULL)
    put("callbackId", callbackId)
    putReplacement(replacement)
}

private fun JSONObject.putReplacement(replacement: ProductReplacement?) {
    if (replacement == null) return
    put("replacement", JSONObject().apply {
        put("mode", replacement.mode.rawName)
        put("oldProductId", replacement.oldProductId ?: JSONObject.NULL)
        put("oldPurchaseToken", replacement.oldPurchaseToken ?: JSONObject.NULL)
    })
}

/** The string at [key], or null when it's missing or JSON null (`optString` would return "null"). */
private fun JSONObject.stringOrNull(key: String): String? = if (isNull(key)) null else optString(key)

/**
 * A JSON object from C# as the maps and lists the SDK expects. Nested objects and arrays used to be passed on as
 * `JSONObject`/`JSONArray`, which the SDK does not read as maps or lists. A null value stays null, which removes a
 * user attribute; nested nulls are dropped.
 */
internal fun jsonToMap(json: JSONObject): Map<String, Any?> {
    val map = mutableMapOf<String, Any?>()
    json.keys().forEach { key -> map[key] = fromJsonValue(json.get(key)) }
    return map
}

private fun fromJsonValue(value: Any?): Any? = when (value) {
    is JSONObject -> {
        val map = mutableMapOf<String, Any>()
        value.keys().forEach { key -> fromJsonValue(value.get(key))?.let { map[key] = it } }
        map
    }
    is JSONArray -> (0 until value.length()).mapNotNull { fromJsonValue(value.get(it)) }
    JSONObject.NULL -> null
    else -> value
}

/**
 * A map from the SDK as JSON for C#. `JSONObject(map)` keeps values it can't represent (dates, the SDK's own JSON
 * elements, NaN) in a form that either throws later or reaches C# as an unreadable string, so every value is
 * converted here.
 */
internal fun jsonObjectOf(map: Map<String, *>): JSONObject {
    val json = JSONObject()
    map.forEach { (key, value) -> json.put(key, toJsonValue(value)) }
    return json
}

private fun toJsonValue(value: Any?): Any = when (value) {
    null -> JSONObject.NULL
    is String, is Boolean, is Int, is Long -> value
    is Double -> if (value.isNaN() || value.isInfinite()) value.toString() else value
    is Float -> if (value.isNaN() || value.isInfinite()) value.toString() else value.toDouble()
    is Number -> value
    is Map<*, *> -> JSONObject().apply {
        value.forEach { (key, element) -> if (key != null) put(key.toString(), toJsonValue(element)) }
    }
    is Iterable<*> -> JSONArray().apply { value.forEach { put(toJsonValue(it)) } }
    is Array<*> -> JSONArray().apply { value.forEach { put(toJsonValue(it)) } }
    is JSONObject, is JSONArray -> value
    is Date -> value.time
    // kotlinx.serialization's JsonElement isn't on this module's compile classpath; its toString() is JSON.
    else -> if (value.javaClass.name.startsWith("kotlinx.serialization.json.")) {
        try {
            org.json.JSONTokener(value.toString()).nextValue() ?: JSONObject.NULL
        } catch (_: JSONException) {
            value.toString()
        }
    } else {
        value.toString()
    }
}
