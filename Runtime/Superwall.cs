using System;
using System.Collections.Generic;
using UnityEngine;
using Superwall.Internal;

namespace Superwall
{
    public class Superwall
    {
        private static Superwall _shared;
        private ISuperwallDelegate _delegate;
        private IPurchaseController _purchaseController;

        private Superwall() { }

        public static Superwall Shared
        {
            get
            {
                if (_shared == null)
                {
                    Debug.LogWarning("[Superwall] SDK has not been configured. Call Superwall.Configure() first.");
                }
                return _shared;
            }
        }

        // ------- Configuration -------

        public static Superwall Configure(string apiKey, SuperwallOptions options = null, IPurchaseController purchaseController = null, Action<bool> completion = null)
        {
            if (_shared != null)
            {
                Debug.LogWarning("[Superwall] SDK has already been configured.");
                return _shared;
            }

            _shared = new Superwall();
            _shared._purchaseController = purchaseController;

            BridgeCallbackHandler.Initialize();
            BridgeCallbackHandler.Instance.PurchaseController = purchaseController;

            string optionsJson = options != null ? SerializeOptions(options) : null;
            bool hasPurchaseController = purchaseController != null;

            string completionCallbackId = null;
            if (completion != null)
            {
                completionCallbackId = Guid.NewGuid().ToString();
                BridgeCallbackHandler.Instance.RegisterAsyncCallback(completionCallbackId, (json) =>
                {
                    var data = Json.Deserialize(json) as Dictionary<string, object>;
                    bool success = data != null && data.ContainsKey("success") && (bool)data["success"];
                    completion(success);
                });
            }

            CallNative_Configure(apiKey, optionsJson, hasPurchaseController, completionCallbackId);
            return _shared;
        }

        public void SetDelegate(ISuperwallDelegate superwallDelegate)
        {
            _delegate = superwallDelegate;
            BridgeCallbackHandler.Instance.Delegate = superwallDelegate;
            CallNative_SetDelegate(superwallDelegate != null);
        }

        public void Reset()
        {
            CallNative_Reset();
        }

        // ------- User Identity -------

        public string UserId
        {
            get { return CallNative_GetUserId(); }
        }

        public bool IsLoggedIn
        {
            get { return CallNative_GetIsLoggedIn(); }
        }

        public bool IsInitialized
        {
            get { return CallNative_GetIsInitialized(); }
        }

        public void Identify(string userId, IdentityOptions identityOptions = null)
        {
            string optionsJson = identityOptions != null ? Json.Serialize(new Dictionary<string, object>
            {
                { "restorePaywallAssignments", identityOptions.RestorePaywallAssignments }
            }) : null;
            CallNative_Identify(userId, optionsJson);
        }

        // ------- User Attributes -------

        public void SetUserAttributes(Dictionary<string, object> attributes)
        {
            CallNative_SetUserAttributes(Json.Serialize(attributes));
        }

        public Dictionary<string, object> GetUserAttributes()
        {
            string json = CallNative_GetUserAttributes();
            if (string.IsNullOrEmpty(json)) return new Dictionary<string, object>();
            return Json.Deserialize(json) as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        // ------- Integration Attributes -------

        public void SetIntegrationAttribute(IntegrationAttribute attribute, string value = null)
        {
            CallNative_SetIntegrationAttribute(attribute.ToString(), value);
        }

        public void SetIntegrationAttributes(Dictionary<IntegrationAttribute, string> attributes)
        {
            var serializable = new Dictionary<string, object>();
            foreach (var kvp in attributes)
            {
                serializable[kvp.Key.ToString()] = kvp.Value;
            }
            CallNative_SetIntegrationAttributes(Json.Serialize(serializable));
        }

        // ------- Device Attributes -------

        public void GetDeviceAttributes(Action<Dictionary<string, object>> completion)
        {
            string callbackId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                var result = Json.Deserialize(json) as Dictionary<string, object> ?? new Dictionary<string, object>();
                completion(result);
            });
            CallNative_GetDeviceAttributes(callbackId);
        }

        // ------- Locale -------

        public string LocaleIdentifier
        {
            get { return CallNative_GetLocaleIdentifier(); }
            set { CallNative_SetLocaleIdentifier(value); }
        }

        // ------- Logging -------

        public LogLevel LogLevel
        {
            get
            {
                string level = CallNative_GetLogLevel();
                if (Enum.TryParse<LogLevel>(level, true, out var result)) return result;
                return LogLevel.Warn;
            }
            set { CallNative_SetLogLevel(value.ToString()); }
        }

