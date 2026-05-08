using System;
using System.Collections.Generic;
using System.Linq;
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

        #region Deserialization Helpers

        private static string GetString(Dictionary<string, object> data, string key)
        {
            if (data != null && data.ContainsKey(key))
                return data[key] as string;
            return null;
        }

        private static Dictionary<string, object> GetDict(Dictionary<string, object> data, string key)
        {
            if (data != null && data.ContainsKey(key))
                return data[key] as Dictionary<string, object>;
            return null;
        }

        private static List<object> GetList(Dictionary<string, object> data, string key)
        {
            if (data != null && data.ContainsKey(key))
                return data[key] as List<object>;
            return null;
        }

        private static bool GetBool(Dictionary<string, object> data, string key, bool defaultValue = false)
        {
            if (data != null && data.ContainsKey(key))
            {
                var val = data[key];
                if (val is bool b) return b;
                if (val is string s) return s.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            return defaultValue;
        }

        private static long GetLong(Dictionary<string, object> data, string key, long defaultValue = 0)
        {
            if (data != null && data.ContainsKey(key))
            {
                var val = data[key];
                if (val is long l) return l;
                if (val is double d) return (long)d;
                if (val is int i) return i;
                if (val is string s && long.TryParse(s, out var parsed)) return parsed;
            }
            return defaultValue;
        }

        private static double? GetDoubleNullable(Dictionary<string, object> data, string key)
        {
            if (data != null && data.ContainsKey(key) && data[key] != null)
            {
                var val = data[key];
                if (val is double d) return d;
                if (val is long l) return l;
                if (val is int i) return i;
            }
            return null;
        }

        private static int? GetIntNullable(Dictionary<string, object> data, string key)
        {
            if (data != null && data.ContainsKey(key) && data[key] != null)
            {
                var val = data[key];
                if (val is long l) return (int)l;
                if (val is double d) return (int)d;
                if (val is int i) return i;
            }
            return null;
        }

        private static T? ParseEnum<T>(string value) where T : struct
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (Enum.TryParse<T>(value, true, out var result)) return result;
            return null;
        }

        private static SubscriptionStatus DeserializeSubscriptionStatus(Dictionary<string, object> data)
        {
            if (data == null) return SubscriptionStatus.CreateUnknown();

            var type = GetString(data, "type");
            switch (type?.ToLowerInvariant())
            {
                case "active":
                    var entitlementsList = GetList(data, "entitlements");
                    var entitlements = new List<Entitlement>();
                    if (entitlementsList != null)
                    {
                        foreach (var item in entitlementsList)
                        {
                            if (item is Dictionary<string, object> entDict)
                                entitlements.Add(DeserializeEntitlement(entDict));
                        }
                    }
                    return SubscriptionStatus.CreateActive(entitlements);
                case "inactive":
                    return SubscriptionStatus.CreateInactive();
                default:
                    return SubscriptionStatus.CreateUnknown();
            }
        }

        private static Entitlement DeserializeEntitlement(Dictionary<string, object> data)
        {
            if (data == null) return null;

            var entitlement = new Entitlement();
            entitlement.Id = GetString(data, "id");
            entitlement.IsActive = GetBool(data, "isActive");

            var typeStr = GetString(data, "type");
            entitlement.Type = ParseEnum<EntitlementType>(typeStr) ?? EntitlementType.ServiceLevel;

            var productIdsList = GetList(data, "productIds");
            if (productIdsList != null)
                entitlement.ProductIds = productIdsList.Select(p => p?.ToString()).Where(p => p != null).ToList();

            entitlement.LatestProductId = GetString(data, "latestProductId");

            var storeStr = GetString(data, "store");
            entitlement.Store = ParseEnum<ProductStore>(storeStr);

            entitlement.StartsAt = data.ContainsKey("startsAt") && data["startsAt"] != null ? (long?)GetLong(data, "startsAt") : null;
            entitlement.RenewedAt = data.ContainsKey("renewedAt") && data["renewedAt"] != null ? (long?)GetLong(data, "renewedAt") : null;
            entitlement.ExpiresAt = data.ContainsKey("expiresAt") && data["expiresAt"] != null ? (long?)GetLong(data, "expiresAt") : null;

            if (data.ContainsKey("isLifetime") && data["isLifetime"] != null)
                entitlement.IsLifetime = GetBool(data, "isLifetime");
            if (data.ContainsKey("willRenew") && data["willRenew"] != null)
                entitlement.WillRenew = GetBool(data, "willRenew");

            var stateStr = GetString(data, "state");
            entitlement.State = ParseEnum<LatestSubscriptionState>(stateStr);

            var offerTypeStr = GetString(data, "offerType");
            entitlement.OfferType = ParseEnum<LatestSubscriptionOfferType>(offerTypeStr);

            return entitlement;
        }

        private static PaywallInfo DeserializePaywallInfo(Dictionary<string, object> data)
        {
            if (data == null) return null;

            var info = new PaywallInfo();
            info.Identifier = GetString(data, "identifier");
            info.Name = GetString(data, "name");
            info.Url = GetString(data, "url");

            var productIdsList = GetList(data, "productIds");
            if (productIdsList != null)
                info.ProductIds = productIdsList.Select(p => p?.ToString()).Where(p => p != null).ToList();

            var productsList = GetList(data, "products");
            if (productsList != null)
            {
                info.Products = new List<Product>();
                foreach (var item in productsList)
                {
                    if (item is Dictionary<string, object> pDict)
                        info.Products.Add(DeserializeProduct(pDict));
                }
            }

            var experimentDict = GetDict(data, "experiment");
            if (experimentDict != null)
                info.Experiment = DeserializeExperiment(experimentDict);

            info.PresentedByPlacementWithName = GetString(data, "presentedByPlacementWithName");
            info.PresentedByPlacementWithId = GetString(data, "presentedByPlacementWithId");
            info.PresentedByPlacementAt = GetString(data, "presentedByPlacementAt");
            info.PresentedBy = GetString(data, "presentedBy");
            info.PresentationSourceType = GetString(data, "presentationSourceType");
            info.PaywalljsVersion = GetString(data, "paywalljsVersion");

            info.ResponseLoadStartTime = GetString(data, "responseLoadStartTime");
            info.ResponseLoadCompleteTime = GetString(data, "responseLoadCompleteTime");
            info.ResponseLoadFailTime = GetString(data, "responseLoadFailTime");
            info.ResponseLoadDuration = GetDoubleNullable(data, "responseLoadDuration");

            info.WebViewLoadStartTime = GetString(data, "webViewLoadStartTime");
            info.WebViewLoadCompleteTime = GetString(data, "webViewLoadCompleteTime");
            info.WebViewLoadFailTime = GetString(data, "webViewLoadFailTime");
            info.WebViewLoadDuration = GetDoubleNullable(data, "webViewLoadDuration");

            info.ProductsLoadStartTime = GetString(data, "productsLoadStartTime");
            info.ProductsLoadCompleteTime = GetString(data, "productsLoadCompleteTime");
            info.ProductsLoadFailTime = GetString(data, "productsLoadFailTime");
            info.ProductsLoadDuration = GetDoubleNullable(data, "productsLoadDuration");

            if (data.ContainsKey("isFreeTrialAvailable") && data["isFreeTrialAvailable"] != null)
                info.IsFreeTrialAvailable = GetBool(data, "isFreeTrialAvailable");

            var featureGatingStr = GetString(data, "featureGatingBehavior");
            info.FeatureGatingBehavior = ParseEnum<FeatureGatingBehavior>(featureGatingStr);

            var closeReasonStr = GetString(data, "closeReason");
            info.CloseReason = ParseEnum<PaywallCloseReason>(closeReasonStr);

            var localNotifList = GetList(data, "localNotifications");
            if (localNotifList != null)
            {
                info.LocalNotifications = new List<LocalNotification>();
                foreach (var item in localNotifList)
                {
                    if (item is Dictionary<string, object> nDict)
                        info.LocalNotifications.Add(DeserializeLocalNotification(nDict));
                }
            }

            var computedList = GetList(data, "computedPropertyRequests");
            if (computedList != null)
            {
                info.ComputedPropertyRequests = new List<ComputedPropertyRequest>();
                foreach (var item in computedList)
                {
                    if (item is Dictionary<string, object> cDict)
                        info.ComputedPropertyRequests.Add(DeserializeComputedPropertyRequest(cDict));
                }
            }

            var surveysList = GetList(data, "surveys");
            if (surveysList != null)
            {
                info.Surveys = new List<Survey>();
                foreach (var item in surveysList)
                {
                    if (item is Dictionary<string, object> sDict)
                        info.Surveys.Add(DeserializeSurvey(sDict));
                }
            }

            info.State = GetDict(data, "state");

            return info;
        }

        private static Product DeserializeProduct(Dictionary<string, object> data)
        {
            if (data == null) return null;
            var p = new Product();
            p.Id = GetString(data, "id");
            p.Name = GetString(data, "name");
            var entList = GetList(data, "entitlements");
            if (entList != null)
            {
                p.Entitlements = new List<Entitlement>();
                foreach (var item in entList)
                {
                    if (item is Dictionary<string, object> eDict)
                        p.Entitlements.Add(DeserializeEntitlement(eDict));
                }
            }
            return p;
        }

        private static LocalNotification DeserializeLocalNotification(Dictionary<string, object> data)
        {
            if (data == null) return null;
            var n = new LocalNotification();
            n.Id = GetString(data, "id");
            n.Type = ParseEnum<LocalNotificationType>(GetString(data, "type")) ?? LocalNotificationType.Unsupported;
            n.Title = GetString(data, "title");
            n.Subtitle = GetString(data, "subtitle");
            n.Body = GetString(data, "body");
            n.Delay = GetIntNullable(data, "delay") ?? 0;
            return n;
        }

        private static ComputedPropertyRequest DeserializeComputedPropertyRequest(Dictionary<string, object> data)
        {
            if (data == null) return null;
            var c = new ComputedPropertyRequest();
            c.Type = ParseEnum<ComputedPropertyRequestType>(GetString(data, "type")) ?? ComputedPropertyRequestType.MinutesSince;
            c.EventName = GetString(data, "eventName");
            return c;
        }

        private static Survey DeserializeSurvey(Dictionary<string, object> data)
        {
            if (data == null) return null;
            var s = new Survey();
            s.Id = GetString(data, "id");
            s.AssignmentKey = GetString(data, "assignmentKey");
            s.Title = GetString(data, "title");
            s.Message = GetString(data, "message");
            s.PresentationCondition = ParseEnum<SurveyShowCondition>(GetString(data, "presentationCondition")) ?? SurveyShowCondition.OnManualClose;
            s.PresentationProbability = GetDoubleNullable(data, "presentationProbability") ?? 0;
            s.IncludeOtherOption = GetBool(data, "includeOtherOption");
            s.IncludeCloseOption = GetBool(data, "includeCloseOption");
            var optsList = GetList(data, "options");
            if (optsList != null)
            {
                s.Options = new List<SurveyOption>();
                foreach (var item in optsList)
                {
                    if (item is Dictionary<string, object> oDict)
                    {
                        s.Options.Add(new SurveyOption
                        {
                            Id = GetString(oDict, "id"),
                            Text = GetString(oDict, "text")
                        });
                    }
                }
            }
            return s;
        }

        private static Experiment DeserializeExperiment(Dictionary<string, object> data)
        {
            if (data == null) return null;

            var experiment = new Experiment();
            experiment.Id = GetString(data, "id");
            experiment.GroupId = GetString(data, "groupId");

            var variantDict = GetDict(data, "variant");
            if (variantDict != null)
            {
                experiment.Variant = new Variant();
                experiment.Variant.Id = GetString(variantDict, "id");
                experiment.Variant.PaywallId = GetString(variantDict, "paywallId");
                var typeStr = GetString(variantDict, "type");
                experiment.Variant.Type = typeStr?.ToLowerInvariant() == "holdout"
                    ? VariantType.Holdout
                    : VariantType.Treatment;
            }

            return experiment;
        }

        private static CustomerInfo DeserializeCustomerInfo(Dictionary<string, object> data)
        {
            if (data == null) return null;

            var info = new CustomerInfo();
            info.UserId = GetString(data, "userId");

            var entitlementsList = GetList(data, "entitlements");
            if (entitlementsList != null)
            {
                info.Entitlements = new List<Entitlement>();
                foreach (var item in entitlementsList)
                {
                    if (item is Dictionary<string, object> entDict)
                        info.Entitlements.Add(DeserializeEntitlement(entDict));
                }
            }

            return info;
        }

        private static SuperwallEventInfo DeserializeSuperwallEventInfo(Dictionary<string, object> data)
        {
            if (data == null) return null;

            var info = new SuperwallEventInfo();

            var eventTypeStr = GetString(data, "eventType");
            if (eventTypeStr != null)
            {
                // Try to parse the event type string to the enum
                var parsed = ParseEnum<EventType>(eventTypeStr);
                if (parsed.HasValue)
                    info.EventType = parsed.Value;
            }

            var paramsDict = GetDict(data, "params");
            info.Params = paramsDict;

            return info;
        }

        private static RedemptionResult DeserializeRedemptionResult(Dictionary<string, object> data)
        {
            if (data == null) return null;

            var type = GetString(data, "type");
            var code = GetString(data, "code");

            switch (type?.ToLowerInvariant())
            {
                case "success":
                    var redemptionInfoDict = GetDict(data, "redemptionInfo");
                    var redemptionInfo = DeserializeRedemptionInfo(redemptionInfoDict);
                    return RedemptionResult.Success(code, redemptionInfo);

                case "error":
                    var errorMsg = GetString(data, "error");
                    var errorInfo = new ErrorInfo { Message = errorMsg };
                    return RedemptionResult.Error(code, errorInfo);

                case "expiredcode":
                    var resent = GetBool(data, "resent");
                    var obfuscatedEmail = GetString(data, "obfuscatedEmail");
                    var expiredCodeInfo = new ExpiredCodeInfo { Resent = resent, ObfuscatedEmail = obfuscatedEmail };
                    return RedemptionResult.ExpiredCode(code, expiredCodeInfo);

                case "invalidcode":
                    return RedemptionResult.InvalidCode(code);

                case "expiredsubscription":
                    var expSubInfoDict = GetDict(data, "redemptionInfo");
                    var expSubInfo = DeserializeRedemptionInfo(expSubInfoDict);
                    return RedemptionResult.ExpiredSubscription(code, expSubInfo);

                default:
                    return null;
            }
        }

        private static RedemptionInfo DeserializeRedemptionInfo(Dictionary<string, object> data)
        {
            if (data == null) return null;

            var info = new RedemptionInfo();

            var entitlementsList = GetList(data, "entitlements");
            if (entitlementsList != null)
            {
                info.Entitlements = new List<Entitlement>();
                foreach (var item in entitlementsList)
                {
                    if (item is Dictionary<string, object> entDict)
                        info.Entitlements.Add(DeserializeEntitlement(entDict));
                }
            }

            return info;
        }

        private static PaywallResult DeserializePaywallResult(Dictionary<string, object> data)
        {
            if (data == null) return null;

            var type = GetString(data, "type");
            switch (type?.ToLowerInvariant())
            {
                case "purchased":
                    var productId = GetString(data, "productId");
                    return PaywallResult.Purchased(productId);
                case "declined":
                    return PaywallResult.Declined();
                case "restored":
                    return PaywallResult.Restored();
                default:
                    return null;
            }
        }

        private static PaywallSkippedReason DeserializePaywallSkippedReason(string reason)
        {
            switch (reason?.ToLowerInvariant())
            {
                case "holdout":
                    return PaywallSkippedReason.Holdout;
                case "noaudiencematch":
                    return PaywallSkippedReason.NoAudienceMatch;
                case "placementnotfound":
                    return PaywallSkippedReason.PlacementNotFound;
                default:
                    return PaywallSkippedReason.NoAudienceMatch;
            }
        }

        private static CustomCallback DeserializeCustomCallback(Dictionary<string, object> data)
        {
            if (data == null) return null;

            var callback = new CustomCallback();
            callback.Name = GetString(data, "name");
            callback.Variables = GetDict(data, "variables");
            return callback;
        }

        private static Dictionary<string, object> SerializePurchaseResult(PurchaseResult result)
        {
            var dict = new Dictionary<string, object>();
            switch (result.Type)
            {
                case PurchaseResult.ResultType.Cancelled:
                    dict["type"] = "cancelled";
                    break;
                case PurchaseResult.ResultType.Purchased:
                    dict["type"] = "purchased";
                    break;
                case PurchaseResult.ResultType.Pending:
                    dict["type"] = "pending";
                    break;
                case PurchaseResult.ResultType.Failed:
                    dict["type"] = "failed";
                    if (result is PurchaseResult.FailedResult failedResult)
                        dict["error"] = failedResult.Error ?? "";
                    break;
            }
            return dict;
        }

        private static Dictionary<string, object> SerializeRestorationResult(RestorationResult result)
        {
            var dict = new Dictionary<string, object>();
            switch (result.Type)
            {
                case RestorationResult.ResultType.Restored:
                    dict["type"] = "restored";
                    break;
                case RestorationResult.ResultType.Failed:
                    dict["type"] = "failed";
                    if (result is RestorationResult.FailedResult failedResult)
                        dict["error"] = failedResult.Error ?? "";
                    break;
            }
            return dict;
        }

        private static Dictionary<string, object> SerializeCustomCallbackResult(CustomCallbackResult result)
        {
            if (result == null) return new Dictionary<string, object> { { "status", "failure" } };

            var dict = new Dictionary<string, object>();
            dict["status"] = result.Status == CustomCallbackResultStatus.Success ? "success" : "failure";
            if (result.Data != null)
                dict["data"] = result.Data;
            return dict;
        }

        private static void SendResponseToNative(string callbackId, Dictionary<string, object> resultDict)
        {
            if (string.IsNullOrEmpty(callbackId)) return;
            var json = Json.Serialize(resultDict);
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_RespondToPurchaseController(callbackId, json);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.RespondToPurchaseController(callbackId, json);
#else
            Debug.Log($"[Superwall] RespondToPurchaseController(callbackId={callbackId}, result={json})");
#endif
        }

        private static void SendRestoreResponseToNative(string callbackId, Dictionary<string, object> resultDict)
        {
            if (string.IsNullOrEmpty(callbackId)) return;
            var json = Json.Serialize(resultDict);
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_RespondToRestorePurchases(callbackId, json);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.RespondToRestorePurchases(callbackId, json);
#else
            Debug.Log($"[Superwall] RespondToRestorePurchases(callbackId={callbackId}, result={json})");
#endif
        }

        #endregion

        #region Delegate Handlers

        private void HandleSubscriptionStatusDidChange(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            var fromDict = GetDict(data, "from");
            var toDict = GetDict(data, "to");
            SubscriptionStatus from = DeserializeSubscriptionStatus(fromDict);
            SubscriptionStatus to = DeserializeSubscriptionStatus(toDict);
            Delegate.SubscriptionStatusDidChange(from, to);
        }

        private void HandleSuperwallEvent(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            SuperwallEventInfo eventInfo = DeserializeSuperwallEventInfo(data);
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

            PaywallInfo paywallInfo = DeserializePaywallInfo(data);
            Delegate.WillDismissPaywall(paywallInfo);
        }

        private void HandleWillPresentPaywall(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            PaywallInfo paywallInfo = DeserializePaywallInfo(data);
            Delegate.WillPresentPaywall(paywallInfo);
        }

        private void HandleDidDismissPaywall(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            PaywallInfo paywallInfo = DeserializePaywallInfo(data);
            Delegate.DidDismissPaywall(paywallInfo);
        }

        private void HandleDidPresentPaywall(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            PaywallInfo paywallInfo = DeserializePaywallInfo(data);
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

            RedemptionResult result = DeserializeRedemptionResult(data);
            Delegate.DidRedeemLink(result);
        }

        private void HandleSuperwallDeepLink(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            var fullURL = GetString(data, "fullURL");

            List<string> pathComponents = null;
            var pathList = GetList(data, "pathComponents");
            if (pathList != null)
                pathComponents = pathList.Select(p => p?.ToString()).Where(p => p != null).ToList();

            Dictionary<string, string> queryParameters = null;
            var queryDict = GetDict(data, "queryParameters");
            if (queryDict != null)
                queryParameters = queryDict.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString());

            Delegate.HandleSuperwallDeepLink(fullURL, pathComponents, queryParameters);
        }

        private void HandleCustomerInfoDidChange(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

            var fromDict = GetDict(data, "from");
            var toDict = GetDict(data, "to");
            CustomerInfo from = DeserializeCustomerInfo(fromDict);
            CustomerInfo to = DeserializeCustomerInfo(toDict);
            Delegate.CustomerInfoDidChange(from, to);
        }

        private void HandleUserAttributesDidChange(Dictionary<string, object> data)
        {
            if (Delegate == null) return;

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

            var productId = GetString(data, "productId");
            var callbackId = GetString(data, "callbackId");

            PurchaseController.PurchaseFromAppStore(productId, (result) =>
            {
                var resultDict = SerializePurchaseResult(result);
                SendResponseToNative(callbackId, resultDict);
            });
        }

        private void HandlePurchaseFromGooglePlay(Dictionary<string, object> data)
        {
            if (PurchaseController == null) return;

            var productId = GetString(data, "productId");
            var basePlanId = GetString(data, "basePlanId");
            var offerId = GetString(data, "offerId");
            var callbackId = GetString(data, "callbackId");

            PurchaseController.PurchaseFromGooglePlay(productId, basePlanId, offerId, (result) =>
            {
                var resultDict = SerializePurchaseResult(result);
                SendResponseToNative(callbackId, resultDict);
            });
        }

        private void HandleRestorePurchases(Dictionary<string, object> data)
        {
            if (PurchaseController == null) return;

            var callbackId = GetString(data, "callbackId");

            PurchaseController.RestorePurchases((result) =>
            {
                var resultDict = SerializeRestorationResult(result);
                SendRestoreResponseToNative(callbackId, resultDict);
            });
        }

        #endregion

        #region Presentation Handler Methods

        private void HandleOnPresent(Dictionary<string, object> data)
        {
            var handlerId = GetString(data, "handlerId");
            if (handlerId == null || !_presentationHandlers.ContainsKey(handlerId)) return;

            var handler = _presentationHandlers[handlerId];
            if (handler.OnPresent == null) return;

            var paywallInfoDict = GetDict(data, "paywallInfo");
            PaywallInfo paywallInfo = DeserializePaywallInfo(paywallInfoDict);
            handler.OnPresent(paywallInfo);
        }

        private void HandleOnDismiss(Dictionary<string, object> data)
        {
            var handlerId = GetString(data, "handlerId");
            if (handlerId == null || !_presentationHandlers.ContainsKey(handlerId)) return;

            var handler = _presentationHandlers[handlerId];
            if (handler.OnDismiss == null) return;

            var paywallInfoDict = GetDict(data, "paywallInfo");
            PaywallInfo paywallInfo = DeserializePaywallInfo(paywallInfoDict);

            var resultDict = GetDict(data, "result");
            PaywallResult paywallResult = DeserializePaywallResult(resultDict);

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
            var handlerId = GetString(data, "handlerId");
            if (handlerId == null || !_presentationHandlers.ContainsKey(handlerId)) return;

            var handler = _presentationHandlers[handlerId];
            if (handler.OnSkip == null) return;

            var reasonStr = GetString(data, "reason");
            PaywallSkippedReason reason = DeserializePaywallSkippedReason(reasonStr);
            handler.OnSkip(reason);
        }

        private void HandleOnCustomCallback(Dictionary<string, object> data)
        {
            var handlerId = GetString(data, "handlerId");
            if (handlerId == null || !_presentationHandlers.ContainsKey(handlerId)) return;

            var handler = _presentationHandlers[handlerId];
            if (handler.OnCustomCallback == null) return;

            var callbackDict = GetDict(data, "customCallback");
            CustomCallback customCallback = DeserializeCustomCallback(callbackDict);
            CustomCallbackResult result = handler.OnCustomCallback(customCallback);

            // Serialize result and send back to native via callbackId
            var callbackId = GetString(data, "callbackId");
            if (!string.IsNullOrEmpty(callbackId))
            {
                var resultDict = SerializeCustomCallbackResult(result);
                var json = Json.Serialize(resultDict);
                // Send via async response mechanism
                var responsePayload = new Dictionary<string, object>
                {
                    { "callbackId", callbackId },
                    { "result", resultDict }
                };
                var responseJson = Json.Serialize(new Dictionary<string, object>
                {
                    { "method", "asyncResponse" },
                    { "data", responsePayload }
                });
                // Use the pending async callback if registered
                if (_pendingAsyncCallbacks.ContainsKey(callbackId))
                {
                    var callback = _pendingAsyncCallbacks[callbackId];
                    _pendingAsyncCallbacks.Remove(callbackId);
                    callback?.Invoke(Json.Serialize(resultDict));
                }
            }
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
