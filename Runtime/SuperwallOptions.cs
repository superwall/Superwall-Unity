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
        public bool ShouldShowWebPurchaseConfirmationAlert = false;
        public bool UseCachedTemplates = false;
        public float? TimeoutAfter = null;

        /// <summary>
        /// Android only. Hides the status and navigation bars while a Superwall paywall Activity is in the
        /// foreground (sticky immersive: a swipe from the edge shows them transiently), so a paywall matches an
        /// app that already runs full-screen. Without it the paywall Activity is edge-to-edge but pads only the
        /// bottom system-bar inset, so in landscape the 3-button navigation bar covers paywall content on one
        /// side. Ignored on iOS. Default false to keep the platform default for existing apps.
        /// </summary>
        public bool HideAndroidSystemBars = false;
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
