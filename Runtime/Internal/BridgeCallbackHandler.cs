using System;
using System.Collections.Generic;
using UnityEngine;
using Superwall;

namespace Superwall.Internal
{
    public class BridgeCallbackHandler : MonoBehaviour
    {
        private static BridgeCallbackHandler _instance;
        public static BridgeCallbackHandler Instance => _instance;

        public ISuperwallDelegate Delegate { get; set; }
        public IPurchaseController PurchaseController { get; set; }

        private readonly Dictionary<string, PaywallPresentationHandler> _presentationHandlers =
            new Dictionary<string, PaywallPresentationHandler>();

        private readonly Dictionary<string, Action> _featureHandlers =
            new Dictionary<string, Action>();

        private readonly Dictionary<string, Action<string>> _pendingAsyncCallbacks =
            new Dictionary<string, Action<string>>();

        public static void Initialize()
        {
            if (_instance != null) return;

            var go = new GameObject("SuperwallBridge");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BridgeCallbackHandler>();
        }

        public void RegisterPresentationHandler(string handlerId, PaywallPresentationHandler handler)
        {
            _presentationHandlers[handlerId] = handler;
        }

        public void UnregisterPresentationHandler(string handlerId)
        {
            _presentationHandlers.Remove(handlerId);
        }

        public void RegisterFeatureHandler(string handlerId, Action feature)
        {
            _featureHandlers[handlerId] = feature;
        }

        public void RegisterAsyncCallback(string callbackId, Action<string> callback)
        {
            _pendingAsyncCallbacks[callbackId] = callback;
        }

        // Called by native code via UnitySendMessage("SuperwallBridge", "OnCallback", jsonPayload)
        public void OnCallback(string jsonPayload)
        {
            try
            {
                var payload = Json.Deserialize(jsonPayload) as Dictionary<string, object>;
                if (payload == null)
                {
                    Debug.LogError("[Superwall] Failed to deserialize callback payload.");
                    return;
                }

                var method = payload.ContainsKey("method") ? payload["method"] as string : null;
                var data = payload.ContainsKey("data") ? payload["data"] as Dictionary<string, object> : null;

                if (string.IsNullOrEmpty(method))
                {
                    Debug.LogError("[Superwall] Callback payload missing 'method' field.");
                    return;
                }

                RouteCallback(method, data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Superwall] Error handling callback: {e.Message}\n{e.StackTrace}");
            }
        }

        private void RouteCallback(string method, Dictionary<string, object> data)
        {
            switch (method)
            {
                // --- Delegate methods ---
                case "subscriptionStatusDidChange":
                    HandleSubscriptionStatusDidChange(data);
                    break;
                case "handleSuperwallEvent":
                    HandleSuperwallEvent(data);
                    break;
                case "handleCustomPaywallAction":
                    HandleCustomPaywallAction(data);
                    break;
                case "willDismissPaywall":
                    HandleWillDismissPaywall(data);
                    break;
                case "willPresentPaywall":
                    HandleWillPresentPaywall(data);
                    break;
                case "didDismissPaywall":
                    HandleDidDismissPaywall(data);
                    break;
                case "didPresentPaywall":
                    HandleDidPresentPaywall(data);
                    break;
                case "paywallWillOpenURL":
                    HandlePaywallWillOpenURL(data);
                    break;
                case "paywallWillOpenDeepLink":
                    HandlePaywallWillOpenDeepLink(data);
                    break;
                case "handleLog":
                    HandleLog(data);
                    break;
                case "willRedeemLink":
                    HandleWillRedeemLink(data);
                    break;
                case "didRedeemLink":
                    HandleDidRedeemLink(data);
                    break;
                case "handleSuperwallDeepLink":
                    HandleSuperwallDeepLink(data);
                    break;
                case "customerInfoDidChange":
                    HandleCustomerInfoDidChange(data);
                    break;
                case "userAttributesDidChange":
                    HandleUserAttributesDidChange(data);
                    break;

                // --- Purchase controller methods ---
                case "purchaseFromAppStore":
                    HandlePurchaseFromAppStore(data);
                    break;
                case "purchaseFromGooglePlay":
                    HandlePurchaseFromGooglePlay(data);
                    break;
                case "restorePurchases":
                    HandleRestorePurchases(data);
                    break;

                // --- Presentation handler methods ---
                case "onPresent":
                    HandleOnPresent(data);
                    break;
                case "onDismiss":
                    HandleOnDismiss(data);
                    break;
                case "onError":
                    HandleOnError(data);
                    break;
                case "onSkip":
                    HandleOnSkip(data);
                    break;
                case "onCustomCallback":
                    HandleOnCustomCallback(data);
                    break;

                // --- Feature handler ---
                case "onFeature":
                    HandleOnFeature(data);
                    break;

                // --- Async response ---
                case "asyncResponse":
                    HandleAsyncResponse(data);
                    break;

                default:
                    Debug.LogWarning($"[Superwall] Unknown callback method: {method}");
                    break;
            }
        }