        // ------- Entitlements -------

        public Entitlements Entitlements
        {
            get
            {
                string json = CallNative_GetEntitlements();
                // TODO: Deserialize JSON into Entitlements object
                return new Entitlements();
            }
        }

        public List<Entitlement> GetEntitlementsByProductIds(List<string> productIds)
        {
            string json = CallNative_GetEntitlementsByProductIds(Json.Serialize(productIds));
            // TODO: Deserialize JSON into List<Entitlement>
            return new List<Entitlement>();
        }

        // ------- Customer Info -------

        public void GetCustomerInfo(Action<CustomerInfo> completion)
        {
            string callbackId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                // TODO: Deserialize JSON into CustomerInfo
                completion(new CustomerInfo());
            });
            CallNative_GetCustomerInfo(callbackId);
        }

        // ------- Subscription Status -------

        public SubscriptionStatus SubscriptionStatus
        {
            get
            {
                string json = CallNative_GetSubscriptionStatus();
                // TODO: Deserialize JSON into SubscriptionStatus
                return SubscriptionStatus.CreateUnknown();
            }
            set
            {
                var data = new Dictionary<string, object>();
                data["type"] = value.Type.ToString();
                CallNative_SetSubscriptionStatus(Json.Serialize(data));
            }
        }

        // ------- Configuration Status -------

        public ConfigurationStatus ConfigurationStatus
        {
            get
            {
                string status = CallNative_GetConfigurationStatus();
                if (Enum.TryParse<ConfigurationStatus>(status, true, out var result)) return result;
                return ConfigurationStatus.Pending;
            }
        }

        public bool IsConfigured
        {
            get { return CallNative_GetIsConfigured(); }
        }

        // ------- Paywall Management -------

        public bool IsPaywallPresented
        {
            get { return CallNative_GetIsPaywallPresented(); }
        }

        public void PreloadAllPaywalls()
        {
            CallNative_PreloadAllPaywalls();
        }

        public void PreloadPaywallsForPlacements(List<string> placementNames)
        {
            CallNative_PreloadPaywallsForPlacements(Json.Serialize(placementNames));
        }

        public bool HandleDeepLink(string url)
        {
            return CallNative_HandleDeepLink(url);
        }

        public void TogglePaywallSpinner(bool isHidden)
        {
            CallNative_TogglePaywallSpinner(isHidden);
        }

        public PaywallInfo LatestPaywallInfo
        {
            get
            {
                string json = CallNative_GetLatestPaywallInfo();
                if (string.IsNullOrEmpty(json)) return null;
                // TODO: Deserialize JSON into PaywallInfo
                return null;
            }
        }

        public void RegisterPlacement(string placement, Dictionary<string, object> parameters = null, PaywallPresentationHandler handler = null, Action feature = null)
        {
            string paramsJson = parameters != null ? Json.Serialize(parameters) : null;

            string handlerId = null;
            if (handler != null)
            {
                handlerId = Guid.NewGuid().ToString();
                BridgeCallbackHandler.Instance.RegisterPresentationHandler(handlerId, handler);
            }

            string featureId = null;
            if (feature != null)
            {
                featureId = Guid.NewGuid().ToString();
                BridgeCallbackHandler.Instance.RegisterFeatureHandler(featureId, feature);
            }

            string callbackId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                // Registration completed
            });

            CallNative_RegisterPlacement(placement, paramsJson, handlerId, featureId, callbackId);
        }

        public void Dismiss()
        {
            CallNative_Dismiss();
        }

        // ------- Presentation Result -------

        public void GetPresentationResult(string placement, Dictionary<string, object> parameters = null, Action<PresentationResult> completion = null)
        {
            string paramsJson = parameters != null ? Json.Serialize(parameters) : null;
            string callbackId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                // TODO: Deserialize JSON into PresentationResult
                completion?.Invoke(PresentationResult.PlacementNotFound());
            });
            CallNative_GetPresentationResult(placement, paramsJson, callbackId);
        }

        // ------- Assignments -------

        public void ConfirmAllAssignments(Action<List<ConfirmedAssignment>> completion = null)
        {
            string callbackId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                // TODO: Deserialize JSON into List<ConfirmedAssignment>
                completion?.Invoke(new List<ConfirmedAssignment>());
            });
            CallNative_ConfirmAllAssignments(callbackId);
        }

        // ------- Purchases -------

        public void RestorePurchases(Action<RestorationResult> completion = null)
        {
            string callbackId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                // TODO: Deserialize JSON into RestorationResult
                completion?.Invoke(RestorationResult.Restored());
            });
            CallNative_RestorePurchases(callbackId);
        }

        // ------- Override Products -------

        public Dictionary<string, string> OverrideProductsByName
        {
            get
            {
                string json = CallNative_GetOverrideProductsByName();
                if (string.IsNullOrEmpty(json)) return null;
                var dict = Json.Deserialize(json) as Dictionary<string, object>;
                if (dict == null) return null;
                var result = new Dictionary<string, string>();
                foreach (var kvp in dict)
                {
                    result[kvp.Key] = kvp.Value?.ToString();
                }
                return result;
            }
            set
            {
                string json = value != null ? Json.Serialize(value) : null;
                CallNative_SetOverrideProductsByName(json);
            }
        }

        // ------- Consume (Android) -------

        public void Consume(string purchaseToken, Action<string> completion = null)
        {
            string callbackId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                completion?.Invoke(json);
            });
            CallNative_Consume(purchaseToken, callbackId);
        }

        // ============= Options Serialization =============

        private static string SerializeOptions(SuperwallOptions options)
        {
            var dict = new Dictionary<string, object>();

            if (options.Paywalls != null)
            {
                var paywalls = new Dictionary<string, object>();
                paywalls["isHapticFeedbackEnabled"] = options.Paywalls.IsHapticFeedbackEnabled;
                paywalls["shouldShowPurchaseFailureAlert"] = options.Paywalls.ShouldShowPurchaseFailureAlert;
                paywalls["shouldPreload"] = options.Paywalls.ShouldPreload;
                paywalls["automaticallyDismiss"] = options.Paywalls.AutomaticallyDismiss;
                paywalls["shouldShowWebRestorationAlert"] = options.Paywalls.ShouldShowWebRestorationAlert;
                paywalls["transactionBackgroundView"] = options.Paywalls.TransactionBackgroundView.ToString();
                paywalls["shouldShowWebPurchaseConfirmationAlert"] = options.Paywalls.ShouldShowWebPurchaseConfirmationAlert;

                if (options.Paywalls.RestoreFailed != null)
                {
                    paywalls["restoreFailed"] = new Dictionary<string, object>
                    {
                        { "title", options.Paywalls.RestoreFailed.Title },
                        { "message", options.Paywalls.RestoreFailed.Message },
                        { "closeButtonTitle", options.Paywalls.RestoreFailed.CloseButtonTitle }
                    };
                }

                if (options.Paywalls.OverrideProductsByName != null)
                {
                    paywalls["overrideProductsByName"] = options.Paywalls.OverrideProductsByName;
                }

                dict["paywalls"] = paywalls;
            }

            dict["networkEnvironment"] = options.NetworkEnvironment.ToString();
            dict["isExternalDataCollectionEnabled"] = options.IsExternalDataCollectionEnabled;
            if (options.LocaleIdentifier != null) dict["localeIdentifier"] = options.LocaleIdentifier;
            dict["isGameControllerEnabled"] = options.IsGameControllerEnabled;
            dict["passIdentifiersToPlayStore"] = options.PassIdentifiersToPlayStore;
            dict["testModeBehavior"] = options.TestModeBehavior.ToString();
            dict["shouldObservePurchases"] = options.ShouldObservePurchases;
            dict["shouldBypassAppTransactionCheck"] = options.ShouldBypassAppTransactionCheck;
            dict["maxConfigRetryCount"] = options.MaxConfigRetryCount;
            dict["useMockReviews"] = options.UseMockReviews;

            if (options.Logging != null)
            {
                var logging = new Dictionary<string, object>();
                logging["level"] = options.Logging.Level.ToString();
                if (options.Logging.Scopes != null)
                {
                    var scopes = new List<object>();
                    foreach (var scope in options.Logging.Scopes)
                    {
                        scopes.Add(scope.ToString());
                    }
                    logging["scopes"] = scopes;
                }
                dict["logging"] = logging;
            }

            return Json.Serialize(dict);
        }

        // ============= Platform Bridge Calls =============

        private static void CallNative_Configure(string apiKey, string optionsJson, bool hasPurchaseController, string completionCallbackId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_Configure(apiKey, optionsJson, hasPurchaseController, completionCallbackId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.Configure(apiKey, optionsJson, hasPurchaseController, completionCallbackId);
#else
            Debug.Log($"[Superwall] Configure(apiKey={apiKey}, hasPurchaseController={hasPurchaseController})");
#endif
        }

        private static void CallNative_Reset()
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_Reset();
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.Reset();
#else
            Debug.Log("[Superwall] Reset()");
#endif
        }

        private static void CallNative_SetDelegate(bool hasDelegate)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_SetDelegate(hasDelegate);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.SetDelegate(hasDelegate);
