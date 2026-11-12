using System;
using System.Collections.Generic;

namespace Superwall
{
    [Serializable]
    public class SuperwallOptions
    {
        public PaywallOptions Paywalls = new PaywallOptions();
        public NetworkEnvironment NetworkEnvironment = NetworkEnvironment.Release;
        public bool IsExternalDataCollectionEnabled = true;
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
