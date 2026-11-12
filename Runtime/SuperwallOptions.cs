using System;
using System.Collections.Generic;

namespace Superwall
{
    [Serializable]
    public class SuperwallOptions
    {
        public PaywallOptions Paywalls = new PaywallOptions();
        public NetworkEnvironment NetworkEnvironment = NetworkEnvironment.Release;
        /// <summary>
        /// Deprecated: use <see cref="EventTrackingBehavior"/>, which also has a setting for sending no events at all.
        /// <c>false</c> means <c>EventTrackingBehavior.SuperwallOnly</c>. Ignored when
        /// <see cref="EventTrackingBehavior"/> is set.
        /// </summary>
        [Obsolete("Use EventTrackingBehavior instead.")]
        public bool IsExternalDataCollectionEnabled = true;
        /// <summary>
        /// Which events the SDK sends to Superwall. Null, the default, keeps the native default (all events), or what
        /// <see cref="IsExternalDataCollectionEnabled"/> asks for. Change it later with
        /// <see cref="Superwall.EventTrackingBehavior"/>.
        /// </summary>
        public EventTrackingBehavior? EventTrackingBehavior;
        /// <summary>
        /// The user's consent for advertising use of their data, passed on with the conversions Superwall uploads to
        /// Google Ads and Meta. Null, the default, leaves it unset: the SDKs then use the answer stored by an IAB TCF
        /// consent banner when GDPR applies, and otherwise report it as granted. Change it later with
        /// <see cref="Superwall.AdConsent"/>.
        /// </summary>
        public AdConsent AdConsent;
        /// <summary>
        /// The Customer Center that <see cref="Superwall.PresentCustomerCenter"/> shows when it isn't given one. Null,
        /// the default, uses <see cref="CustomerCenterConfiguration.Default"/>.
        /// </summary>
        public CustomerCenterConfiguration CustomerCenter;
        /// <summary>iOS only. Null, the default, keeps SuperwallKit's default, StoreKit 2.</summary>
        public StoreKitVersion? StoreKitVersion;
        /// <summary>Enables experimental device variables, which are subject to change. Defaults to false.</summary>
        public bool EnableExperimentalDeviceVariables = false;
        public string LocaleIdentifier;
        public bool IsGameControllerEnabled = false;
        public Logging Logging = new Logging();
        public bool PassIdentifiersToPlayStore = false;
        public TestModeBehavior TestModeBehavior = TestModeBehavior.Automatic;
        public bool ShouldObservePurchases = false;
        public bool ShouldBypassAppTransactionCheck = false;
        public int MaxConfigRetryCount = 6;
        public bool UseMockReviews = false;
    }

    [Serializable]
    public class PaywallOptions
    {
        public bool IsHapticFeedbackEnabled = true;
        public RestoreFailed RestoreFailed = new RestoreFailed();
        public bool ShouldShowPurchaseFailureAlert = true;
        public bool ShouldPreload = true;
        public bool AutomaticallyDismiss = true;
        public bool ShouldShowWebRestorationAlert = true;
        public TransactionBackgroundView TransactionBackgroundView = TransactionBackgroundView.Spinner;
        public Dictionary<string, string> OverrideProductsByName;
        /// <summary>
        /// iOS only. Whether to show an alert confirming a purchase made on the web. Defaults to true, as in SuperwallKit.
        /// </summary>
        public bool ShouldShowWebPurchaseConfirmationAlert = true;
        public bool UseCachedTemplates = false;
        public float? TimeoutAfter = null;
        /// <summary>
        /// Android only. Where paywalls are shown. The default, <see cref="AndroidPaywallPresentation.InWindow"/>,
        /// shows them inside the game's activity so Unity keeps running while a paywall is up, which a C#
        /// purchase controller needs to receive purchase requests. <see cref="AndroidPaywallPresentation.Activity"/>
        /// opens them in their own activity, which pauses Unity until the paywall closes.
        /// </summary>
        public AndroidPaywallPresentation AndroidPresentation = AndroidPaywallPresentation.InWindow;
        /// <summary>
        /// iOS only. The alert shown when a paywall asks for notification permission the user has denied. Null, the
        /// default, shows no alert.
        /// </summary>
        public NotificationPermissionsDenied NotificationPermissionsDenied;
        /// <summary>
        /// Android only. Shows the paywall before its products have loaded instead of waiting for them. Defaults to false.
        /// </summary>
        public bool OptimisticLoading = false;
    }

    /// <summary>iOS only. The texts of the alert shown when notification permission is denied.</summary>
    [Serializable]
    public class NotificationPermissionsDenied
    {
        public string Title = "Notification Permissions Denied";
        public string Message = "Please enable notification permissions from the Settings app so we can notify you when your free trial ends.";
        public string ActionButtonTitle = "Open Settings";
        public string CloseButtonTitle = "Not now";
    }

    /// <summary>
    /// The user's consent for advertising use of their data. Both default to <see cref="AdConsentStatus.Granted"/>.
    /// </summary>
    [Serializable]
    public class AdConsent
    {
        /// <summary>Consent to send user data to ad networks for advertising.</summary>
        public AdConsentStatus AdUserData = AdConsentStatus.Granted;
        /// <summary>Consent for them to use that data for personalized advertising.</summary>
        public AdConsentStatus AdPersonalization = AdConsentStatus.Granted;
    }

    public enum AndroidPaywallPresentation
    {
        /// <summary>Paywalls are shown as an overlay inside the game's activity. The default.</summary>
        InWindow,
        /// <summary>Paywalls open in their own Android activity, which pauses Unity while shown.</summary>
        Activity
    }

    [Serializable]
    public class RestoreFailed
    {
        public string Title = "No Subscription Found";
        public string Message = "We couldn't find an active subscription for your account.";
        public string CloseButtonTitle = "Okay";
    }

    [Serializable]
    public class Logging
    {
        public LogLevel Level = LogLevel.Warn;
        public List<LogScope> Scopes = new List<LogScope>();
    }
}