#else
            Debug.Log($"[Superwall] SetDelegate(hasDelegate={hasDelegate})");
#endif
        }

        private static void CallNative_Identify(string userId, string identityOptionsJson)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_Identify(userId, identityOptionsJson);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.Identify(userId, identityOptionsJson);
#else
            Debug.Log($"[Superwall] Identify(userId={userId})");
#endif
        }

        private static string CallNative_GetUserId()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetUserId();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetUserId();
#else
            Debug.Log("[Superwall] GetUserId()");
            return "";
#endif
        }

        private static bool CallNative_GetIsLoggedIn()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetIsLoggedIn();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetIsLoggedIn();
#else
            Debug.Log("[Superwall] GetIsLoggedIn()");
            return false;
#endif
        }

        private static bool CallNative_GetIsInitialized()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetIsInitialized();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetIsInitialized();
#else
            Debug.Log("[Superwall] GetIsInitialized()");
            return false;
#endif
        }

        private static void CallNative_SetUserAttributes(string attributesJson)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_SetUserAttributes(attributesJson);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.SetUserAttributes(attributesJson);
#else
            Debug.Log($"[Superwall] SetUserAttributes({attributesJson})");
#endif
        }

        private static string CallNative_GetUserAttributes()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetUserAttributes();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetUserAttributes();
