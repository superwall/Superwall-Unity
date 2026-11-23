using System;
using System.Collections.Generic;

namespace Superwall
{
    [Serializable]
    public class SuperwallOptions
    {
        public PaywallOptions Paywalls = new PaywallOptions();
        /// <summary>
        /// Which Superwall backend the SDK talks to. Leave it at <see cref="NetworkEnvironment.Release"/> unless the
        /// Superwall team tells you otherwise.
        /// </summary>
        public NetworkEnvironment NetworkEnvironment = NetworkEnvironment.Release;
        /// <summary>The backend used when <see cref="NetworkEnvironment"/> is <see cref="NetworkEnvironment.Custom"/>. Ignored otherwise.</summary>
        public CustomNetworkEnvironment CustomNetworkEnvironment;
        /// <summary>
        /// iOS only, development builds only. Connects the SDK to a running <c>superwall dev</c> server, which serves the
        /// paywalls you're editing, turns on test mode and turns off paywall preloading. Null, the default, connects
        /// to none. The app's Info.plist must allow local networking (<c>NSAllowsLocalNetworking</c>). The Android
        /// SDK has no dev server support, so this does nothing there.
        /// </summary>
        public DevServer DevServer;
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
        /// <summary>
        /// Android only. Overrides <see cref="ShouldPreload"/> for devices of a given tier, for example to skip
        /// preloading on <see cref="DeviceTier.UltraLow"/> and <see cref="DeviceTier.Low"/> devices. Null, the
        /// default, overrides nothing.
        /// </summary>
        public Dictionary<DeviceTier, bool> PreloadDeviceOverrides;
        /// <summary>
        /// Android only. The colour of the paywall's loading spinner, as the name of a colour resource in the app
        /// (<c>res/values/colors.xml</c> of an Android library or the launcher module), without the <c>R.color.</c>
        /// prefix. The Android SDK only takes a colour resource here, so a hex value can't be passed. Null, the
        /// default, keeps the SDK's colour. A name that isn't found is ignored with a warning.
        /// </summary>
        public string LoadingColorResourceName;
        /// <summary>
        /// Android only. Called when the user presses back on a paywall that has "Reroute back button" turned on in
        /// its settings on the dashboard; the SDK doesn't ask for other paywalls. While this is set the press
        /// doesn't dismiss such a paywall: call <c>Superwall.Shared.Dismiss()</c> from here to close it. The
        /// paywall's info is null if it isn't known yet. With <see cref="AndroidPaywallPresentation.Activity"/>
        /// Unity is paused while the paywall is up, so the press keeps its default behaviour and this runs once
        /// Unity resumes.
        /// </summary>
        public Action<PaywallInfo> OnBackPressed;
    }

    /// <summary>
    /// The backend used with <see cref="NetworkEnvironment.Custom"/>. Only for use when the Superwall team asks for it.
    /// </summary>
    [Serializable]
    public class CustomNetworkEnvironment
    {
        /// <summary>
        /// The API's domain or URL, such as <c>https://api.example.com:8443</c>. This is all iOS takes: its scheme,
        /// host and port come from here. On Android it supplies <see cref="BaseHost"/>, <see cref="Scheme"/> and
        /// <see cref="Port"/> when they aren't set.
        /// </summary>
        public string Domain;
        /// <summary>Android only. The API host. Defaults to the host of <see cref="Domain"/>.</summary>
        public string BaseHost;
        /// <summary>Android only. The events collector host. Defaults to <c>collector.superwall.dev</c>, as on iOS.</summary>
        public string CollectorHost;
        /// <summary>Android only. <c>https</c> or <c>http</c>. Defaults to the scheme of <see cref="Domain"/>, or <c>https</c>.</summary>
        public string Scheme;
        /// <summary>Android only. Defaults to the port of <see cref="Domain"/>, if it has one.</summary>
        public int? Port;
        /// <summary>Android only. Null keeps the SDK's default.</summary>
        public string SubscriptionHost;
        /// <summary>Android only. Null keeps the SDK's default.</summary>
        public string EnrichmentHost;
        /// <summary>Android only. Null keeps the SDK's default.</summary>
        public string MmpHost;
    }

    /// <summary>iOS only. The <c>superwall dev</c> server <see cref="SuperwallOptions.DevServer"/> connects to.</summary>
    [Serializable]
    public class DevServer
    {
        /// <summary>
        /// The server's address: the origin of the <c>Device</c> URL that <c>superwall dev</c> prints, such as
        /// <c>http://192.168.1.10:6100</c>. Needed on a physical device. Null finds the server on localhost, which
        /// works from a simulator.
        /// </summary>
        public string Url;

        /// <summary>The dev server on this machine, found on localhost. For the simulator.</summary>
        public static DevServer Default => new DevServer();

        /// <summary>The dev server at an exact address. For a physical device.</summary>
        public static DevServer At(string url) => new DevServer { Url = url };
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
