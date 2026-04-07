using System.Runtime.InteropServices;

namespace Superwall.Internal
{
#if UNITY_IOS && !UNITY_EDITOR
    internal static class SuperwallBridgeiOS
    {
        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_Configure(string apiKey, string optionsJson, bool hasPurchaseController, string completionCallbackId);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_Reset();

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_SetDelegate(bool hasDelegate);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_Identify(string userId, string identityOptionsJson);

        [DllImport("__Internal")]
        internal static extern string _SuperwallBridge_GetUserId();

        [DllImport("__Internal")]
        internal static extern bool _SuperwallBridge_GetIsLoggedIn();

        [DllImport("__Internal")]
        internal static extern bool _SuperwallBridge_GetIsInitialized();

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_SetUserAttributes(string attributesJson);

        [DllImport("__Internal")]
        internal static extern string _SuperwallBridge_GetUserAttributes();

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_SetIntegrationAttribute(string attribute, string value);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_SetIntegrationAttributes(string attributesJson);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_GetDeviceAttributes(string callbackId);

        [DllImport("__Internal")]
        internal static extern string _SuperwallBridge_GetLocaleIdentifier();

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_SetLocaleIdentifier(string localeIdentifier);

        [DllImport("__Internal")]
        internal static extern string _SuperwallBridge_GetLogLevel();

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_SetLogLevel(string logLevel);

        [DllImport("__Internal")]
        internal static extern string _SuperwallBridge_GetEntitlements();

        [DllImport("__Internal")]
        internal static extern string _SuperwallBridge_GetEntitlementsByProductIds(string productIdsJson);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_GetCustomerInfo(string callbackId);

        [DllImport("__Internal")]
        internal static extern string _SuperwallBridge_GetSubscriptionStatus();

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_SetSubscriptionStatus(string statusJson);

        [DllImport("__Internal")]
        internal static extern string _SuperwallBridge_GetConfigurationStatus();

        [DllImport("__Internal")]
        internal static extern bool _SuperwallBridge_GetIsConfigured();

        [DllImport("__Internal")]
        internal static extern bool _SuperwallBridge_GetIsPaywallPresented();

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_PreloadAllPaywalls();

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_PreloadPaywallsForPlacements(string placementNamesJson);

        [DllImport("__Internal")]
        internal static extern bool _SuperwallBridge_HandleDeepLink(string url);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_TogglePaywallSpinner(bool isHidden);

        [DllImport("__Internal")]
        internal static extern string _SuperwallBridge_GetLatestPaywallInfo();

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_RegisterPlacement(string placement, string paramsJson, string handlerId, string featureId, string callbackId);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_Dismiss();

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_GetPresentationResult(string placement, string paramsJson, string callbackId);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_ConfirmAllAssignments(string callbackId);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_RestorePurchases(string callbackId);

        [DllImport("__Internal")]
        internal static extern string _SuperwallBridge_GetOverrideProductsByName();

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_SetOverrideProductsByName(string productsJson);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_Consume(string purchaseToken, string callbackId);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_RespondToPurchaseController(string callbackId, string resultJson);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_RespondToRestorePurchases(string callbackId, string resultJson);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_Purchase(string productId, string callbackId);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_GetProducts(string productIdsJson, string callbackId);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_GetAssignments(string callbackId);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_ShowAlert(string alertJson);

        [DllImport("__Internal")]
        internal static extern void _SuperwallBridge_RefreshConfiguration();
    }
#endif
}