        #region Delegate Handlers

        private void HandleSubscriptionStatusDidChange(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            // TODO: Deserialize SubscriptionStatus from/to from data
            SubscriptionStatus from = null;
            SubscriptionStatus to = null;
            Delegate.SubscriptionStatusDidChange(from, to);
        }

        private void HandleSuperwallEvent(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            // TODO: Deserialize SuperwallEventInfo from data
            SuperwallEventInfo eventInfo = null;
            Delegate.HandleSuperwallEvent(eventInfo);
        }

        private void HandleCustomPaywallAction(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            var name = data != null && data.ContainsKey("name") ? data["name"] as string : null;
            Delegate.HandleCustomPaywallAction(name);
        }

        private void HandleWillDismissPaywall(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            // TODO: Deserialize PaywallInfo from data
            PaywallInfo paywallInfo = null;
            Delegate.WillDismissPaywall(paywallInfo);
        }

        private void HandleWillPresentPaywall(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            // TODO: Deserialize PaywallInfo from data
            PaywallInfo paywallInfo = null;
            Delegate.WillPresentPaywall(paywallInfo);
        }

        private void HandleDidDismissPaywall(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            // TODO: Deserialize PaywallInfo from data
            PaywallInfo paywallInfo = null;
            Delegate.DidDismissPaywall(paywallInfo);
        }

        private void HandleDidPresentPaywall(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            // TODO: Deserialize PaywallInfo from data
            PaywallInfo paywallInfo = null;
            Delegate.DidPresentPaywall(paywallInfo);
        }

        private void HandlePaywallWillOpenURL(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            var url = data != null && data.ContainsKey("url") ? data["url"] as string : null;
            Delegate.PaywallWillOpenURL(url);
        }

        private void HandlePaywallWillOpenDeepLink(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            var url = data != null && data.ContainsKey("url") ? data["url"] as string : null;
            Delegate.PaywallWillOpenDeepLink(url);
        }

        private void HandleLog(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            var level = data != null && data.ContainsKey("level") ? data["level"] as string : null;
            var scope = data != null && data.ContainsKey("scope") ? data["scope"] as string : null;
            var message = data != null && data.ContainsKey("message") ? data["message"] as string : null;
            var info = data != null && data.ContainsKey("info") ? data["info"] as Dictionary<string, object> : null;
            var error = data != null && data.ContainsKey("error") ? data["error"] as string : null;

            Delegate.HandleLog(level, scope, message, info, error);
        }

        private void HandleWillRedeemLink(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            Delegate.WillRedeemLink();
        }

        private void HandleDidRedeemLink(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            // TODO: Deserialize RedemptionResult from data
            RedemptionResult result = null;
            Delegate.DidRedeemLink(result);
        }

        private void HandleSuperwallDeepLink(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            var fullURL = data != null && data.ContainsKey("fullURL") ? data["fullURL"] as string : null;
            // TODO: Deserialize pathComponents and queryParameters from data
            List<string> pathComponents = null;
            Dictionary<string, string> queryParameters = null;
            Delegate.HandleSuperwallDeepLink(fullURL, pathComponents, queryParameters);
        }

        private void HandleCustomerInfoDidChange(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            // TODO: Deserialize CustomerInfo from/to from data
            CustomerInfo from = null;
            CustomerInfo to = null;
            Delegate.CustomerInfoDidChange(from, to);
        }

        private void HandleUserAttributesDidChange(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            // TODO: Deserialize newAttributes from data
            var newAttributes = data != null && data.ContainsKey("newAttributes")
                ? data["newAttributes"] as Dictionary<string, object>
                : null;
            Delegate.UserAttributesDidChange(newAttributes);
        }

        #endregion

        #region Purchase Controller Handlers

        private void HandlePurchaseFromAppStore(Dictionary<string, object> data)
        {
            if (PurchaseController == null) return;

            var productId = data != null && data.ContainsKey("productId") ? data["productId"] as string : null;
            var callbackId = data != null && data.ContainsKey("callbackId") ? data["callbackId"] as string : null;

            PurchaseController.PurchaseFromAppStore(productId, (result) =>
            {
                // TODO: Serialize PurchaseResult and send back to native via callbackId
            });
        }