#else
            Debug.Log("[Superwall] GetUserAttributes()");
            return "{}";
#endif
        }

        private static void CallNative_SetIntegrationAttribute(string attribute, string value)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_SetIntegrationAttribute(attribute, value);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.SetIntegrationAttribute(attribute, value);
#else
            Debug.Log($"[Superwall] SetIntegrationAttribute({attribute}, {value})");
#endif
        }

        private static void CallNative_SetIntegrationAttributes(string attributesJson)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_SetIntegrationAttributes(attributesJson);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.SetIntegrationAttributes(attributesJson);
#else
            Debug.Log($"[Superwall] SetIntegrationAttributes({attributesJson})");
#endif
        }

        private static void CallNative_GetDeviceAttributes(string callbackId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_GetDeviceAttributes(callbackId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.GetDeviceAttributes(callbackId);
#else
            Debug.Log("[Superwall] GetDeviceAttributes()");
#endif
        }

        private static string CallNative_GetLocaleIdentifier()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetLocaleIdentifier();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetLocaleIdentifier();
#else
            Debug.Log("[Superwall] GetLocaleIdentifier()");
            return null;
#endif
        }

        private static void CallNative_SetLocaleIdentifier(string localeIdentifier)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_SetLocaleIdentifier(localeIdentifier);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.SetLocaleIdentifier(localeIdentifier);
#else
            Debug.Log($"[Superwall] SetLocaleIdentifier({localeIdentifier})");
#endif
        }

        private static string CallNative_GetLogLevel()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetLogLevel();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetLogLevel();
#else
            Debug.Log("[Superwall] GetLogLevel()");
            return "warn";
#endif
        }

        private static void CallNative_SetLogLevel(string logLevel)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_SetLogLevel(logLevel);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.SetLogLevel(logLevel);
#else
            Debug.Log($"[Superwall] SetLogLevel({logLevel})");
#endif
        }

        private static string CallNative_GetEntitlements()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetEntitlements();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetEntitlements();
#else
            Debug.Log("[Superwall] GetEntitlements()");
            return "{}";
#endif
        }

        private static string CallNative_GetEntitlementsByProductIds(string productIdsJson)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetEntitlementsByProductIds(productIdsJson);
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetEntitlementsByProductIds(productIdsJson);
#else
            Debug.Log($"[Superwall] GetEntitlementsByProductIds({productIdsJson})");
            return "[]";
#endif
        }

        private static void CallNative_GetCustomerInfo(string callbackId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_GetCustomerInfo(callbackId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.GetCustomerInfo(callbackId);
#else
            Debug.Log("[Superwall] GetCustomerInfo()");
#endif
        }

        private static string CallNative_GetSubscriptionStatus()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetSubscriptionStatus();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetSubscriptionStatus();
#else
            Debug.Log("[Superwall] GetSubscriptionStatus()");
            return "{\"type\":\"unknown\"}";
#endif
        }

        private static void CallNative_SetSubscriptionStatus(string statusJson)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_SetSubscriptionStatus(statusJson);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.SetSubscriptionStatus(statusJson);
