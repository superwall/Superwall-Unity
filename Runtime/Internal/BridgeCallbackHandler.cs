using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        // The delegate and onDismiss of each Customer Center presentation, until native reports it dismissed.
        private readonly Dictionary<string, (ICustomerCenterDelegate customerCenterDelegate, Action onDismiss)> _customerCenters =
            new Dictionary<string, (ICustomerCenterDelegate, Action)>();

        // Purchase/restore callbackIds native stopped waiting for. Bounded; completions may
        // arrive off the main thread, so access is locked.
        private const int MaxAbandonedPurchaseCallbacks = 32;
        private readonly HashSet<string> _abandonedPurchaseCallbacks = new HashSet<string>();
        private readonly Queue<string> _abandonedPurchaseCallbackOrder = new Queue<string>();

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

        public void RegisterCustomerCenter(string presentationId, ICustomerCenterDelegate customerCenterDelegate, Action onDismiss)
        {
            _customerCenters[presentationId] = (customerCenterDelegate, onDismiss);
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
                case "purchaseCustomProduct":
                    HandlePurchaseCustomProduct(data);
                    break;
                case "restorePurchases":
                    HandleRestorePurchases(data);
                    break;
                case "purchaseCancelled":
                case "restoreCancelled":
                    HandlePurchaseRequestCancelled(data);
                    break;

                // --- Presentation handler methods ---
                case "onPresent":
                    HandleOnPresent(data);
                    break;
                case "onWillDismiss":
                    HandleOnWillDismiss(data);
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

                // --- Customer Center ---
                case "customerCenterShouldRestorePurchases":
                    HandleCustomerCenterShouldRestorePurchases(data);
                    break;
                case "customerCenterDidSelectAction":
                    CustomerCenterDelegateFor(data)?.DidSelectAction(
                        DeserializeCustomerCenterAction(GetDict(data, "action")), GetString(data, "pathId"),
                        DeserializeCustomerCenterPurchase(GetDict(data, "purchase")));
                    break;
                case "customerCenterDidCompleteSurvey":
                    CustomerCenterDelegateFor(data)?.DidCompleteSurvey(GetString(data, "surveyId"), GetString(data, "optionId"),
                        DeserializeCustomerCenterAction(GetDict(data, "action")), GetString(data, "pathId"));
                    break;
                case "customerCenterDidCompleteRefundRequest":
                    CustomerCenterDelegateFor(data)?.DidCompleteRefundRequest(GetString(data, "productId"),
                        ParseEnum<CustomerCenterRefundStatus>(GetString(data, "status")) ?? CustomerCenterRefundStatus.Error);
                    break;
                case "customerCenterDidDismiss":
                    CustomerCenterDelegateFor(data)?.DidDismiss();
                    break;
                case "customerCenterOnDismiss":
                    HandleCustomerCenterOnDismiss(data);
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

        /// <summary>
        /// The two native bridges do not agree on the shape of an event name: iOS sends the Swift case
        /// name (camelCase) or, on newer SuperwallKit, the snake_case placement name; Android sends
        /// <c>SuperwallEvent.rawName</c>, which is snake_case (<c>transaction_complete</c>,
        /// <c>freeTrial_start</c>). Stripping underscores makes all of them match the PascalCase
        /// <see cref="EventType"/> members under a case-insensitive parse. Two placement names differ from
        /// the member that was chosen for them and are mapped explicitly.
        /// </summary>
        internal static EventType? ParseEventType(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            var parsed = ParseEnum<EventType>(value) ?? ParseEnum<EventType>(value.Replace("_", string.Empty));
            if (parsed.HasValue) return parsed;

            switch (value.Replace("_", string.Empty).ToLowerInvariant())
            {
                case "deeplinkopen": return EventType.DeepLink;
                case "paywallwebviewloadprocessterminated": return EventType.PaywallWebviewProcessTerminated;
                default: return null;
            }
        }

        internal static SubscriptionStatus DeserializeSubscriptionStatus(Dictionary<string, object> data)
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

            if (data.ContainsKey("isRevoked") && data["isRevoked"] != null)
                entitlement.IsRevoked = GetBool(data, "isRevoked");
            entitlement.RenewsAt = data.ContainsKey("renewsAt") && data["renewsAt"] != null ? (long?)GetLong(data, "renewsAt") : null;
            if (data.ContainsKey("isInGracePeriod") && data["isInGracePeriod"] != null)
                entitlement.IsInGracePeriod = GetBool(data, "isInGracePeriod");

            return entitlement;
        }

        // Also used by Superwall.LatestPaywallInfo, which had a slimmer parser of its own.
        internal static PaywallInfo DeserializePaywallInfo(Dictionary<string, object> data)
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

            info.CacheKey = GetString(data, "cacheKey");
            info.BuildId = GetString(data, "buildId");
            info.ShimmerLoadStartTime = GetString(data, "shimmerLoadStartTime");
            info.ShimmerLoadCompleteTime = GetString(data, "shimmerLoadCompleteTime");
            if (data.ContainsKey("isScrollEnabled") && data["isScrollEnabled"] != null)
                info.IsScrollEnabled = GetBool(data, "isScrollEnabled");
            info.PresentationId = GetString(data, "presentationId");
            if (data.ContainsKey("isLocal") && data["isLocal"] != null)
                info.IsLocal = GetBool(data, "isLocal");

            return info;
        }

        private static Product DeserializeProduct(Dictionary<string, object> data)
        {
            if (data == null) return null;
            var p = new Product();
            p.Id = GetString(data, "id");
            p.Name = GetString(data, "name");
            p.Store = ParseEnum<ProductStore>(GetString(data, "store"));
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

        // Also used by Superwall.GetCustomerInfo. Dates are epoch milliseconds.
        internal static CustomerInfo DeserializeCustomerInfo(Dictionary<string, object> data)
        {
            if (data == null) return null;

            var info = new CustomerInfo();
            info.UserId = GetString(data, "userId");
            info.ActiveSubscriptionProductIds = GetList(data, "activeSubscriptionProductIds")?
                .Select(p => p?.ToString()).Where(p => p != null).ToList();

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

            var subscriptionsList = GetList(data, "subscriptions");
            if (subscriptionsList != null)
            {
                info.Subscriptions = new List<SubscriptionTransaction>();
                foreach (var item in subscriptionsList)
                {
                    if (item is Dictionary<string, object> t) info.Subscriptions.Add(DeserializeSubscriptionTransaction(t));
                }
            }

            var nonSubscriptionsList = GetList(data, "nonSubscriptions");
            if (nonSubscriptionsList != null)
            {
                info.NonSubscriptions = new List<NonSubscriptionTransaction>();
                foreach (var item in nonSubscriptionsList)
                {
                    if (item is Dictionary<string, object> t) info.NonSubscriptions.Add(DeserializeNonSubscriptionTransaction(t));
                }
            }

            return info;
        }

        private static SubscriptionTransaction DeserializeSubscriptionTransaction(Dictionary<string, object> t)
        {
            if (t == null) return null;
            return new SubscriptionTransaction
            {
                TransactionId = GetString(t, "transactionId"),
                ProductId = GetString(t, "productId"),
                PurchaseDate = GetLong(t, "purchaseDate"),
                WillRenew = GetBool(t, "willRenew"),
                IsRevoked = GetBool(t, "isRevoked"),
                IsInGracePeriod = GetBool(t, "isInGracePeriod"),
                IsInBillingRetryPeriod = GetBool(t, "isInBillingRetryPeriod"),
                IsActive = GetBool(t, "isActive"),
                ExpirationDate = t.ContainsKey("expirationDate") && t["expirationDate"] != null ? (long?)GetLong(t, "expirationDate") : null,
                OfferType = ParseEnum<LatestSubscriptionOfferType>(GetString(t, "offerType")),
                SubscriptionGroupId = GetString(t, "subscriptionGroupId"),
                Store = ParseEnum<ProductStore>(GetString(t, "store")),
            };
        }

        private static NonSubscriptionTransaction DeserializeNonSubscriptionTransaction(Dictionary<string, object> t)
        {
            if (t == null) return null;
            return new NonSubscriptionTransaction
            {
                TransactionId = GetString(t, "transactionId"),
                ProductId = GetString(t, "productId"),
                PurchaseDate = GetLong(t, "purchaseDate"),
                IsConsumable = GetBool(t, "isConsumable"),
                IsRevoked = GetBool(t, "isRevoked"),
                Store = ParseEnum<ProductStore>(GetString(t, "store")),
            };
        }

        // Both bridges send the action as {type, url?, identifier?} with camelCase type names.
        internal static CustomerCenterAction DeserializeCustomerCenterAction(Dictionary<string, object> data)
        {
            if (data == null) return null;
            return new CustomerCenterAction
            {
                Type = ParseEnum<CustomerCenterActionType>(GetString(data, "type")) ?? CustomerCenterActionType.Custom,
                Url = GetString(data, "url"),
                Identifier = GetString(data, "identifier"),
            };
        }

        private static CustomerCenterPurchase DeserializeCustomerCenterPurchase(Dictionary<string, object> data)
        {
            if (data == null) return null;
            var purchase = new CustomerCenterPurchase
            {
                ProductId = GetString(data, "productId"),
                Store = ParseEnum<ProductStore>(GetString(data, "store")),
                Entitlements = new List<Entitlement>(),
                Subscription = DeserializeSubscriptionTransaction(GetDict(data, "subscription")),
                NonSubscription = DeserializeNonSubscriptionTransaction(GetDict(data, "nonSubscription")),
            };
            var entitlements = GetList(data, "entitlements");
            if (entitlements != null)
            {
                foreach (var item in entitlements)
                {
                    if (item is Dictionary<string, object> e) purchase.Entitlements.Add(DeserializeEntitlement(e));
                }
            }
            return purchase;
        }

        private static SuperwallEventInfo DeserializeSuperwallEventInfo(Dictionary<string, object> data)
        {
            if (data == null) return null;

            var info = new SuperwallEventInfo();

            var eventTypeStr = GetString(data, "eventType");
            if (eventTypeStr != null)
            {
                var parsed = ParseEventType(eventTypeStr);
                if (parsed.HasValue)
                    info.EventType = parsed.Value;
                else
                    Debug.LogWarning($"[Superwall] Unrecognised eventType '{eventTypeStr}'; leaving EventType at its default. Handlers switching on it will not run for this event.");
            }

            var paramsDict = GetDict(data, "params");
            info.Params = paramsDict;

            // Populated by the native bridges only for the events that carry them. Absent keys leave the
            // fields null.
            info.PaywallInfo = DeserializePaywallInfo(GetDict(data, "paywallInfo"));

            var productDict = GetDict(data, "product");
            if (productDict != null)
                info.Product = Superwall.DeserializeStoreProduct(productDict);

            var transactionDict = GetDict(data, "transaction");
            if (transactionDict != null)
                info.Transaction = DeserializeStoreTransaction(transactionDict);

            info.Error = GetString(data, "error");
            info.RestoreType = GetString(data, "restoreType");

            info.PlacementName = GetString(data, "placementName");
            info.DeviceAttributes = GetDict(data, "deviceAttributes");
            info.DeepLinkUrl = GetString(data, "deepLinkUrl");
            info.Result = DeserializeTriggerResult(GetDict(data, "result"));
            info.TriggeredPlacementName = GetString(data, "triggeredPlacementName");
            info.Attempt = GetIntNullable(data, "attempt");
            info.Name = GetString(data, "name");
            var surveyDict = GetDict(data, "survey");
            if (surveyDict != null)
                info.Survey = DeserializeSurvey(surveyDict);
            var optionDict = GetDict(data, "selectedOption");
            if (optionDict != null)
                info.SelectedOption = new SurveyOption { Id = GetString(optionDict, "id"), Text = GetString(optionDict, "text") };
            info.CustomResponse = GetString(data, "customResponse");
            info.Status = ParseEnum<PaywallPresentationRequestStatusType>(GetString(data, "status"));
            info.UserAttributes = GetDict(data, "userAttributes");
            info.Token = GetString(data, "token");
            info.UserEnrichment = GetDict(data, "userEnrichment");
            info.DeviceEnrichment = GetDict(data, "deviceEnrichment");
            info.Message = GetString(data, "message");
            info.IntegrationAttributes = GetDict(data, "integrationAttributes");
            info.ReviewRequestedCount = GetIntNullable(data, "reviewRequestedCount");
            var missingList = GetList(data, "missingProductIdentifiers");
            if (missingList != null)
                info.MissingProductIdentifiers = missingList.Select(p => p?.ToString()).Where(p => p != null).ToList();
            info.PermissionName = GetString(data, "permissionName");
            info.PaywallIdentifier = GetString(data, "paywallIdentifier");
            info.PaywallCount = GetIntNullable(data, "paywallCount");
            info.TransactionType = ParseEnum<TransactionType>(GetString(data, "transactionType"));
            info.CustomerCenterAction = DeserializeCustomerCenterAction(GetDict(data, "customerCenterAction"));
            info.CustomerCenterPathId = GetString(data, "customerCenterPathId");
            info.CustomerCenterProductId = GetString(data, "customerCenterProductId");
            info.CustomerCenterSurveyId = GetString(data, "customerCenterSurveyId");
            info.CustomerCenterSurveyOptionId = GetString(data, "customerCenterSurveyOptionId");
            info.CustomerCenterRefundStatus = ParseEnum<CustomerCenterRefundStatus>(GetString(data, "customerCenterRefundStatus"));
            info.CustomerCenterScreen = ParseEnum<CustomerCenterScreenType>(GetString(data, "customerCenterScreen"));
            info.CustomerCenterPresentationStyle = ParseEnum<CustomerCenterPresentationStyle>(GetString(data, "customerCenterPresentationStyle"));

            return info;
        }

        private static TriggerResult DeserializeTriggerResult(Dictionary<string, object> data)
        {
            if (data == null) return null;
            switch (GetString(data, "type"))
            {
                case "placementNotFound": return TriggerResult.PlacementNotFound();
                case "noAudienceMatch": return TriggerResult.NoAudienceMatch();
                case "paywall": return TriggerResult.Paywall(DeserializeExperiment(GetDict(data, "experiment")));
                case "holdout": return TriggerResult.Holdout(DeserializeExperiment(GetDict(data, "experiment")));
                case "error": return TriggerResult.Error(GetString(data, "error"));
                default: return null;
            }
        }

        private static StoreTransaction DeserializeStoreTransaction(Dictionary<string, object> data)
        {
            if (data == null) return null;

            var transaction = new StoreTransaction();
            transaction.ConfigRequestId = GetString(data, "configRequestId");
            transaction.AppSessionId = GetString(data, "appSessionId");
            transaction.TransactionDate = GetString(data, "transactionDate");
            transaction.OriginalTransactionIdentifier = GetString(data, "originalTransactionIdentifier");
            transaction.StoreTransactionId = GetString(data, "storeTransactionId");
            transaction.OriginalTransactionDate = GetString(data, "originalTransactionDate");
            transaction.WebOrderLineItemID = GetString(data, "webOrderLineItemID");
            transaction.AppBundleId = GetString(data, "appBundleId");
            transaction.SubscriptionGroupId = GetString(data, "subscriptionGroupId");
            if (data.ContainsKey("isUpgraded") && data["isUpgraded"] != null)
                transaction.IsUpgraded = GetBool(data, "isUpgraded");
            transaction.ExpirationDate = GetString(data, "expirationDate");
            transaction.OfferId = GetString(data, "offerId");
            transaction.RevocationDate = GetString(data, "revocationDate");
            transaction.State = ParseEnum<StoreTransactionState>(GetString(data, "state"));
            var paymentDict = GetDict(data, "payment");
            if (paymentDict != null)
            {
                transaction.Payment = new StorePayment
                {
                    ProductIdentifier = GetString(paymentDict, "productIdentifier"),
                    Quantity = GetIntNullable(paymentDict, "quantity") ?? 0,
                    DiscountIdentifier = GetString(paymentDict, "discountIdentifier"),
                };
            }
            transaction.AppAccountToken = GetString(data, "appAccountToken");
            transaction.PurchaseToken = GetString(data, "purchaseToken");
            transaction.Signature = GetString(data, "signature");
            return transaction;
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

            var ownershipDict = GetDict(data, "ownership");
            switch (GetString(ownershipDict, "type"))
            {
                case "appUser":
                    info.Ownership = Ownership.AppUser(GetString(ownershipDict, "appUserId"));
                    break;
                case "device":
                    info.Ownership = Ownership.Device(GetString(ownershipDict, "deviceId"));
                    break;
            }

            var purchaserDict = GetDict(data, "purchaserInfo");
            if (purchaserDict != null)
            {
                info.PurchaserInfo = new PurchaserInfo
                {
                    AppUserId = GetString(purchaserDict, "appUserId"),
                    Email = GetString(purchaserDict, "email"),
                    StoreIdentifiers = DeserializeStoreIdentifiers(GetDict(purchaserDict, "storeIdentifiers")),
                };
            }

            var paywallDict = GetDict(data, "paywallInfo");
            if (paywallDict != null)
            {
                info.PaywallInfo = new RedemptionPaywallInfo
                {
                    Identifier = GetString(paywallDict, "identifier"),
                    PlacementName = GetString(paywallDict, "placementName"),
                    PlacementParams = GetDict(paywallDict, "placementParams"),
                    VariantId = GetString(paywallDict, "variantId"),
                    ExperimentId = GetString(paywallDict, "experimentId"),
                    ProductIdentifier = GetString(paywallDict, "productIdentifier"),
                };
            }

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

            info.UserAttributes = GetDict(data, "userAttributes");

            return info;
        }

        private static StoreIdentifiers DeserializeStoreIdentifiers(Dictionary<string, object> data)
        {
            if (data == null) return null;
            var subscriptionIds = GetList(data, "subscriptionIds")?.Select(p => p?.ToString()).Where(p => p != null).ToList();
            switch (GetString(data, "type"))
            {
                case "stripe": return StoreIdentifiers.Stripe(GetString(data, "customerId"), subscriptionIds);
                case "paddle": return StoreIdentifiers.Paddle(GetString(data, "customerId"), subscriptionIds);
                case "unknown": return StoreIdentifiers.Unknown(GetString(data, "store"), GetDict(data, "additionalInfo"));
                default: return null;
            }
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
                case "userissubscribed":
                    return PaywallSkippedReason.UserIsSubscribed;
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

        private static void SendCustomCallbackResponseToNative(string callbackId, CustomCallbackResult result)
        {
            if (string.IsNullOrEmpty(callbackId)) return;
            try
            {
                var json = Json.Serialize(SerializeCustomCallbackResult(result));
#if UNITY_IOS && !UNITY_EDITOR
                SuperwallBridgeiOS._SuperwallBridge_RespondToCustomCallback(callbackId, json);
#elif UNITY_ANDROID && !UNITY_EDITOR
                SuperwallBridgeAndroid.RespondToCustomCallback(callbackId, json);
#else
                Debug.Log($"[Superwall] RespondToCustomCallback(callbackId={callbackId}, result={json})");
#endif
            }
            catch (Exception e)
            {
                Debug.LogError($"[Superwall] Could not reply to custom callback {callbackId}: {e.Message}\n{e.StackTrace}");
            }
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
            if (!IsPurchaseRequestPending(callbackId)) return;

            var reply = CreatePurchaseReply(callbackId);
            try
            {
                PurchaseController.PurchaseFromAppStore(productId, reply);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Superwall] PurchaseFromAppStore threw: {e.Message}\n{e.StackTrace}");
                reply(PurchaseResult.Failed(e.Message));
            }
        }

        private void HandlePurchaseFromGooglePlay(Dictionary<string, object> data)
        {
            if (PurchaseController == null) return;

            var productId = GetString(data, "productId");
            var basePlanId = GetString(data, "basePlanId");
            var offerId = GetString(data, "offerId");
            var callbackId = GetString(data, "callbackId");
            if (!IsPurchaseRequestPending(callbackId)) return;

            var replacement = DeserializeReplacement(GetDict(data, "replacement"));

            var reply = CreatePurchaseReply(callbackId);
            try
            {
                PurchaseController.PurchaseFromGooglePlay(productId, basePlanId, offerId, replacement, reply);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Superwall] PurchaseFromGooglePlay threw: {e.Message}\n{e.StackTrace}");
                reply(PurchaseResult.Failed(e.Message));
            }
        }

        private void HandlePurchaseCustomProduct(Dictionary<string, object> data)
        {
            if (PurchaseController == null) return;

            var callbackId = GetString(data, "callbackId");
            if (!IsPurchaseRequestPending(callbackId)) return;

            var purchase = new CustomProductPurchase(
                GetString(data, "productId"),
                GetString(data, "customTransactionId"),
                DeserializeReplacement(GetDict(data, "replacement")));

            var reply = CreatePurchaseReply(callbackId);
            try
            {
                PurchaseController.PurchaseCustomProduct(purchase, reply);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Superwall] PurchaseCustomProduct threw: {e.Message}\n{e.StackTrace}");
                reply(PurchaseResult.Failed(e.Message));
            }
        }

        private static ProductReplacement DeserializeReplacement(Dictionary<string, object> data)
        {
            if (data == null) return null;
            return new ProductReplacement(
                ProductReplacement.ParseMode(GetString(data, "mode")),
                GetString(data, "oldProductId"),
                GetString(data, "oldPurchaseToken"));
        }

        private void HandleRestorePurchases(Dictionary<string, object> data)
        {
            if (PurchaseController == null) return;

            var callbackId = GetString(data, "callbackId");
            if (!IsPurchaseRequestPending(callbackId)) return;

            var replied = false;
            Action<RestorationResult> reply = (result) =>
            {
                if (replied) return;
                replied = true;
                if (IsPurchaseCallbackAbandoned(callbackId))
                {
                    Debug.LogWarning($"[Superwall] Ignoring late restore result for cancelled request {callbackId}");
                    return;
                }
                SendRestoreResponseToNative(callbackId, SerializeRestorationResult(result));
            };
            try
            {
                PurchaseController.RestorePurchases(reply);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Superwall] RestorePurchases threw: {e.Message}\n{e.StackTrace}");
                reply(RestorationResult.Failed(e.Message));
            }
        }

        // Native stopped waiting for this request (e.g. the paywall went away).
        private void HandlePurchaseRequestCancelled(Dictionary<string, object> data)
        {
            var callbackId = GetString(data, "callbackId");
            if (string.IsNullOrEmpty(callbackId)) return;

            lock (_abandonedPurchaseCallbacks)
            {
                if (!_abandonedPurchaseCallbacks.Add(callbackId)) return;
                _abandonedPurchaseCallbackOrder.Enqueue(callbackId);
                while (_abandonedPurchaseCallbackOrder.Count > MaxAbandonedPurchaseCallbacks)
                    _abandonedPurchaseCallbacks.Remove(_abandonedPurchaseCallbackOrder.Dequeue());
            }
        }

        private bool IsPurchaseCallbackAbandoned(string callbackId)
        {
            if (string.IsNullOrEmpty(callbackId)) return false;
            lock (_abandonedPurchaseCallbacks)
            {
                return _abandonedPurchaseCallbacks.Contains(callbackId);
            }
        }

        // A request can sit in Unity's message queue (e.g. while the player is paused) after
        // native has stopped waiting for it. Never start a store transaction for those.
        private bool IsPurchaseRequestPending(string callbackId)
        {
            if (IsPurchaseCallbackAbandoned(callbackId))
            {
                Debug.LogWarning($"[Superwall] Skipping purchase controller request {callbackId}: cancelled by native");
                return false;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (string.IsNullOrEmpty(callbackId)) return true;
            try
            {
                if (!SuperwallBridgeAndroid.IsPurchaseControllerRequestPending(callbackId))
                {
                    Debug.LogWarning($"[Superwall] Skipping purchase controller request {callbackId}: no longer pending on native");
                    return false;
                }
            }
            catch (Exception e)
            {
                // Can't tell; fall back to running the request.
                Debug.LogWarning($"[Superwall] Could not check purchase controller request {callbackId}: {e.Message}");
            }
#endif
            return true;
        }

        // Replies at most once, and not at all once native has abandoned the request.
        private Action<PurchaseResult> CreatePurchaseReply(string callbackId)
        {
            var replied = false;
            return (result) =>
            {
                if (replied) return;
                replied = true;
                if (IsPurchaseCallbackAbandoned(callbackId))
                {
                    Debug.LogWarning($"[Superwall] Ignoring late purchase result for cancelled request {callbackId}");
                    return;
                }
                SendResponseToNative(callbackId, SerializePurchaseResult(result));
            };
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

        private void HandleOnWillDismiss(Dictionary<string, object> data)
        {
            var handlerId = GetString(data, "handlerId");
            if (handlerId == null || !_presentationHandlers.ContainsKey(handlerId)) return;

            var handler = _presentationHandlers[handlerId];
            if (handler.OnWillDismiss == null) return;

            handler.OnWillDismiss(DeserializePaywallInfo(GetDict(data, "paywallInfo")), DeserializePaywallResult(GetDict(data, "result")));
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

        // Native holds the paywall's request until it gets a reply, so every path here must reply.
        // async void because RouteCallback can't await; nothing can escape, as RunCustomCallback
        // catches what the handler throws and SendCustomCallbackResponseToNative catches the rest.
        // Awaiting from here also resumes on Unity's main thread, wherever the handler's task
        // finished, which is where the bridge calls have to come from.
        private async void HandleOnCustomCallback(Dictionary<string, object> data)
        {
            var callbackId = GetString(data, "callbackId");
            var result = await RunCustomCallback(data);
            SendCustomCallbackResponseToNative(callbackId, result);
        }

        private async Task<CustomCallbackResult> RunCustomCallback(Dictionary<string, object> data)
        {
            var customCallback = DeserializeCustomCallback(GetDict(data, "customCallback"));
            var handlerId = GetString(data, "handlerId");
            PaywallPresentationHandler handler = null;
            if (handlerId != null) _presentationHandlers.TryGetValue(handlerId, out handler);

            if (customCallback == null || handler?.OnCustomCallback == null)
            {
                Debug.LogWarning($"[Superwall] No OnCustomCallback handler for custom callback '{customCallback?.Name}'; the paywall gets a failure.");
                return CustomCallbackResult.Failure();
            }

            try
            {
                var task = handler.OnCustomCallback(customCallback);
                var result = task != null ? await task : null;
                if (result != null) return result;
                Debug.LogWarning($"[Superwall] OnCustomCallback returned null for '{customCallback.Name}'; the paywall gets a failure.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Superwall] OnCustomCallback threw for '{customCallback.Name}': {e.Message}\n{e.StackTrace}");
            }
            return CustomCallbackResult.Failure();
        }

        #endregion

        #region Customer Center

        private ICustomerCenterDelegate CustomerCenterDelegateFor(Dictionary<string, object> data)
        {
            var presentationId = GetString(data, "presentationId");
            if (presentationId == null || !_customerCenters.TryGetValue(presentationId, out var presentation)) return null;
            return presentation.customerCenterDelegate;
        }

        // Native holds the restore until it gets a reply, so every path here replies, once.
        private void HandleCustomerCenterShouldRestorePurchases(Dictionary<string, object> data)
        {
            var callbackId = GetString(data, "callbackId");
            var replied = false;
            Action<bool> proceed = (shouldRestore) =>
            {
                if (replied) return;
                replied = true;
                SendCustomerCenterShouldRestoreToNative(callbackId, shouldRestore);
            };

            var customerCenterDelegate = CustomerCenterDelegateFor(data);
            if (customerCenterDelegate == null)
            {
                proceed(true);
                return;
            }
            try
            {
                customerCenterDelegate.ShouldRestorePurchases(proceed);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Superwall] ShouldRestorePurchases threw: {e.Message}\n{e.StackTrace}");
                proceed(true);
            }
        }

        private void HandleCustomerCenterOnDismiss(Dictionary<string, object> data)
        {
            var presentationId = GetString(data, "presentationId");
            if (presentationId == null || !_customerCenters.TryGetValue(presentationId, out var presentation)) return;
            _customerCenters.Remove(presentationId);
            presentation.onDismiss?.Invoke();
        }

        private static void SendCustomerCenterShouldRestoreToNative(string callbackId, bool proceed)
        {
            if (string.IsNullOrEmpty(callbackId)) return;
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_RespondToCustomerCenterShouldRestore(callbackId, proceed);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.RespondToCustomerCenterShouldRestore(callbackId, proceed);
#else
            Debug.Log($"[Superwall] RespondToCustomerCenterShouldRestore(callbackId={callbackId}, proceed={proceed})");
#endif
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

            // Native bridges flatten the response fields directly into `data` alongside `callbackId`.
            // Re-serialize the whole dict (sans callbackId) so the registered callback can parse it.
            string json = null;
            if (data != null)
            {
                var payload = new Dictionary<string, object>(data);
                payload.Remove("callbackId");
                json = Json.Serialize(payload);
            }
            callback?.Invoke(json);
        }

        #endregion
    }
}