        private void HandlePurchaseFromGooglePlay(Dictionary<string, object> data)
        {
            if (PurchaseController == null) return;

            var productId = data != null && data.ContainsKey("productId") ? data["productId"] as string : null;
            var basePlanId = data != null && data.ContainsKey("basePlanId") ? data["basePlanId"] as string : null;
            var offerId = data != null && data.ContainsKey("offerId") ? data["offerId"] as string : null;
            var callbackId = data != null && data.ContainsKey("callbackId") ? data["callbackId"] as string : null;

            PurchaseController.PurchaseFromGooglePlay(productId, basePlanId, offerId, (result) =>
            {
                // TODO: Serialize PurchaseResult and send back to native via callbackId
            });
        }

        private void HandleRestorePurchases(Dictionary<string, object> data)
        {
            if (PurchaseController == null) return;

            var callbackId = data != null && data.ContainsKey("callbackId") ? data["callbackId"] as string : null;

            PurchaseController.RestorePurchases((result) =>
            {
                // TODO: Serialize RestorationResult and send back to native via callbackId
            });
        }

        #endregion

        #region Presentation Handler Methods

        private void HandleOnPresent(Dictionary<string, object> data)
        {
            var handlerId = data != null && data.ContainsKey("handlerId") ? data["handlerId"] as string : null;
            if (handlerId == null || !_presentationHandlers.ContainsKey(handlerId)) return;

            var handler = _presentationHandlers[handlerId];
            if (handler.OnPresent == null) return;

            // TODO: Deserialize PaywallInfo from data
            PaywallInfo paywallInfo = null;
            handler.OnPresent(paywallInfo);
        }

        private void HandleOnDismiss(Dictionary<string, object> data)
        {
            var handlerId = data != null && data.ContainsKey("handlerId") ? data["handlerId"] as string : null;
            if (handlerId == null || !_presentationHandlers.ContainsKey(handlerId)) return;

            var handler = _presentationHandlers[handlerId];
            if (handler.OnDismiss == null) return;

            // TODO: Deserialize PaywallInfo and PaywallResult from data
            PaywallInfo paywallInfo = null;
            PaywallResult paywallResult = null;
            handler.OnDismiss(paywallInfo, paywallResult);
        }

        private void HandleOnError(Dictionary<string, object> data)
        {
            var handlerId = data != null && data.ContainsKey("handlerId") ? data["handlerId"] as string : null;
            if (handlerId == null || !_presentationHandlers.ContainsKey(handlerId)) return;

            var handler = _presentationHandlers[handlerId];
            if (handler.OnError == null) return;

            var error = data.ContainsKey("error") ? data["error"] as string : null;
            handler.OnError(error);
        }

        private void HandleOnSkip(Dictionary<string, object> data)
        {
            var handlerId = data != null && data.ContainsKey("handlerId") ? data["handlerId"] as string : null;
            if (handlerId == null || !_presentationHandlers.ContainsKey(handlerId)) return;

            var handler = _presentationHandlers[handlerId];
            if (handler.OnSkip == null) return;

            // TODO: Deserialize PaywallSkippedReason from data
            PaywallSkippedReason reason = default;
            handler.OnSkip(reason);
        }

        private void HandleOnCustomCallback(Dictionary<string, object> data)
        {
            var handlerId = data != null && data.ContainsKey("handlerId") ? data["handlerId"] as string : null;
            if (handlerId == null || !_presentationHandlers.ContainsKey(handlerId)) return;

            var handler = _presentationHandlers[handlerId];
            if (handler.OnCustomCallback == null) return;

            // TODO: Deserialize CustomCallback from data
            CustomCallback customCallback = null;
            CustomCallbackResult result = handler.OnCustomCallback(customCallback);
            // TODO: Serialize result and send back to native
        }

        #endregion

        #region Feature Handler

        private void HandleOnFeature(Dictionary<string, object> data)
        {
            var handlerId = data != null && data.ContainsKey("handlerId") ? data["handlerId"] as string : null;
            if (handlerId == null || !_featureHandlers.ContainsKey(handlerId)) return;

            var handler = _featureHandlers[handlerId];
            handler?.Invoke();

            // Clean up one-shot feature handler after invocation
            _featureHandlers.Remove(handlerId);
        }

        #endregion

        #region Async Response

        private void HandleAsyncResponse(Dictionary<string, object> data)
        {
            var callbackId = data != null && data.ContainsKey("callbackId") ? data["callbackId"] as string : null;
            if (callbackId == null || !_pendingAsyncCallbacks.ContainsKey(callbackId)) return;

            var callback = _pendingAsyncCallbacks[callbackId];
            _pendingAsyncCallbacks.Remove(callbackId);

            var responseData = data.ContainsKey("response") ? data["response"] as string : null;
            callback?.Invoke(responseData);
        }

        #endregion
    }
}
