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
