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
    }
#endif
}
