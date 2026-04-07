package com.superwall.sdk.unity

import android.app.Activity
import android.net.Uri
import com.unity3d.player.UnityPlayer
import com.superwall.sdk.Superwall
import com.superwall.sdk.config.options.SuperwallOptions
import com.superwall.sdk.delegate.SuperwallDelegate
import com.superwall.sdk.delegate.PurchaseResult
import com.superwall.sdk.delegate.RestorationResult
import com.superwall.sdk.identity.IdentityOptions
import com.superwall.sdk.identity.identify
import com.superwall.sdk.identity.setUserAttributes
import com.superwall.sdk.logger.LogLevel
import com.superwall.sdk.logger.LogScope
import com.superwall.sdk.models.entitlements.Entitlement
import com.superwall.sdk.models.entitlements.SubscriptionStatus
import com.superwall.sdk.models.triggers.Experiment
import com.superwall.sdk.models.customer.CustomerInfo
import com.superwall.sdk.paywall.presentation.PaywallInfo
import com.superwall.sdk.paywall.presentation.PaywallPresentationHandler
import com.superwall.sdk.paywall.presentation.register
import com.superwall.sdk.paywall.presentation.dismissSync
import com.superwall.sdk.paywall.presentation.get_presentation_result.getPresentationResult
import com.superwall.sdk.paywall.presentation.result.PresentationResult
import com.superwall.sdk.analytics.superwall.SuperwallEventInfo
import java.net.URI
import com.superwall.sdk.models.internal.RedemptionResult
import com.superwall.sdk.store.abstractions.product.StoreProduct
import kotlinx.coroutines.*
import kotlin.time.Duration.Companion.seconds
import org.json.JSONArray
import org.json.JSONException
import org.json.JSONObject

@Suppress("unused")
class SuperwallUnityBridge {

    companion object {
        private var unityDelegate: SuperwallUnityDelegateImpl? = null
        private val scope = CoroutineScope(Dispatchers.Main + SupervisorJob())

        private fun sendToUnity(method: String, data: JSONObject) {
            try {
                val payload = JSONObject()
                payload.put("method", method)
                payload.put("data", data)
                UnityPlayer.UnitySendMessage("SuperwallBridge", "OnCallback", payload.toString())
            } catch (e: Exception) {
                // Ignore send failures
            }
        }

        private fun sendAsyncResponse(callbackId: String, data: JSONObject) {
            try {
                data.put("callbackId", callbackId)
                sendToUnity("asyncResponse", data)
            } catch (_: Exception) {}
        }

        fun serializeSubscriptionStatus(status: SubscriptionStatus) = JSONObject().apply {
            when (status) {
                is SubscriptionStatus.Active -> put("type", "active")
                is SubscriptionStatus.Inactive -> put("type", "inactive")
                is SubscriptionStatus.Unknown -> put("type", "unknown")
            }
        }
    }

