using UnityEngine;

namespace Superwall.Internal
{
#if UNITY_ANDROID && !UNITY_EDITOR
    internal static class SuperwallBridgeAndroid
    {
        private static AndroidJavaObject _bridge;

        private static AndroidJavaObject Bridge
        {
            get
            {
                if (_bridge == null)
                {
                    _bridge = new AndroidJavaObject("com.superwall.sdk.unity.SuperwallUnityBridge");
                }
                return _bridge;
            }
        }

        internal static void Configure(string apiKey, string optionsJson, bool hasPurchaseController, string completionCallbackId)
        {
            Bridge.Call("configure", apiKey, optionsJson, hasPurchaseController, completionCallbackId);
        }

        internal static void Reset()
        {
            Bridge.Call("reset");
        }

        internal static void SetDelegate(bool hasDelegate)
        {
            Bridge.Call("setDelegate", hasDelegate);
        }

        internal static void Identify(string userId, string identityOptionsJson)
        {
            Bridge.Call("identify", userId, identityOptionsJson);
        }

        internal static string GetUserId()
        {
            return Bridge.Call<string>("getUserId");
        }

        internal static bool GetIsLoggedIn()
        {
            return Bridge.Call<bool>("getIsLoggedIn");
        }

        internal static bool GetIsInitialized()
        {
            return Bridge.Call<bool>("getIsInitialized");
        }

        internal static void SetUserAttributes(string attributesJson)
        {
            Bridge.Call("setUserAttributes", attributesJson);
        }

        internal static string GetUserAttributes()
        {
            return Bridge.Call<string>("getUserAttributes");
        }

        internal static void SetIntegrationAttribute(string attribute, string value)
        {
            Bridge.Call("setIntegrationAttribute", attribute, value);
        }

        internal static void SetIntegrationAttributes(string attributesJson)
        {
            Bridge.Call("setIntegrationAttributes", attributesJson);
        }

        internal static void GetDeviceAttributes(string callbackId)
        {
            Bridge.Call("getDeviceAttributes", callbackId);
        }

        internal static string GetLocaleIdentifier()
        {
            return Bridge.Call<string>("getLocaleIdentifier");
        }

        internal static void SetLocaleIdentifier(string localeIdentifier)
        {
            Bridge.Call("setLocaleIdentifier", localeIdentifier);
        }

        internal static string GetLogLevel()
        {
            return Bridge.Call<string>("getLogLevel");
        }

        internal static void SetLogLevel(string logLevel)
        {
            Bridge.Call("setLogLevel", logLevel);
        }

        internal static string GetEntitlements()
        {
            return Bridge.Call<string>("getEntitlements");
        }

        internal static string GetEntitlementsByProductIds(string productIdsJson)
        {
            return Bridge.Call<string>("getEntitlementsByProductIds", productIdsJson);
        }

        internal static void GetCustomerInfo(string callbackId)
        {
            Bridge.Call("getCustomerInfo", callbackId);
        }

        internal static string GetSubscriptionStatus()
        {
            return Bridge.Call<string>("getSubscriptionStatus");
        }

        internal static void SetSubscriptionStatus(string statusJson)
        {
            Bridge.Call("setSubscriptionStatus", statusJson);
        }

        internal static string GetConfigurationStatus()
        {
            return Bridge.Call<string>("getConfigurationStatus");
        }

        internal static bool GetIsConfigured()
        {
            return Bridge.Call<bool>("getIsConfigured");
        }

        internal static bool GetIsPaywallPresented()
        {
            return Bridge.Call<bool>("getIsPaywallPresented");
        }

        internal static void PreloadAllPaywalls()
        {
            Bridge.Call("preloadAllPaywalls");
        }

        internal static void PreloadPaywallsForPlacements(string placementNamesJson)
        {
            Bridge.Call("preloadPaywallsForPlacements", placementNamesJson);
        }

        internal static bool HandleDeepLink(string url)
        {
            return Bridge.Call<bool>("handleDeepLink", url);
        }

        internal static void TogglePaywallSpinner(bool isHidden)
        {
            Bridge.Call("togglePaywallSpinner", isHidden);
        }

        internal static string GetLatestPaywallInfo()
        {
            return Bridge.Call<string>("getLatestPaywallInfo");
        }

        internal static void RegisterPlacement(string placement, string paramsJson, string handlerId, string featureId, string callbackId)
        {
            Bridge.Call("registerPlacement", placement, paramsJson, handlerId, featureId, callbackId);
        }

        internal static void Dismiss()
        {
            Bridge.Call("dismiss");
        }

        internal static void GetPresentationResult(string placement, string paramsJson, string callbackId)
        {
            Bridge.Call("getPresentationResult", placement, paramsJson, callbackId);
        }

        internal static void ConfirmAllAssignments(string callbackId)
        {
            Bridge.Call("confirmAllAssignments", callbackId);
        }

        internal static void RestorePurchases(string callbackId)
        {
            Bridge.Call("restorePurchases", callbackId);
        }

        internal static string GetOverrideProductsByName()
        {
            return Bridge.Call<string>("getOverrideProductsByName");
        }

        internal static void SetOverrideProductsByName(string productsJson)
        {
            Bridge.Call("setOverrideProductsByName", productsJson);
        }

        internal static void Consume(string purchaseToken, string callbackId)
        {
            Bridge.Call("consume", purchaseToken, callbackId);
        }

        internal static void RespondToPurchaseController(string callbackId, string resultJson)
        {
            Bridge.Call("respondToPurchaseController", callbackId, resultJson);
        }

