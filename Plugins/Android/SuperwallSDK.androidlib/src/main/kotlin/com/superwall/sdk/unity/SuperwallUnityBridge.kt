package com.superwall.sdk.unity

import android.app.Activity
import android.net.Uri
import com.unity3d.player.UnityPlayer
import com.superwall.sdk.Superwall
import com.superwall.sdk.config.options.SuperwallOptions
import com.superwall.sdk.delegate.SuperwallDelegate
import com.superwall.sdk.delegate.PurchaseResult
import com.superwall.sdk.delegate.RestorationResult
import com.superwall.sdk.delegate.subscription_controller.PurchaseController
import com.superwall.sdk.models.attribution.AttributionProvider
import com.android.billingclient.api.ProductDetails
import kotlinx.coroutines.CompletableDeferred
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
        private var unityPurchaseController: UnityPurchaseController? = null
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
            val json = JSONObject(attributesJson)
            val map = mutableMapOf<String, Any>()
            json.keys().forEach { key -> map[key] = json.get(key) }
            Superwall.instance.setUserAttributes(map)
        } catch (_: JSONException) {}
    }

    fun getUserAttributes(): String = JSONObject(Superwall.instance.userAttributes).toString()

    fun setIntegrationAttribute(attribute: String, value: String?) {
        val provider = parseAttributionProvider(attribute) ?: return
        Superwall.instance.setIntegrationAttributes(mapOf(provider to (value ?: "")))
    }

    fun setIntegrationAttributes(attributesJson: String) {
        try {
            val json = JSONObject(attributesJson)
            val map = mutableMapOf<AttributionProvider, String>()
            json.keys().forEach { key ->
                val provider = parseAttributionProvider(key) ?: return@forEach
                map[provider] = json.optString(key, "")
            }
            if (map.isNotEmpty()) {
                Superwall.instance.setIntegrationAttributes(map)
            }
        } catch (_: JSONException) {}
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
            handler.onDismiss { info, result ->
                sendToUnity("onDismiss", JSONObject().apply {
                    put("handlerId", handlerId)
                    put("paywallInfo", serializePaywallInfo(info))
                    put("result", JSONObject().apply {
                        when (result) {
                            is com.superwall.sdk.paywall.presentation.internal.state.PaywallResult.Purchased -> {
                                put("type", "purchased")
                                put("productId", result.productId)
                            }
                            is com.superwall.sdk.paywall.presentation.internal.state.PaywallResult.Declined -> put("type", "declined")
                            is com.superwall.sdk.paywall.presentation.internal.state.PaywallResult.Restored -> put("type", "restored")
                        }
                    })
                })
            }
            handler.onError { error ->
                sendToUnity("onError", JSONObject().apply {
                    put("handlerId", handlerId)
                    put("error", error.localizedMessage ?: "Unknown error")
                })
            }
            handler.onSkip { reason ->
                val reasonStr = when (reason) {
                    is com.superwall.sdk.paywall.presentation.internal.state.PaywallSkippedReason.Holdout -> "holdout"
                    is com.superwall.sdk.paywall.presentation.internal.state.PaywallSkippedReason.NoAudienceMatch -> "noAudienceMatch"
                    is com.superwall.sdk.paywall.presentation.internal.state.PaywallSkippedReason.PlacementNotFound -> "placementNotFound"
                    is com.superwall.sdk.paywall.presentation.internal.state.PaywallSkippedReason.UserIsSubscribed -> "userIsSubscribed"
                    else -> "noAudienceMatch"
                }
                sendToUnity("onSkip", JSONObject().apply {
                    put("handlerId", handlerId)
                    put("reason", reasonStr)
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

    // --- Serialization ---

    private fun serializePaywallInfo(info: PaywallInfo) = JSONObject().apply {
        put("identifier", info.identifier)
        put("name", info.name)
        put("url", info.url.toString())
        put("productIds", JSONArray(info.productIds))
        put("products", JSONArray().apply {
            info.products.forEach { p ->
                put(JSONObject().apply {
                    put("id", p.compositeId)
                    put("name", p.name)
                    put("entitlements", serializeEntitlementSet(p.entitlements))
                })
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
            info.surveys.forEach { s ->
                put(JSONObject().apply {
                    put("id", s.id)
                    put("assignmentKey", s.assignmentKey)
                    put("title", s.title)
                    put("message", s.message)
                    put("options", JSONArray().apply {
                        s.options.forEach { o ->
                            put(JSONObject().apply {
                                put("id", o.id)
                                put("text", o.title)
                            })
                        }
                    })
                    put("presentationCondition", when (s.presentationCondition) {
                        com.superwall.sdk.config.models.SurveyShowCondition.ON_MANUAL_CLOSE -> "onManualClose"
                        com.superwall.sdk.config.models.SurveyShowCondition.ON_PURCHASE -> "onPurchase"
                    })
                    put("presentationProbability", s.presentationProbability)
                    put("includeOtherOption", s.includeOtherOption)
                    put("includeCloseOption", s.includeCloseOption)
                })
            }
        })
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

    private fun serializeRedemptionResult(result: RedemptionResult): JSONObject = JSONObject().apply {
        when (result) {
            is RedemptionResult.Success -> {
                put("type", "success")
                put("code", result.code)
                put("redemptionInfo", JSONObject().apply {
                    put("entitlements", serializeEntitlementSet(result.redemptionInfo.entitlements.toSet()))
                })
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
                put("redemptionInfo", JSONObject().apply {
                    put("entitlements", serializeEntitlementSet(result.redemptionInfo.entitlements.toSet()))
                })
            }
        }
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

        override suspend fun purchase(
            activity: Activity,
            productDetails: ProductDetails,
            basePlanId: String?,
            offerId: String?
        ): PurchaseResult {
            val callbackId = java.util.UUID.randomUUID().toString()
            val deferred = CompletableDeferred<PurchaseResult>()
            pendingPurchases[callbackId] = deferred

            sendToUnity("purchaseFromGooglePlay", JSONObject().apply {
                put("productId", productDetails.productId)
                put("basePlanId", basePlanId ?: "")
                put("offerId", offerId ?: "")
                put("callbackId", callbackId)
            })

            return try {
                deferred.await()
            } catch (t: Throwable) {
                pendingPurchases.remove(callbackId)
                PurchaseResult.Failed(t.message ?: "Purchase cancelled")
            }
        }

        override suspend fun restorePurchases(): RestorationResult {
            val callbackId = java.util.UUID.randomUUID().toString()
            val deferred = CompletableDeferred<RestorationResult>()
            pendingRestores[callbackId] = deferred

            sendToUnity("restorePurchases", JSONObject().apply {
                put("callbackId", callbackId)
            })

            return try {
                deferred.await()
            } catch (t: Throwable) {
                pendingRestores.remove(callbackId)
                RestorationResult.Failed(t)
            }
        }

        fun completePurchase(callbackId: String, result: PurchaseResult) {
            pendingPurchases.remove(callbackId)?.complete(result)
        }

        fun completeRestore(callbackId: String, result: RestorationResult) {
            pendingRestores.remove(callbackId)?.complete(result)
        }
    }
}