#else
            Debug.Log($"[Superwall] SetSubscriptionStatus({statusJson})");
#endif
        }

        private static string CallNative_GetConfigurationStatus()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetConfigurationStatus();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetConfigurationStatus();
#else
            Debug.Log("[Superwall] GetConfigurationStatus()");
            return "pending";
#endif
        }

        private static bool CallNative_GetIsConfigured()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetIsConfigured();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetIsConfigured();
#else
            Debug.Log("[Superwall] GetIsConfigured()");
            return false;
#endif
        }

        private static bool CallNative_GetIsPaywallPresented()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetIsPaywallPresented();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetIsPaywallPresented();
#else
            Debug.Log("[Superwall] GetIsPaywallPresented()");
            return false;
#endif
        }

        private static void CallNative_PreloadAllPaywalls()
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_PreloadAllPaywalls();
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.PreloadAllPaywalls();
#else
            Debug.Log("[Superwall] PreloadAllPaywalls()");
#endif
        }

        private static void CallNative_PreloadPaywallsForPlacements(string placementNamesJson)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_PreloadPaywallsForPlacements(placementNamesJson);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.PreloadPaywallsForPlacements(placementNamesJson);
#else
            Debug.Log($"[Superwall] PreloadPaywallsForPlacements({placementNamesJson})");
#endif
        }

        private static bool CallNative_HandleDeepLink(string url)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_HandleDeepLink(url);
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.HandleDeepLink(url);
#else
            Debug.Log($"[Superwall] HandleDeepLink({url})");
            return false;
#endif
        }

        private static void CallNative_TogglePaywallSpinner(bool isHidden)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_TogglePaywallSpinner(isHidden);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.TogglePaywallSpinner(isHidden);
#else
            Debug.Log($"[Superwall] TogglePaywallSpinner({isHidden})");
#endif
        }

        private static string CallNative_GetLatestPaywallInfo()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetLatestPaywallInfo();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetLatestPaywallInfo();
#else
            Debug.Log("[Superwall] GetLatestPaywallInfo()");
            return null;
#endif
        }

        private static void CallNative_RegisterPlacement(string placement, string paramsJson, string handlerId, string featureId, string callbackId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_RegisterPlacement(placement, paramsJson, handlerId, featureId, callbackId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.RegisterPlacement(placement, paramsJson, handlerId, featureId, callbackId);
#else
            Debug.Log($"[Superwall] RegisterPlacement({placement})");
#endif
        }

        private static void CallNative_Dismiss()
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_Dismiss();
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.Dismiss();
#else
            Debug.Log("[Superwall] Dismiss()");
#endif
        }

        private static void CallNative_GetPresentationResult(string placement, string paramsJson, string callbackId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_GetPresentationResult(placement, paramsJson, callbackId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.GetPresentationResult(placement, paramsJson, callbackId);
#else
            Debug.Log($"[Superwall] GetPresentationResult({placement})");
#endif
        }

        private static void CallNative_ConfirmAllAssignments(string callbackId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_ConfirmAllAssignments(callbackId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.ConfirmAllAssignments(callbackId);
#else
            Debug.Log("[Superwall] ConfirmAllAssignments()");
#endif
        }

        private static void CallNative_RestorePurchases(string callbackId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_RestorePurchases(callbackId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.RestorePurchases(callbackId);
#else
            Debug.Log("[Superwall] RestorePurchases()");
#endif
        }

        private static string CallNative_GetOverrideProductsByName()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetOverrideProductsByName();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetOverrideProductsByName();
#else
            Debug.Log("[Superwall] GetOverrideProductsByName()");
            return null;
#endif
        }

        private static void CallNative_SetOverrideProductsByName(string productsJson)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_SetOverrideProductsByName(productsJson);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.SetOverrideProductsByName(productsJson);
#else
            Debug.Log($"[Superwall] SetOverrideProductsByName({productsJson})");
#endif
        }

        private static void CallNative_Consume(string purchaseToken, string callbackId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_Consume(purchaseToken, callbackId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.Consume(purchaseToken, callbackId);
#else
            Debug.Log($"[Superwall] Consume({purchaseToken})");
#endif
        }
    }
}