    fun configure(apiKey: String, optionsJson: String?, hasPurchaseController: Boolean, completionCallbackId: String?) {
        val activity = UnityPlayer.currentActivity ?: return
        val options = if (optionsJson != null) parseOptions(optionsJson) else null

        val unityActivityProvider = object : com.superwall.sdk.misc.ActivityProvider {
            override fun getCurrentActivity(): Activity? = UnityPlayer.currentActivity
        }

        Superwall.configure(activity.application, apiKey, options = options, activityProvider = unityActivityProvider) {
            completionCallbackId?.let { cbId ->
                sendAsyncResponse(cbId, JSONObject().put("success", true))
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
            val json = JSONObject(attributesJson)
            val map = mutableMapOf<String, Any>()
            json.keys().forEach { key -> map[key] = json.get(key) }
            Superwall.instance.setUserAttributes(map)
        } catch (_: JSONException) {}
    }

    fun getUserAttributes(): String = JSONObject(Superwall.instance.userAttributes).toString()

    fun setIntegrationAttribute(attribute: String, value: String?) {}
    fun setIntegrationAttributes(attributesJson: String) {}

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
        }.toString()
    }

    fun getEntitlementsByProductIds(productIdsJson: String): String = "[]"

    fun getCustomerInfo(callbackId: String) {
        scope.launch {
            val info = Superwall.instance.getCustomerInfo()
            sendAsyncResponse(callbackId, serializeCustomerInfo(info))
        }
    }

    fun getSubscriptionStatus(): String = serializeSubscriptionStatus(Superwall.instance.subscriptionStatus.value).toString()

    fun setSubscriptionStatus(statusJson: String) {
        try {
            val type = JSONObject(statusJson).optString("type", "unknown")
            val status: SubscriptionStatus = when (type.lowercase()) {
                "active" -> SubscriptionStatus.Active(emptySet())
                "inactive" -> SubscriptionStatus.Inactive
                else -> SubscriptionStatus.Unknown
            }
            Superwall.instance.setSubscriptionStatus(status)
        } catch (_: JSONException) {}
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
        return Superwall.instance.handleDeepLink(Uri.parse(url)).getOrDefault(false)
    }

    fun togglePaywallSpinner(isHidden: Boolean) { Superwall.instance.togglePaywallSpinner(isHidden) }

    fun getLatestPaywallInfo(): String? {
        return Superwall.instance.latestPaywallInfo?.let { serializePaywallInfo(it).toString() }
    }

    fun registerPlacement(placement: String, paramsJson: String?, handlerId: String?, featureId: String?, callbackId: String?) {
        val params = paramsJson?.let {
            try {
                val json = JSONObject(it)
                val map = mutableMapOf<String, Any>()
                json.keys().forEach { key -> map[key] = json.get(key) }
                map
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
            handler.onDismiss { info, _ ->
                sendToUnity("onDismiss", JSONObject().apply {
                    put("handlerId", handlerId)
                    put("paywallInfo", serializePaywallInfo(info))
                })
            }
            handler.onError { error ->
                sendToUnity("onError", JSONObject().apply {
                    put("handlerId", handlerId)
                    put("error", error.localizedMessage ?: "Unknown error")
                })
            }
            handler.onSkip { reason ->
                sendToUnity("onSkip", JSONObject().apply {
                    put("handlerId", handlerId)
                    put("reason", reason::class.simpleName ?: "unknown")
                })
            }
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

    fun dismiss() { Superwall.instance.dismissSync() }

    fun getPresentationResult(placement: String, paramsJson: String?, callbackId: String) {
        val params = paramsJson?.let {
            try {
                val json = JSONObject(it)
                val map = mutableMapOf<String, Any>()
                json.keys().forEach { key -> map[key] = json.get(key) }
                map
            } catch (_: JSONException) { null }
        }
        scope.launch {
            val result = Superwall.instance.getPresentationResult(placement, params).getOrNull()
            if (result != null) {
                sendAsyncResponse(callbackId, serializePresentationResult(result))
            }
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
            val result = Superwall.instance.restorePurchases()
            when (result) {
                is RestorationResult.Restored -> sendAsyncResponse(callbackId, JSONObject().put("type", "restored"))
                is RestorationResult.Failed -> sendAsyncResponse(callbackId, JSONObject().put("type", "failed").put("error", result.error?.message ?: ""))
            }
        }
    }

    fun getOverrideProductsByName(): String? = null
    fun setOverrideProductsByName(productsJson: String?) {}
    fun consume(purchaseToken: String, callbackId: String) { sendAsyncResponse(callbackId, JSONObject().put("result", "not_implemented")) }
    fun respondToPurchaseController(callbackId: String, resultJson: String) {}
    fun respondToRestorePurchases(callbackId: String, resultJson: String) {}

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
            val actionCallbackId = json.optString("actionCallbackId", null)
            val closeCallbackId = json.optString("closeCallbackId", null)

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

    // --- Serialization ---

    private fun serializePaywallInfo(info: PaywallInfo) = JSONObject().apply {
        put("identifier", info.identifier)
        put("name", info.name)
        put("url", info.url)
        put("productIds", JSONArray(info.productIds))
        info.experiment?.let { put("experiment", serializeExperiment(it)) }
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

    private fun serializeEntitlement(e: Entitlement) = JSONObject().apply {
        put("id", e.id)
        put("type", "serviceLevel")
        put("isActive", e.isActive)
        put("productIds", JSONArray(e.productIds.toList()))
    }

    private fun serializeEntitlementSet(set: Set<Entitlement>) = JSONArray().apply {
        set.forEach { put(serializeEntitlement(it)) }
    }

    private fun serializeCustomerInfo(info: CustomerInfo) = JSONObject().apply {
        put("userId", info.userId)
        put("entitlements", JSONArray().apply { info.entitlements.forEach { put(serializeEntitlement(it)) } })
    }

    private fun serializeStoreProduct(product: StoreProduct) = JSONObject().apply {
        put("productIdentifier", product.productIdentifier)
        put("fullIdentifier", product.fullIdentifier)
        put("localizedPrice", product.localizedPrice)
        put("localizedSubscriptionPeriod", product.localizedSubscriptionPeriod)
        put("period", product.period)
        put("periodly", product.periodly)
        put("periodDays", product.periodDays)
        put("periodWeeks", product.periodWeeks)
        put("periodMonths", product.periodMonths)
        put("periodYears", product.periodYears)
        put("dailyPrice", product.dailyPrice)
        put("weeklyPrice", product.weeklyPrice)
        put("monthlyPrice", product.monthlyPrice)
        put("yearlyPrice", product.yearlyPrice)
        put("hasFreeTrial", product.hasFreeTrial)
        put("trialPeriodDays", product.trialPeriodDays)
        put("trialPeriodWeeks", product.trialPeriodWeeks)
        put("trialPeriodMonths", product.trialPeriodMonths)
        put("trialPeriodYears", product.trialPeriodYears)
        put("trialPeriodText", product.trialPeriodText)
        put("trialPeriodPrice", product.trialPeriodPrice.toString())
        put("localizedTrialPeriodPrice", product.localizedTrialPeriodPrice)
        put("currencyCode", product.currencyCode)
        put("currencySymbol", product.currencySymbol)
        put("locale", product.locale)
        put("languageCode", product.languageCode)
        put("price", product.price.toString())
        put("productType", product.productType)
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
            if (json.has("localeIdentifier")) localeIdentifier = json.getString("localeIdentifier")
            if (json.has("isExternalDataCollectionEnabled")) isExternalDataCollectionEnabled = json.getBoolean("isExternalDataCollectionEnabled")
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
                        val scope = when (scopesArray.getString(i).lowercase()) {
                            "all" -> LogScope.all
                            "paywallevents" -> LogScope.paywallEvents
                            "paywallpresentation" -> LogScope.paywallPresentation
                            "network" -> LogScope.network
                            "productsmanager" -> LogScope.productsManager
                            "superwallcore" -> LogScope.superwallCore
                            "configmanager" -> LogScope.configManager
                            "identitymanager" -> LogScope.identityManager
                            "localizationmanager" -> LogScope.localizationManager
                            "storekitmanager" -> LogScope.storeKitManager
                            "bouncebutton" -> LogScope.bounceButton
                            "device" -> LogScope.device
                            else -> null
                        }
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
                if (paywallsJson.has("timeoutAfter")) paywalls.timeoutAfter = paywallsJson.getDouble("timeoutAfter").seconds
                if (paywallsJson.has("restoreFailed")) {
                    val restoreJson = paywallsJson.getJSONObject("restoreFailed")
                    if (restoreJson.has("title")) paywalls.restoreFailed.title = restoreJson.getString("title")
                    if (restoreJson.has("message")) paywalls.restoreFailed.message = restoreJson.getString("message")
                    if (restoreJson.has("closeButtonTitle")) paywalls.restoreFailed.closeButtonTitle = restoreJson.getString("closeButtonTitle")
                }
            }
        }
    } catch (_: JSONException) { null }

    // --- Delegate ---

    private class SuperwallUnityDelegateImpl : SuperwallDelegate {
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
            })
        }

        override fun handleCustomPaywallAction(withName: String) {
            sendToUnity("handleCustomPaywallAction", JSONObject().put("name", withName))
        }

        override fun willDismissPaywall(withInfo: PaywallInfo) {
            sendToUnity("willDismissPaywall", JSONObject().put("identifier", withInfo.identifier))
        }

        override fun willPresentPaywall(withInfo: PaywallInfo) {
            sendToUnity("willPresentPaywall", JSONObject().put("identifier", withInfo.identifier))
        }

        override fun didDismissPaywall(withInfo: PaywallInfo) {
            sendToUnity("didDismissPaywall", JSONObject().put("identifier", withInfo.identifier))
        }

        override fun didPresentPaywall(withInfo: PaywallInfo) {
            sendToUnity("didPresentPaywall", JSONObject().put("identifier", withInfo.identifier))
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
            })
        }

        override fun willRedeemLink() {
            sendToUnity("willRedeemLink", JSONObject())
        }

        override fun didRedeemLink(result: RedemptionResult) {
            sendToUnity("didRedeemLink", JSONObject().apply {
                put("result", JSONObject().apply {
                    when (result) {
                        is RedemptionResult.Success -> put("status", "success")
                        is RedemptionResult.Error -> {
                            put("status", "error")
                            put("error", result.error.message ?: "")
                        }
                        is RedemptionResult.Expired -> put("status", "expired")
                        is RedemptionResult.InvalidCode -> put("status", "invalidCode")
                        is RedemptionResult.ExpiredSubscription -> put("status", "expiredSubscription")
                    }
                })
            })
        }

        override fun userAttributesDidChange(newAttributes: Map<String, Any>) {
            sendToUnity("userAttributesDidChange", JSONObject().apply {
                put("attributes", JSONObject(newAttributes))
            })
        }

        override fun customerInfoDidChange(from: CustomerInfo, to: CustomerInfo) {
            sendToUnity("customerInfoDidChange", JSONObject().apply {
                put("from", JSONObject().put("userId", from.userId))
                put("to", JSONObject().put("userId", to.userId))
            })
        }
    }
}
