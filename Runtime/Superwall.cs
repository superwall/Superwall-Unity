using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Superwall.Internal;

namespace Superwall
{
    public class Superwall
    {
        /// <summary>The name the native SDKs report this wrapper under.</summary>
        internal const string PlatformWrapper = "Unity";

        /// <summary>This package's version, as in package.json. A test keeps the two in step.</summary>
        internal const string PackageVersion = "0.6.0";

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

        public static Superwall Configure(string apiKey, SuperwallOptions options = null, IPurchaseController purchaseController = null, Action<ConfigurationResult> completion = null)
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
                    if (data != null && data.ContainsKey("success") && (bool)data["success"])
                    {
                        completion(ConfigurationResult.Success());
                    }
                    else
                    {
                        string error = data != null && data.ContainsKey("error") ? data["error"] as string : "Unknown error";
                        completion(ConfigurationResult.Failed(error));
                    }
                });
            }

            CallNative_Configure(apiKey, optionsJson, hasPurchaseController, completionCallbackId);
            // Tags this app's events and device attributes as coming from the Unity SDK, as the other wrappers do.
            CallNative_SetPlatformWrapper(PlatformWrapper, PackageVersion);
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

        /// <summary>Sets one integration attribute. A null value removes it.</summary>
        public void SetIntegrationAttribute(IntegrationAttribute attribute, string value = null)
        {
            CallNative_SetIntegrationAttribute(IntegrationAttributeKey(attribute), value);
        }

        /// <summary>Sets the integration attributes passed, leaving the others as they are. A null value removes one.</summary>
        public void SetIntegrationAttributes(Dictionary<IntegrationAttribute, string> attributes)
        {
            var serializable = new Dictionary<string, object>();
            foreach (var kvp in attributes)
            {
                serializable[IntegrationAttributeKey(kvp.Key)] = kvp.Value;
            }
            CallNative_SetIntegrationAttributes(Json.Serialize(serializable));
        }

        /// <summary>
        /// The integration attributes set with <see cref="SetIntegrationAttribute"/> and
        /// <see cref="SetIntegrationAttributes"/>, keyed by the native SDKs' names (<c>adjustId</c>).
        /// </summary>
        public Dictionary<string, string> IntegrationAttributes
        {
            get
            {
                var result = new Dictionary<string, string>();
                string json = CallNative_GetIntegrationAttributes();
                if (string.IsNullOrEmpty(json)) return result;
                if (Json.Deserialize(json) is Dictionary<string, object> dict)
                {
                    foreach (var kvp in dict)
                    {
                        if (kvp.Value != null) result[kvp.Key] = kvp.Value.ToString();
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// The native SDKs name integration attributes in camelCase (<c>adjustId</c>) and both bridges match
        /// those names exactly. The C# member name (<c>AdjustId</c>) matched neither, so every call was dropped.
        /// </summary>
        internal static string IntegrationAttributeKey(IntegrationAttribute attribute)
        {
            var name = attribute.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
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
                if (string.IsNullOrEmpty(json)) return new Entitlements();
                var dict = Json.Deserialize(json) as Dictionary<string, object>;
                if (dict == null) return new Entitlements();
                var result = new Entitlements();
                result.Active = DeserializeEntitlementList(dict.ContainsKey("active") ? dict["active"] as List<object> : null);
                result.Inactive = DeserializeEntitlementList(dict.ContainsKey("inactive") ? dict["inactive"] as List<object> : null);
                result.All = DeserializeEntitlementList(dict.ContainsKey("all") ? dict["all"] as List<object> : null);
                result.Web = DeserializeEntitlementList(dict.ContainsKey("web") ? dict["web"] as List<object> : null);
                return result;
            }
        }

        public List<Entitlement> GetEntitlementsByProductIds(List<string> productIds)
        {
            string json = CallNative_GetEntitlementsByProductIds(Json.Serialize(productIds));
            if (string.IsNullOrEmpty(json)) return new List<Entitlement>();
            var list = Json.Deserialize(json) as List<object>;
            return DeserializeEntitlementList(list);
        }

        // ------- Customer Info -------

        public void GetCustomerInfo(Action<CustomerInfo> completion)
        {
            string callbackId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                var dict = string.IsNullOrEmpty(json) ? null : Json.Deserialize(json) as Dictionary<string, object>;
                completion(BridgeCallbackHandler.DeserializeCustomerInfo(dict) ?? new CustomerInfo());
            });
            CallNative_GetCustomerInfo(callbackId);
        }

        /// <summary>
        /// Fetches the customer info from Superwall's servers instead of returning the copy the SDK has, for example
        /// after a purchase made outside the app. Android has no such call and returns the same as
        /// <see cref="GetCustomerInfo"/>.
        /// </summary>
        public void RefreshCustomerInfo(Action<CustomerInfo> completion)
        {
            string callbackId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                var dict = string.IsNullOrEmpty(json) ? null : Json.Deserialize(json) as Dictionary<string, object>;
                completion(BridgeCallbackHandler.DeserializeCustomerInfo(dict) ?? new CustomerInfo());
            });
            CallNative_RefreshCustomerInfo(callbackId);
        }

        // ------- Subscription Status -------

        public SubscriptionStatus SubscriptionStatus
        {
            get
            {
                string json = CallNative_GetSubscriptionStatus();
                if (!string.IsNullOrEmpty(json))
                {
                    var dict = Json.Deserialize(json) as Dictionary<string, object>;
                    if (dict != null)
                    {
                        return BridgeCallbackHandler.DeserializeSubscriptionStatus(dict);
                    }
                }
                return SubscriptionStatus.CreateUnknown();
            }
            set
            {
                var data = new Dictionary<string, object>();
                data["type"] = value.Type.ToString().ToLowerInvariant();
                if (value is SubscriptionStatus.ActiveStatus active && active.Entitlements != null)
                {
                    var entitlements = new List<object>();
                    foreach (var entitlement in active.Entitlements)
                    {
                        if (entitlement == null || !entitlement.IsActive || string.IsNullOrEmpty(entitlement.Id)) continue;
                        entitlements.Add(new Dictionary<string, object> { { "id", entitlement.Id } });
                    }
                    data["entitlements"] = entitlements;
                }
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

        /// <summary>Handles a deep link sent to the app. Needs <see cref="Configure"/> first; prefer <see cref="HandleDeepLink(Uri)"/>.</summary>
        public bool HandleDeepLink(string url)
        {
            return CallNative_HandleDeepLink(url);
        }

        /// <summary>
        /// Handles a deep link sent to the app: paywall previews, web checkout redemption codes, Superwall universal
        /// links and <c>deepLink_open</c> triggers. Returns whether Superwall handles the link, so other handlers can
        /// take the rest. Unlike the instance method it can be called before <see cref="Configure"/>, for a link that
        /// launched the app: the native SDKs keep it until they're configured.
        /// </summary>
        public static bool HandleDeepLink(Uri url)
        {
            if (url == null) return false;
            return CallNative_HandleDeepLink(url.OriginalString);
        }

        // ------- Event Tracking and Ad Consent -------

        /// <summary>
        /// Which events the SDK sends to Superwall. Set it at any time, for example to
        /// <see cref="global::Superwall.EventTrackingBehavior.None"/> when the user declines data collection.
        /// </summary>
        public EventTrackingBehavior EventTrackingBehavior
        {
            get
            {
                return ParseEnumOrDefault(CallNative_GetEventTrackingBehavior(), global::Superwall.EventTrackingBehavior.All);
            }
            set { CallNative_SetEventTrackingBehavior(SerializeEventTrackingBehavior(value)); }
        }

        /// <summary>
        /// The user's consent for advertising use of their data, passed on with the conversions Superwall uploads to
        /// Google Ads and Meta. Set it from your consent flow. Until it's set, the getter returns both as granted.
        /// </summary>
        public AdConsent AdConsent
        {
            get
            {
                var result = new AdConsent();
                if (Json.Deserialize(CallNative_GetAdConsent() ?? "") is Dictionary<string, object> dict)
                {
                    result.AdUserData = ParseEnumOrDefault(dict.ContainsKey("adUserData") ? dict["adUserData"] as string : null, AdConsentStatus.Granted);
                    result.AdPersonalization = ParseEnumOrDefault(dict.ContainsKey("adPersonalization") ? dict["adPersonalization"] as string : null, AdConsentStatus.Granted);
                }
                return result;
            }
            set { CallNative_SetAdConsent(Json.Serialize(SerializeAdConsent(value ?? new AdConsent()))); }
        }

        // ------- Appearance -------

        /// <summary>Shows paywalls in light or dark mode, whatever the system setting. Pass null to follow the system again.</summary>
        public void SetInterfaceStyle(InterfaceStyle? style)
        {
            CallNative_SetInterfaceStyle(style.HasValue ? (style.Value == InterfaceStyle.Dark ? "dark" : "light") : null);
        }

        // ------- Notifications -------

        /// <summary>Cancels the local notifications Superwall scheduled, such as free trial reminders, that haven't been delivered.</summary>
        public void CancelAllScheduledNotifications()
        {
            CallNative_CancelAllScheduledNotifications();
        }

        /// <summary>
        /// Android only. Registers the device's Firebase Cloud Messaging token, so Superwall can send it push
        /// notifications. Call it at launch and whenever the token changes. Does nothing on iOS.
        /// </summary>
        public void SetPushToken(string token)
        {
            if (token == null) return;
            CallNative_SetPushToken(token);
        }

        /// <summary>Android only. Stops Superwall sending this device push notifications. Does nothing on iOS.</summary>
        public void OptOutOfNotifications()
        {
            CallNative_SetNotificationsEnabled(false);
        }

        /// <summary>Android only. Lets Superwall send this device push notifications again. Does nothing on iOS.</summary>
        public void OptInToNotifications()
        {
            CallNative_SetNotificationsEnabled(true);
        }

        /// <summary>
        /// Android only. Reports that the user opened a push notification, passing the notification's data
        /// (<c>RemoteMessage.Data</c>). Notifications Superwall didn't send are ignored. Does nothing on iOS.
        /// </summary>
        public void HandleNotificationOpen(Dictionary<string, string> data)
        {
            if (data == null) return;
            CallNative_HandleNotificationOpen(Json.Serialize(data));
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
                var dict = Json.Deserialize(json) as Dictionary<string, object>;
                if (dict == null) return null;
                return BridgeCallbackHandler.DeserializePaywallInfo(dict);
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

        // ------- Customer Center -------

        /// <summary>
        /// Presents the Customer Center, where users view and manage their subscriptions, request refunds, restore
        /// purchases and contact support. Only one can be presented at a time: while one is, this presents nothing
        /// and calls <paramref name="onDismiss"/> straight away.
        /// </summary>
        /// <param name="configuration">Null uses <see cref="SuperwallOptions.CustomerCenter"/>.</param>
        /// <param name="customerCenterDelegate">Receives this presentation's events. Kept until it's dismissed.</param>
        /// <param name="onDismiss">Called once the Customer Center is dismissed.</param>
        public void PresentCustomerCenter(CustomerCenterConfiguration configuration = null,
            ICustomerCenterDelegate customerCenterDelegate = null, Action onDismiss = null)
        {
            string presentationId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterCustomerCenter(presentationId, customerCenterDelegate, onDismiss);
            string configurationJson = configuration != null ? Json.Serialize(SerializeCustomerCenterConfiguration(configuration)) : null;
            CallNative_PresentCustomerCenter(configurationJson, presentationId, customerCenterDelegate != null);
        }

        /// <summary>Dismisses the Customer Center presented with <see cref="PresentCustomerCenter"/>, if any.</summary>
        /// <param name="completion">Called once it has been dismissed, or straight away if none is presented.</param>
        public void DismissCustomerCenter(Action completion = null)
        {
            string callbackId = null;
            if (completion != null)
            {
                callbackId = Guid.NewGuid().ToString();
                BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) => completion());
            }
            CallNative_DismissCustomerCenter(callbackId);
        }

        // ------- Presentation Result -------

        public void GetPresentationResult(string placement, Dictionary<string, object> parameters = null, Action<PresentationResult> completion = null)
        {
            string paramsJson = parameters != null ? Json.Serialize(parameters) : null;
            string callbackId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                PresentationResult result = PresentationResult.PlacementNotFound();
                if (!string.IsNullOrEmpty(json))
                {
                    var dict = Json.Deserialize(json) as Dictionary<string, object>;
                    if (dict != null && dict.ContainsKey("type"))
                    {
                        string type = dict["type"] as string;
                        switch (type)
                        {
                            case "placementNotFound":
                                result = PresentationResult.PlacementNotFound();
                                break;
                            case "noAudienceMatch":
                                result = PresentationResult.NoAudienceMatch();
                                break;
                            case "paywall":
                                result = PresentationResult.Paywall(DeserializeExperiment(dict.ContainsKey("experiment") ? dict["experiment"] as Dictionary<string, object> : null));
                                break;
                            case "holdout":
                                result = PresentationResult.Holdout(DeserializeExperiment(dict.ContainsKey("experiment") ? dict["experiment"] as Dictionary<string, object> : null));
                                break;
                            case "paywallNotAvailable":
                                result = PresentationResult.PaywallNotAvailable();
                                break;
                        }
                    }
                }
                completion?.Invoke(result);
            });
            CallNative_GetPresentationResult(placement, paramsJson, callbackId);
        }

        // ------- Assignments -------

        public void ConfirmAllAssignments(Action<List<ConfirmedAssignment>> completion = null)
        {
            string callbackId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                var assignments = new List<ConfirmedAssignment>();
                if (!string.IsNullOrEmpty(json))
                {
                    var dict = Json.Deserialize(json) as Dictionary<string, object>;
                    if (dict != null && dict.ContainsKey("assignments"))
                    {
                        var list = dict["assignments"] as List<object>;
                        if (list != null)
                        {
                            foreach (var item in list)
                            {
                                var aDict = item as Dictionary<string, object>;
                                if (aDict == null) continue;
                                var assignment = new ConfirmedAssignment();
                                assignment.ExperimentId = aDict.ContainsKey("experimentId") ? aDict["experimentId"] as string : null;
                                assignment.Variant = DeserializeVariant(aDict.ContainsKey("variant") ? aDict["variant"] as Dictionary<string, object> : null);
                                assignments.Add(assignment);
                            }
                        }
                    }
                }
                completion?.Invoke(assignments);
            });
            CallNative_ConfirmAllAssignments(callbackId);
        }

        // ------- Purchases -------

        public void RestorePurchases(Action<RestorationResult> completion = null)
        {
            string callbackId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                RestorationResult result = RestorationResult.Restored();
                if (!string.IsNullOrEmpty(json))
                {
                    var dict = Json.Deserialize(json) as Dictionary<string, object>;
                    if (dict != null && dict.ContainsKey("type"))
                    {
                        string type = dict["type"] as string;
                        if (type == "failed")
                        {
                            string error = dict.ContainsKey("error") ? dict["error"] as string : "";
                            result = RestorationResult.Failed(error ?? "");
                        }
                    }
                }
                completion?.Invoke(result);
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

        // ------- Local Resources -------

        /// <summary>
        /// Map asset names to local file paths for paywall WebViews (served via swlocal:// URLs).
        /// Keys are asset names used in the paywall template, values are absolute file paths.
        /// Pass null to clear them. On Android they can be changed at any time; on iOS they are read
        /// once by <see cref="Configure"/>, so call this before it.
        /// </summary>
        public void SetLocalResources(Dictionary<string, string> resources)
        {
            string json = resources != null ? Json.Serialize(resources) : null;
            CallNative_SetLocalResources(json);
        }

        // ------- Purchase -------

        public void Purchase(string productId, Action<PurchaseResult> completion)
        {
            string callbackId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                PurchaseResult result = PurchaseResult.Cancelled();
                if (!string.IsNullOrEmpty(json))
                {
                    var dict = Json.Deserialize(json) as Dictionary<string, object>;
                    if (dict != null && dict.ContainsKey("type"))
                    {
                        string type = dict["type"] as string;
                        switch (type)
                        {
                            case "cancelled":
                                result = PurchaseResult.Cancelled();
                                break;
                            case "purchased":
                                result = PurchaseResult.Purchased();
                                break;
                            case "pending":
                                result = PurchaseResult.Pending();
                                break;
                            case "failed":
                                string error = dict.ContainsKey("error") ? dict["error"] as string : "";
                                result = PurchaseResult.Failed(error ?? "");
                                break;
                        }
                    }
                }
                completion?.Invoke(result);
            });
            CallNative_Purchase(productId, callbackId);
        }

        // ------- Products -------

        public void GetProducts(List<string> productIds, Action<Dictionary<string, StoreProduct>> completion)
        {
            string callbackId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                var result = new Dictionary<string, StoreProduct>();
                if (!string.IsNullOrEmpty(json))
                {
                    var dict = Json.Deserialize(json) as Dictionary<string, object>;
                    // Android replies with an error instead of products when the store can't be reached.
                    if (dict != null && dict.ContainsKey("error"))
                    {
                        Debug.LogWarning($"[Superwall] GetProducts failed: {dict["error"]}");
                    }
                    if (dict != null && dict.ContainsKey("products"))
                    {
                        var products = dict["products"] as Dictionary<string, object>;
                        if (products != null)
                        {
                            foreach (var kvp in products)
                            {
                                var productDict = kvp.Value as Dictionary<string, object>;
                                if (productDict != null)
                                {
                                    result[kvp.Key] = DeserializeStoreProduct(productDict);
                                }
                            }
                        }
                    }
                }
                completion?.Invoke(result);
            });
            CallNative_GetProducts(Json.Serialize(productIds), callbackId);
        }

        // ------- Get Assignments -------

        public void GetAssignments(Action<List<ConfirmedAssignment>> completion)
        {
            string callbackId = Guid.NewGuid().ToString();
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                var assignments = new List<ConfirmedAssignment>();
                if (!string.IsNullOrEmpty(json))
                {
                    var dict = Json.Deserialize(json) as Dictionary<string, object>;
                    if (dict != null && dict.ContainsKey("assignments"))
                    {
                        var list = dict["assignments"] as List<object>;
                        if (list != null)
                        {
                            foreach (var item in list)
                            {
                                var aDict = item as Dictionary<string, object>;
                                if (aDict == null) continue;
                                var assignment = new ConfirmedAssignment();
                                assignment.ExperimentId = aDict.ContainsKey("experimentId") ? aDict["experimentId"] as string : null;
                                assignment.Variant = DeserializeVariant(aDict.ContainsKey("variant") ? aDict["variant"] as Dictionary<string, object> : null);
                                assignments.Add(assignment);
                            }
                        }
                    }
                }
                completion?.Invoke(assignments);
            });
            CallNative_GetAssignments(callbackId);
        }

        // ------- Show Alert -------

        public void ShowAlert(string title = null, string message = null, string actionTitle = null, string closeActionTitle = "Done", Action onAction = null, Action onClose = null)
        {
            var data = new Dictionary<string, object>();
            if (title != null) data["title"] = title;
            if (message != null) data["message"] = message;
            if (actionTitle != null) data["actionTitle"] = actionTitle;
            data["closeActionTitle"] = closeActionTitle;

            string onActionCallbackId = null;
            if (onAction != null)
            {
                onActionCallbackId = Guid.NewGuid().ToString();
                BridgeCallbackHandler.Instance.RegisterAsyncCallback(onActionCallbackId, (json) =>
                {
                    onAction();
                });
                data["onActionCallbackId"] = onActionCallbackId;
            }

            string onCloseCallbackId = null;
            if (onClose != null)
            {
                onCloseCallbackId = Guid.NewGuid().ToString();
                BridgeCallbackHandler.Instance.RegisterAsyncCallback(onCloseCallbackId, (json) =>
                {
                    onClose();
                });
                data["onCloseCallbackId"] = onCloseCallbackId;
            }

            CallNative_ShowAlert(Json.Serialize(data));
        }

        // ------- Refresh Configuration -------

        public void RefreshConfiguration()
        {
            CallNative_RefreshConfiguration();
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

        // ============= Deserialization Helpers =============

        private static Entitlement DeserializeEntitlement(Dictionary<string, object> dict)
        {
            if (dict == null) return new Entitlement();
            var e = new Entitlement();
            e.Id = dict.ContainsKey("id") ? dict["id"] as string : null;
            e.IsActive = dict.ContainsKey("isActive") && dict["isActive"] is bool b && b;
            if (dict.ContainsKey("type"))
            {
                string typeStr = dict["type"] as string;
                if (Enum.TryParse<EntitlementType>(typeStr, true, out var et)) e.Type = et;
            }
            if (dict.ContainsKey("productIds") && dict["productIds"] is List<object> pids)
            {
                e.ProductIds = new List<string>();
                foreach (var pid in pids) e.ProductIds.Add(pid?.ToString());
            }
            if (dict.ContainsKey("latestProductId")) e.LatestProductId = dict["latestProductId"] as string;
            if (dict.ContainsKey("store"))
            {
                string storeStr = dict["store"] as string;
                if (storeStr != null && Enum.TryParse<ProductStore>(storeStr, true, out var ps)) e.Store = ps;
            }
            if (dict.ContainsKey("startsAt") && dict["startsAt"] != null) e.StartsAt = Convert.ToInt64(dict["startsAt"]);
            if (dict.ContainsKey("renewedAt") && dict["renewedAt"] != null) e.RenewedAt = Convert.ToInt64(dict["renewedAt"]);
            if (dict.ContainsKey("expiresAt") && dict["expiresAt"] != null) e.ExpiresAt = Convert.ToInt64(dict["expiresAt"]);
            if (dict.ContainsKey("isLifetime") && dict["isLifetime"] is bool lt) e.IsLifetime = lt;
            if (dict.ContainsKey("willRenew") && dict["willRenew"] is bool wr) e.WillRenew = wr;
            if (dict.ContainsKey("state"))
            {
                string stateStr = dict["state"] as string;
                if (stateStr != null && Enum.TryParse<LatestSubscriptionState>(stateStr, true, out var ls)) e.State = ls;
            }
            if (dict.ContainsKey("offerType"))
            {
                string offerStr = dict["offerType"] as string;
                if (offerStr != null && Enum.TryParse<LatestSubscriptionOfferType>(offerStr, true, out var ot)) e.OfferType = ot;
            }
            if (dict.ContainsKey("isRevoked") && dict["isRevoked"] is bool revoked) e.IsRevoked = revoked;
            if (dict.ContainsKey("renewsAt") && dict["renewsAt"] != null) e.RenewsAt = Convert.ToInt64(dict["renewsAt"]);
            if (dict.ContainsKey("isInGracePeriod") && dict["isInGracePeriod"] is bool grace) e.IsInGracePeriod = grace;
            return e;
        }

        private static List<Entitlement> DeserializeEntitlementList(List<object> list)
        {
            var result = new List<Entitlement>();
            if (list == null) return result;
            foreach (var item in list)
            {
                var dict = item as Dictionary<string, object>;
                if (dict != null) result.Add(DeserializeEntitlement(dict));
            }
            return result;
        }

        private static Variant DeserializeVariant(Dictionary<string, object> dict)
        {
            if (dict == null) return new Variant();
            var v = new Variant();
            v.Id = dict.ContainsKey("id") ? dict["id"] as string : null;
            v.PaywallId = dict.ContainsKey("paywallId") ? dict["paywallId"] as string : null;
            if (dict.ContainsKey("type"))
            {
                string typeStr = dict["type"] as string;
                if (typeStr == "treatment") v.Type = VariantType.Treatment;
                else if (typeStr == "holdout") v.Type = VariantType.Holdout;
            }
            return v;
        }

        private static Experiment DeserializeExperiment(Dictionary<string, object> dict)
        {
            if (dict == null) return new Experiment();
            var exp = new Experiment();
            exp.Id = dict.ContainsKey("id") ? dict["id"] as string : null;
            exp.GroupId = dict.ContainsKey("groupId") ? dict["groupId"] as string : null;
            exp.Variant = DeserializeVariant(dict.ContainsKey("variant") ? dict["variant"] as Dictionary<string, object> : null);
            return exp;
        }

        /// <summary>
        /// Parses a bridge number that may arrive as a JSON number (iOS) or as a string (Android sends
        /// <c>BigDecimal.toString()</c>, e.g. "199.99"). <c>Convert.ToDouble(object)</c> parses strings with the
        /// device culture, so on a pt-BR or de-DE device "199.99" became 19999. Always invariant.
        /// </summary>
        private static double ToDoubleInvariant(object value)
        {
            switch (value)
            {
                case null: return 0;
                case double d: return d;
                case float f: return f;
                case int i: return i;
                case long l: return l;
                case string text:
                    return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : 0;
                default:
                    return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
        }

        // Missing screens are the default's, as both native SDKs require them. Optional values are left out so the
        // native defaults (localized titles, path IDs) apply.
        internal static Dictionary<string, object> SerializeCustomerCenterConfiguration(CustomerCenterConfiguration configuration)
        {
            var defaults = CustomerCenterConfiguration.Default;
            var support = configuration.Support ?? new CustomerCenterSupport();
            var supportDict = new Dictionary<string, object>
            {
                { "warnsAboutUpdates", support.WarnsAboutUpdates },
                { "checksAppStoreForUpdates", support.ChecksAppStoreForUpdates }
            };
            if (support.Email != null) supportDict["email"] = support.Email;
            if (support.LatestAppVersion != null) supportDict["latestAppVersion"] = support.LatestAppVersion;
            if (support.WebManagementUrl != null) supportDict["webManagementUrl"] = support.WebManagementUrl;

            var dict = new Dictionary<string, object>
            {
                { "managementScreen", SerializeCustomerCenterScreen(configuration.ManagementScreen ?? defaults.ManagementScreen) },
                { "noPurchasesScreen", SerializeCustomerCenterScreen(configuration.NoPurchasesScreen ?? defaults.NoPurchasesScreen) },
                { "support", supportDict },
                { "showsAccountDetails", configuration.ShowsAccountDetails },
                { "warnsAboutDuplicateSubscriptions", configuration.WarnsAboutDuplicateSubscriptions }
            };
            if (configuration.Accent != null)
            {
                dict["accent"] = new Dictionary<string, object> { { "light", configuration.Accent.Light }, { "dark", configuration.Accent.Dark } };
            }
            return dict;
        }

        private static Dictionary<string, object> SerializeCustomerCenterScreen(CustomerCenterScreen screen)
        {
            var paths = new List<object>();
            if (screen.Paths != null)
            {
                foreach (var path in screen.Paths)
                {
                    if (path != null) paths.Add(SerializeCustomerCenterPath(path));
                }
            }
            var dict = new Dictionary<string, object> { { "paths", paths } };
            if (screen.Title != null) dict["title"] = screen.Title;
            if (screen.Subtitle != null) dict["subtitle"] = screen.Subtitle;
            return dict;
        }

        private static Dictionary<string, object> SerializeCustomerCenterPath(CustomerCenterPath path)
        {
            var name = path.Type.ToString();
            var dict = new Dictionary<string, object> { { "type", char.ToLowerInvariant(name[0]) + name.Substring(1) } };
            if (path.Id != null) dict["id"] = path.Id;
            if (path.Title != null) dict["title"] = path.Title;
            switch (path.Type)
            {
                case CustomerCenterActionType.Refund:
                    if (path.RefundWindowMillis.HasValue) dict["windowMillis"] = path.RefundWindowMillis.Value;
                    break;
                case CustomerCenterActionType.ChangePlan:
                    if (path.ProductIds != null) dict["productIds"] = path.ProductIds;
                    break;
                case CustomerCenterActionType.Url:
                    dict["url"] = path.Url;
                    dict["openMethod"] = path.OpenMethod == CustomerCenterOpenMethod.External ? "external" : "inApp";
                    break;
                case CustomerCenterActionType.Custom:
                    dict["identifier"] = path.Identifier;
                    break;
            }
            if (path.Survey != null)
            {
                var options = new List<object>();
                if (path.Survey.Options != null)
                {
                    foreach (var option in path.Survey.Options)
                    {
                        if (option == null) continue;
                        var optionDict = new Dictionary<string, object> { { "id", option.Id } };
                        if (option.Title != null) optionDict["title"] = option.Title;
                        options.Add(optionDict);
                    }
                }
                var survey = new Dictionary<string, object> { { "id", path.Survey.Id }, { "options", options } };
                if (path.Survey.Title != null) survey["title"] = path.Survey.Title;
                dict["survey"] = survey;
            }
            return dict;
        }

        private static T ParseEnumOrDefault<T>(string value, T defaultValue) where T : struct
        {
            return !string.IsNullOrEmpty(value) && Enum.TryParse<T>(value, true, out var parsed) ? parsed : defaultValue;
        }

        // The native SDKs' names: all, superwallOnly, none.
        internal static string SerializeEventTrackingBehavior(EventTrackingBehavior behavior)
        {
            var name = behavior.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        internal static Dictionary<string, object> SerializeAdConsent(AdConsent consent)
        {
            return new Dictionary<string, object>
            {
                { "adUserData", consent.AdUserData == AdConsentStatus.Denied ? "denied" : "granted" },
                { "adPersonalization", consent.AdPersonalization == AdConsentStatus.Denied ? "denied" : "granted" }
            };
        }

        // Also used by BridgeCallbackHandler for the `product` carried on delegate events.
        internal static StoreProduct DeserializeStoreProduct(Dictionary<string, object> dict)
        {
            if (dict == null) return new StoreProduct();
            var p = new StoreProduct();
            p.ProductIdentifier = dict.ContainsKey("productIdentifier") ? dict["productIdentifier"] as string : null;
            p.SubscriptionGroupIdentifier = dict.ContainsKey("subscriptionGroupIdentifier") ? dict["subscriptionGroupIdentifier"] as string : null;
            p.LocalizedPrice = dict.ContainsKey("localizedPrice") ? dict["localizedPrice"] as string : null;
            p.LocalizedSubscriptionPeriod = dict.ContainsKey("localizedSubscriptionPeriod") ? dict["localizedSubscriptionPeriod"] as string : null;
            p.Period = dict.ContainsKey("period") ? dict["period"] as string : null;
            p.Periodly = dict.ContainsKey("periodly") ? dict["periodly"] as string : null;
            p.PeriodWeeks = dict.ContainsKey("periodWeeks") ? Convert.ToInt32(dict["periodWeeks"]) : 0;
            p.PeriodWeeksString = dict.ContainsKey("periodWeeksString") ? dict["periodWeeksString"] as string : null;
            p.PeriodMonths = dict.ContainsKey("periodMonths") ? Convert.ToInt32(dict["periodMonths"]) : 0;
            p.PeriodMonthsString = dict.ContainsKey("periodMonthsString") ? dict["periodMonthsString"] as string : null;
            p.PeriodYears = dict.ContainsKey("periodYears") ? Convert.ToInt32(dict["periodYears"]) : 0;
            p.PeriodYearsString = dict.ContainsKey("periodYearsString") ? dict["periodYearsString"] as string : null;
            p.PeriodDays = dict.ContainsKey("periodDays") ? Convert.ToInt32(dict["periodDays"]) : 0;
            p.PeriodDaysString = dict.ContainsKey("periodDaysString") ? dict["periodDaysString"] as string : null;
            p.DailyPrice = dict.ContainsKey("dailyPrice") ? dict["dailyPrice"] as string : null;
            p.WeeklyPrice = dict.ContainsKey("weeklyPrice") ? dict["weeklyPrice"] as string : null;
            p.MonthlyPrice = dict.ContainsKey("monthlyPrice") ? dict["monthlyPrice"] as string : null;
            p.YearlyPrice = dict.ContainsKey("yearlyPrice") ? dict["yearlyPrice"] as string : null;
            p.HasFreeTrial = dict.ContainsKey("hasFreeTrial") && dict["hasFreeTrial"] is bool hft && hft;
            p.TrialPeriodEndDate = dict.ContainsKey("trialPeriodEndDate") ? dict["trialPeriodEndDate"] as string : null;
            p.TrialPeriodEndDateString = dict.ContainsKey("trialPeriodEndDateString") ? dict["trialPeriodEndDateString"] as string : null;
            p.LocalizedTrialPeriodPrice = dict.ContainsKey("localizedTrialPeriodPrice") ? dict["localizedTrialPeriodPrice"] as string : null;
            p.TrialPeriodPrice = dict.ContainsKey("trialPeriodPrice") ? ToDoubleInvariant(dict["trialPeriodPrice"]) : 0;
            p.TrialPeriodDays = dict.ContainsKey("trialPeriodDays") ? Convert.ToInt32(dict["trialPeriodDays"]) : 0;
            p.TrialPeriodDaysString = dict.ContainsKey("trialPeriodDaysString") ? dict["trialPeriodDaysString"] as string : null;
            p.TrialPeriodWeeks = dict.ContainsKey("trialPeriodWeeks") ? Convert.ToInt32(dict["trialPeriodWeeks"]) : 0;
            p.TrialPeriodWeeksString = dict.ContainsKey("trialPeriodWeeksString") ? dict["trialPeriodWeeksString"] as string : null;
            p.TrialPeriodMonths = dict.ContainsKey("trialPeriodMonths") ? Convert.ToInt32(dict["trialPeriodMonths"]) : 0;
            p.TrialPeriodMonthsString = dict.ContainsKey("trialPeriodMonthsString") ? dict["trialPeriodMonthsString"] as string : null;
            p.TrialPeriodYears = dict.ContainsKey("trialPeriodYears") ? Convert.ToInt32(dict["trialPeriodYears"]) : 0;
            p.TrialPeriodYearsString = dict.ContainsKey("trialPeriodYearsString") ? dict["trialPeriodYearsString"] as string : null;
            p.TrialPeriodText = dict.ContainsKey("trialPeriodText") ? dict["trialPeriodText"] as string : null;
            p.Locale = dict.ContainsKey("locale") ? dict["locale"] as string : null;
            p.LanguageCode = dict.ContainsKey("languageCode") ? dict["languageCode"] as string : null;
            p.CurrencySymbol = dict.ContainsKey("currencySymbol") ? dict["currencySymbol"] as string : null;
            p.CurrencyCode = dict.ContainsKey("currencyCode") ? dict["currencyCode"] as string : null;
            p.IsFamilyShareable = dict.ContainsKey("isFamilyShareable") && dict["isFamilyShareable"] is bool ifs && ifs;
            p.RegionCode = dict.ContainsKey("regionCode") ? dict["regionCode"] as string : null;
            p.Price = dict.ContainsKey("price") ? ToDoubleInvariant(dict["price"]) : 0;
            p.FullIdentifier = dict.ContainsKey("fullIdentifier") ? dict["fullIdentifier"] as string : null;
            p.ProductType = dict.ContainsKey("productType") ? dict["productType"] as string : null;
            p.StoreFrontCountryCode = dict.ContainsKey("storeFrontCountryCode") ? dict["storeFrontCountryCode"] as string : null;
            p.IsCustomProduct = dict.ContainsKey("isCustomProduct") && dict["isCustomProduct"] is bool custom && custom;
            p.CustomTransactionId = dict.ContainsKey("customTransactionId") ? dict["customTransactionId"] as string : null;

            if (dict.ContainsKey("entitlements") && dict["entitlements"] is List<object> entList)
            {
                p.Entitlements = DeserializeEntitlementList(entList);
            }

            if (dict.ContainsKey("attributes") && dict["attributes"] is Dictionary<string, object> attrs)
            {
                p.Attributes = new Dictionary<string, string>();
                foreach (var kvp in attrs)
                {
                    p.Attributes[kvp.Key] = kvp.Value?.ToString();
                }
            }

            return p;
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
                    // Only the texts the app changed. Android shows its own translation of each one left unset,
                    // and the C# defaults are the English texts, so sending them replaced the translations.
                    var defaults = new RestoreFailed();
                    var restoreFailed = new Dictionary<string, object>();
                    if (options.Paywalls.RestoreFailed.Title != defaults.Title)
                        restoreFailed["title"] = options.Paywalls.RestoreFailed.Title;
                    if (options.Paywalls.RestoreFailed.Message != defaults.Message)
                        restoreFailed["message"] = options.Paywalls.RestoreFailed.Message;
                    if (options.Paywalls.RestoreFailed.CloseButtonTitle != defaults.CloseButtonTitle)
                        restoreFailed["closeButtonTitle"] = options.Paywalls.RestoreFailed.CloseButtonTitle;
                    paywalls["restoreFailed"] = restoreFailed;
                }

                if (options.Paywalls.OverrideProductsByName != null)
                {
                    paywalls["overrideProductsByName"] = options.Paywalls.OverrideProductsByName;
                }

                paywalls["useCachedTemplates"] = options.Paywalls.UseCachedTemplates;
                if (options.Paywalls.TimeoutAfter.HasValue)
                {
                    paywalls["timeoutAfter"] = options.Paywalls.TimeoutAfter.Value;
                }
                paywalls["androidPresentation"] = options.Paywalls.AndroidPresentation.ToString();
                paywalls["optimisticLoading"] = options.Paywalls.OptimisticLoading;
                if (options.Paywalls.NotificationPermissionsDenied != null)
                {
                    var denied = options.Paywalls.NotificationPermissionsDenied;
                    paywalls["notificationPermissionsDenied"] = new Dictionary<string, object>
                    {
                        { "title", denied.Title },
                        { "message", denied.Message },
                        { "actionButtonTitle", denied.ActionButtonTitle },
                        { "closeButtonTitle", denied.CloseButtonTitle }
                    };
                }

                dict["paywalls"] = paywalls;
            }

            dict["networkEnvironment"] = options.NetworkEnvironment.ToString();
#pragma warning disable CS0618 // Still sent for apps that set it; the bridges apply eventTrackingBehavior after it.
            dict["isExternalDataCollectionEnabled"] = options.IsExternalDataCollectionEnabled;
#pragma warning restore CS0618
            if (options.EventTrackingBehavior.HasValue)
                dict["eventTrackingBehavior"] = SerializeEventTrackingBehavior(options.EventTrackingBehavior.Value);
            if (options.AdConsent != null) dict["adConsent"] = SerializeAdConsent(options.AdConsent);
            if (options.StoreKitVersion.HasValue)
                dict["storeKitVersion"] = options.StoreKitVersion.Value == global::Superwall.StoreKitVersion.StoreKit1 ? "storeKit1" : "storeKit2";
            dict["enableExperimentalDeviceVariables"] = options.EnableExperimentalDeviceVariables;
            if (options.CustomerCenter != null) dict["customerCenter"] = SerializeCustomerCenterConfiguration(options.CustomerCenter);
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

        private static void CallNative_Purchase(string productId, string callbackId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_Purchase(productId, callbackId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.Purchase(productId, callbackId);
#else
            Debug.Log($"[Superwall] Purchase({productId})");
#endif
        }

        private static void CallNative_GetProducts(string productIdsJson, string callbackId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_GetProducts(productIdsJson, callbackId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.GetProducts(productIdsJson, callbackId);
#else
            Debug.Log($"[Superwall] GetProducts({productIdsJson})");
#endif
        }

        private static void CallNative_GetAssignments(string callbackId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_GetAssignments(callbackId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.GetAssignments(callbackId);
#else
            Debug.Log("[Superwall] GetAssignments()");
#endif
        }

        private static void CallNative_ShowAlert(string alertJson)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_ShowAlert(alertJson);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.ShowAlert(alertJson);
#else
            Debug.Log($"[Superwall] ShowAlert({alertJson})");
#endif
        }

        private static void CallNative_RefreshConfiguration()
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_RefreshConfiguration();
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.RefreshConfiguration();
#else
            Debug.Log("[Superwall] RefreshConfiguration()");
#endif
        }

        private static void CallNative_SetLocalResources(string resourcesJson)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_SetLocalResources(resourcesJson);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.SetLocalResources(resourcesJson);
#else
            Debug.Log($"[Superwall] SetLocalResources({resourcesJson})");
#endif
        }

        private static void CallNative_SetPlatformWrapper(string wrapper, string version)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_SetPlatformWrapper(wrapper, version);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.SetPlatformWrapper(wrapper, version);
#else
            Debug.Log($"[Superwall] SetPlatformWrapper({wrapper}, {version})");
#endif
        }

        private static string CallNative_GetIntegrationAttributes()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetIntegrationAttributes();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetIntegrationAttributes();
#else
            Debug.Log("[Superwall] GetIntegrationAttributes()");
            return "{}";
#endif
        }

        private static void CallNative_RefreshCustomerInfo(string callbackId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_RefreshCustomerInfo(callbackId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.RefreshCustomerInfo(callbackId);
#else
            Debug.Log("[Superwall] RefreshCustomerInfo()");
#endif
        }

        private static string CallNative_GetEventTrackingBehavior()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetEventTrackingBehavior();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetEventTrackingBehavior();
#else
            Debug.Log("[Superwall] GetEventTrackingBehavior()");
            return "all";
#endif
        }

        private static void CallNative_SetEventTrackingBehavior(string behavior)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_SetEventTrackingBehavior(behavior);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.SetEventTrackingBehavior(behavior);