        internal static void RespondToRestorePurchases(string callbackId, string resultJson)
        {
            Bridge.Call("respondToRestorePurchases", callbackId, resultJson);
        }

        internal static void RespondToCustomCallback(string callbackId, string resultJson)
        {
            Bridge.Call("respondToCustomCallback", callbackId, resultJson);
        }

        internal static bool IsPurchaseControllerRequestPending(string callbackId)
        {
            return Bridge.Call<bool>("isPurchaseControllerRequestPending", callbackId);
        }

        internal static void Purchase(string productId, string callbackId)
        {
            Bridge.Call("purchase", productId, callbackId);
        }

        internal static void GetProducts(string productIdsJson, string callbackId)
        {
            Bridge.Call("getProducts", productIdsJson, callbackId);
        }

        internal static void GetAssignments(string callbackId)
        {
            Bridge.Call("getAssignments", callbackId);
        }

        internal static void ShowAlert(string alertJson)
        {
            Bridge.Call("showAlert", alertJson);
        }

        internal static void RefreshConfiguration()
        {
            Bridge.Call("refreshConfiguration");
        }

        internal static void SetLocalResources(string resourcesJson)
        {
            Bridge.Call("setLocalResources", resourcesJson);
        }

        internal static void SetPlatformWrapper(string wrapper, string version)
        {
            Bridge.Call("setPlatformWrapper", wrapper, version);
        }

        internal static string GetIntegrationAttributes()
        {
            return Bridge.Call<string>("getIntegrationAttributes");
        }

        internal static void RefreshCustomerInfo(string callbackId)
        {
            Bridge.Call("refreshCustomerInfo", callbackId);
        }

        internal static string GetEventTrackingBehavior()
        {
            return Bridge.Call<string>("getEventTrackingBehavior");
        }

        internal static void SetEventTrackingBehavior(string behavior)
        {
            Bridge.Call("setEventTrackingBehavior", behavior);
        }

        internal static string GetAdConsent()
        {
            return Bridge.Call<string>("getAdConsent");
        }

        internal static void SetAdConsent(string adConsentJson)
        {
            Bridge.Call("setAdConsent", adConsentJson);
        }

        internal static void SetInterfaceStyle(string style)
        {
            Bridge.Call("setInterfaceStyle", style);
        }

        internal static void CancelAllScheduledNotifications()
        {
            Bridge.Call("cancelAllScheduledNotifications");
        }

        internal static void SetPushToken(string token)
        {
            Bridge.Call("setPushToken", token);
        }

        internal static void SetNotificationsEnabled(bool enabled)
        {
            Bridge.Call("setNotificationsEnabled", enabled);
        }

        internal static void HandleNotificationOpen(string dataJson)
        {
            Bridge.Call("handleNotificationOpen", dataJson);
        }

        internal static void PresentCustomerCenter(string configurationJson, string presentationId, bool hasDelegate)
        {
            Bridge.Call("presentCustomerCenter", configurationJson, presentationId, hasDelegate);
        }

        internal static void DismissCustomerCenter(string callbackId)
        {
            Bridge.Call("dismissCustomerCenter", callbackId);
        }

        internal static void RespondToCustomerCenterShouldRestore(string callbackId, bool proceed)
        {
            Bridge.Call("respondToCustomerCenterShouldRestore", callbackId, proceed);
        }

        internal static void ShowEmbeddedPaywall(string viewId, string placement, string paramsJson, float x, float y, float width, float height)
        {
            // An empty string stands for "none": a null argument leaves Unity guessing the Java method's signature.
            Bridge.Call("showEmbeddedPaywall", viewId, placement, paramsJson ?? "", x, y, width, height);
        }

        internal static void SetEmbeddedPaywallRect(string viewId, float x, float y, float width, float height)
        {
            Bridge.Call("setEmbeddedPaywallRect", viewId, x, y, width, height);
        }

        internal static void RemoveEmbeddedPaywall(string viewId)
        {
            Bridge.Call("removeEmbeddedPaywall", viewId);
        }

        internal static void DismissWithCompletion(string callbackId)
        {
            Bridge.Call("dismissWithCompletion", callbackId ?? "");
        }

        internal static void DismissForNextPaywall(string callbackId)
        {
            Bridge.Call("dismissForNextPaywall", callbackId ?? "");
        }

        internal static void OpenRestoreOnWeb()
        {
            Bridge.Call("openRestoreOnWeb");
        }

        internal static string GetExternalAccountId()
        {
            return Bridge.Call<string>("getExternalAccountId");
        }

        internal static void ObservePurchaseStart(string productId)
        {
            Bridge.Call("observePurchaseStart", productId);
        }

        internal static void ObservePurchaseError(string productId, string error)
        {
            Bridge.Call("observePurchaseError", productId, error);
        }

        internal static void ObservePurchaseResult(int billingResponseCode, string debugMessage, string purchasesJson)
        {
            Bridge.Call("observePurchaseResult", billingResponseCode, debugMessage, purchasesJson);
        }

        internal static void DispatchGameControllerKeyEvent(int keyCode, bool isPressed)
        {
            Bridge.Call("dispatchGameControllerKeyEvent", keyCode, isPressed);
        }

        internal static void DispatchGameControllerMotionEvent(float x, float y)
        {
            Bridge.Call("dispatchGameControllerMotionEvent", x, y);
        }

        internal static void Teardown()
        {
            Bridge.Call("teardown");
        }
    }
#endif
}