#else
            Debug.Log($"[Superwall] SetEventTrackingBehavior({behavior})");
#endif
        }

        private static string CallNative_GetAdConsent()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SuperwallBridgeiOS._SuperwallBridge_GetAdConsent();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return SuperwallBridgeAndroid.GetAdConsent();
#else
            Debug.Log("[Superwall] GetAdConsent()");
            return null;
#endif
        }

        private static void CallNative_SetAdConsent(string adConsentJson)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_SetAdConsent(adConsentJson);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.SetAdConsent(adConsentJson);
#else
            Debug.Log($"[Superwall] SetAdConsent({adConsentJson})");
#endif
        }

        private static void CallNative_SetInterfaceStyle(string style)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_SetInterfaceStyle(style);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.SetInterfaceStyle(style);
#else
            Debug.Log($"[Superwall] SetInterfaceStyle({style})");
#endif
        }

        private static void CallNative_CancelAllScheduledNotifications()
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_CancelAllScheduledNotifications();
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.CancelAllScheduledNotifications();
#else
            Debug.Log("[Superwall] CancelAllScheduledNotifications()");
#endif
        }

        private static void CallNative_SetPushToken(string token)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_SetPushToken(token);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.SetPushToken(token);
#else
            Debug.Log("[Superwall] SetPushToken()");
#endif
        }

        private static void CallNative_SetNotificationsEnabled(bool enabled)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_SetNotificationsEnabled(enabled);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.SetNotificationsEnabled(enabled);
#else
            Debug.Log($"[Superwall] SetNotificationsEnabled({enabled})");
#endif
        }

        private static void CallNative_HandleNotificationOpen(string dataJson)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_HandleNotificationOpen(dataJson);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.HandleNotificationOpen(dataJson);
#else
            Debug.Log($"[Superwall] HandleNotificationOpen({dataJson})");
#endif
        }

        private static void CallNative_PresentCustomerCenter(string configurationJson, string presentationId, bool hasDelegate)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_PresentCustomerCenter(configurationJson, presentationId, hasDelegate);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.PresentCustomerCenter(configurationJson, presentationId, hasDelegate);
#else
            Debug.Log($"[Superwall] PresentCustomerCenter({configurationJson})");
#endif
        }

        private static void CallNative_DismissCustomerCenter(string callbackId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SuperwallBridgeiOS._SuperwallBridge_DismissCustomerCenter(callbackId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SuperwallBridgeAndroid.DismissCustomerCenter(callbackId);
#else
            Debug.Log("[Superwall] DismissCustomerCenter()");
#endif
        }
    }
}
