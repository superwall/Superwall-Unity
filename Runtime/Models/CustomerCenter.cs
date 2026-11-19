using System;
using System.Collections.Generic;

namespace Superwall
{
    /// <summary>What a Customer Center path does, and the action reported when the user taps one.</summary>
    [Serializable]
    public enum CustomerCenterActionType
    {
        Restore,
        ManageSubscription,
        Refund,
        ChangePlan,
        ContactSupport,
        Url,
        Custom
    }

    /// <summary>How a URL path in the Customer Center opens.</summary>
    [Serializable]
    public enum CustomerCenterOpenMethod
    {
        /// <summary>In an in-app browser. The default.</summary>
        InApp,
        /// <summary>Handed to whichever app handles the URL.</summary>
        External
    }

    /// <summary>
    /// How a refund request made from the Customer Center ended. Android can't see how one ends on Google Play's
    /// pages: there <see cref="Success"/> means the request was handed to Google Play, and
    /// <see cref="UserCancelled"/> is never reported.
    /// </summary>
    [Serializable]
    public enum CustomerCenterRefundStatus
    {
        Success,
        UserCancelled,
        Error
    }

    /// <summary>Which Customer Center screen was shown.</summary>
    [Serializable]
    public enum CustomerCenterScreenType
    {
        /// <summary>The user has, or had, at least one purchase.</summary>
        Management,
        /// <summary>The user has no purchases.</summary>
        NoPurchases
    }

    /// <summary>iOS only. How the Customer Center was put on screen.</summary>
    [Serializable]
    public enum CustomerCenterPresentationStyle
    {
        Sheet,
        Embedded
    }

    /// <summary>
    /// The screens, actions, support options and appearance of the Customer Center. Set the default with
    /// <see cref="SuperwallOptions.CustomerCenter"/>, or pass one to <see cref="Superwall.PresentCustomerCenter"/>.
    /// </summary>
    [Serializable]
    public class CustomerCenterConfiguration
    {
        /// <summary>The screen shown when the user has at least one subscription (active or expired) or purchase.</summary>
        public CustomerCenterScreen ManagementScreen;
        /// <summary>The screen shown when the user has no purchases at all.</summary>
        public CustomerCenterScreen NoPurchasesScreen;
        public CustomerCenterSupport Support = new CustomerCenterSupport();
        /// <summary>Tints buttons and links, as <c>#RRGGBB</c> or <c>#RRGGBBAA</c> hex. Null uses the default.</summary>
        public CustomerCenterColorPair Accent;
        /// <summary>Shows the account details section (user ID, original download date). Defaults to true.</summary>
        public bool ShowsAccountDetails = true;
        /// <summary>Warns when both a store and a web subscription are active. Defaults to true.</summary>
        public bool WarnsAboutDuplicateSubscriptions = true;

        /// <summary>
        /// A new copy of the native SDKs' default: restore, change plan, refund, manage subscription (with the
        /// cancellation survey) and contact support on the management screen, and restore on the no-purchases screen.
        /// </summary>
        public static CustomerCenterConfiguration Default => new CustomerCenterConfiguration
        {
            ManagementScreen = new CustomerCenterScreen
            {
                Paths = new List<CustomerCenterPath>
                {
                    CustomerCenterPath.Restore(),
                    CustomerCenterPath.ChangePlan(),
                    CustomerCenterPath.Refund(),
                    CustomerCenterPath.ManageSubscription(survey: CustomerCenterSurvey.Cancellation),
                    CustomerCenterPath.ContactSupport(),
                }
            },
            NoPurchasesScreen = new CustomerCenterScreen
            {
                Paths = new List<CustomerCenterPath> { CustomerCenterPath.Restore() }
            },
        };
    }

    /// <summary>A Customer Center screen: a title, an optional subtitle and the paths it lists, in order.</summary>
    [Serializable]
    public class CustomerCenterScreen
    {
        /// <summary>Null uses the localized default for the screen.</summary>
        public string Title;
        /// <summary>Null uses the localized default on the no-purchases screen, and none on the management screen.</summary>
        public string Subtitle;
        public List<CustomerCenterPath> Paths = new List<CustomerCenterPath>();
    }

    /// <summary>A row in the Customer Center. Build one with the static methods, such as <see cref="Restore"/>.</summary>
    [Serializable]
    public class CustomerCenterPath
    {
        public CustomerCenterActionType Type;
        /// <summary>
        /// Reported as the path ID on Customer Center events and delegate calls. Null uses the native default: the
        /// type's name for the built-in types, the URL's host and path for a URL path, and the identifier for a
        /// custom path. Only needed to tell apart two paths that would otherwise share one.
        /// </summary>
        public string Id;
        /// <summary>Null uses the localized default for <see cref="Type"/>. A URL path needs one.</summary>
        public string Title;
        /// <summary>A survey shown before the path's action runs.</summary>
        public CustomerCenterSurvey Survey;
        /// <summary><see cref="CustomerCenterActionType.Refund"/>: how long after a purchase, in milliseconds, a refund may be requested. Null for no limit.</summary>
        public long? RefundWindowMillis;
        /// <summary><see cref="CustomerCenterActionType.ChangePlan"/>: the plans to offer. Null offers every plan.</summary>
        public List<string> ProductIds;
        /// <summary><see cref="CustomerCenterActionType.Url"/>: the URL to open.</summary>
        public string Url;
        /// <summary><see cref="CustomerCenterActionType.Url"/>: how the URL opens.</summary>
        public CustomerCenterOpenMethod OpenMethod = CustomerCenterOpenMethod.InApp;
        /// <summary><see cref="CustomerCenterActionType.Custom"/>: passed back in the action when the path is tapped.</summary>
        public string Identifier;

        public static CustomerCenterPath Restore(string id = null, string title = null, CustomerCenterSurvey survey = null) =>
            new CustomerCenterPath { Type = CustomerCenterActionType.Restore, Id = id, Title = title, Survey = survey };

        public static CustomerCenterPath ManageSubscription(string id = null, string title = null, CustomerCenterSurvey survey = null) =>
            new CustomerCenterPath { Type = CustomerCenterActionType.ManageSubscription, Id = id, Title = title, Survey = survey };

        public static CustomerCenterPath Refund(long? windowMillis = null, string id = null, string title = null, CustomerCenterSurvey survey = null) =>
            new CustomerCenterPath { Type = CustomerCenterActionType.Refund, RefundWindowMillis = windowMillis, Id = id, Title = title, Survey = survey };

        public static CustomerCenterPath ChangePlan(List<string> productIds = null, string id = null, string title = null, CustomerCenterSurvey survey = null) =>
            new CustomerCenterPath { Type = CustomerCenterActionType.ChangePlan, ProductIds = productIds, Id = id, Title = title, Survey = survey };

        public static CustomerCenterPath ContactSupport(string id = null, string title = null, CustomerCenterSurvey survey = null) =>
            new CustomerCenterPath { Type = CustomerCenterActionType.ContactSupport, Id = id, Title = title, Survey = survey };

        /// <param name="title">What the row says. Without one the row shows the URL's host, the same for every link to one site.</param>
        public static CustomerCenterPath ForUrl(string url, string title, CustomerCenterOpenMethod openMethod = CustomerCenterOpenMethod.InApp,
            string id = null, CustomerCenterSurvey survey = null) =>
            new CustomerCenterPath { Type = CustomerCenterActionType.Url, Url = url, Title = title, OpenMethod = openMethod, Id = id, Survey = survey };

        public static CustomerCenterPath Custom(string identifier, string title = null, string id = null, CustomerCenterSurvey survey = null) =>
            new CustomerCenterPath { Type = CustomerCenterActionType.Custom, Identifier = identifier, Title = title, Id = id, Survey = survey };
    }

    /// <summary>A single-choice survey shown before a path's action runs.</summary>
    [Serializable]
    public class CustomerCenterSurvey
    {
        public string Id;
        /// <summary>
        /// The question. Null uses the localized "Why are you cancelling?" on a manage-subscription path, and no
        /// question on any other path.
        /// </summary>
        public string Title;
        public List<CustomerCenterSurveyOption> Options = new List<CustomerCenterSurveyOption>();

        /// <summary>The built-in cancellation survey the default configuration uses, with its three localized options.</summary>
        public static CustomerCenterSurvey Cancellation => new CustomerCenterSurvey
        {
            Id = "cancel_survey",
            Options = new List<CustomerCenterSurveyOption>
            {
                CustomerCenterSurveyOption.TooExpensive,
                CustomerCenterSurveyOption.DontUse,
                CustomerCenterSurveyOption.BoughtByMistake,
            }
        };
    }

    [Serializable]
    public class CustomerCenterSurveyOption
    {
        public string Id;
        /// <summary>Null uses the localized text of the built-in options, and no text for any other.</summary>
        public string Title;

        /// <summary>"Too expensive", localized.</summary>
        public static CustomerCenterSurveyOption TooExpensive => new CustomerCenterSurveyOption { Id = "too_expensive" };
        /// <summary>"Don't use the app", localized.</summary>
        public static CustomerCenterSurveyOption DontUse => new CustomerCenterSurveyOption { Id = "dont_use" };
        /// <summary>"Bought by mistake", localized.</summary>
        public static CustomerCenterSurveyOption BoughtByMistake => new CustomerCenterSurveyOption { Id = "bought_by_mistake" };
    }

    [Serializable]
    public class CustomerCenterSupport
    {
        /// <summary>The address the contact support path writes to. Null hides that path.</summary>
        public string Email;
        /// <summary>The latest published app version. When it's newer than the installed one, an update banner shows.</summary>
        public string LatestAppVersion;
        /// <summary>Whether to show the update banner. Defaults to true.</summary>
        public bool WarnsAboutUpdates = true;
        /// <summary>Overrides the page where web subscriptions are managed.</summary>
        public string WebManagementUrl;
        /// <summary>iOS only. Looks the latest version up on the App Store when <see cref="LatestAppVersion"/> isn't set. Defaults to true.</summary>
        public bool ChecksAppStoreForUpdates = true;
    }

    /// <summary>A colour for light mode and one for dark mode, each as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>.</summary>
    [Serializable]
    public class CustomerCenterColorPair
    {
        public string Light;
        public string Dark;
    }

    /// <summary>The action the user selected in the Customer Center.</summary>
    [Serializable]
    public class CustomerCenterAction
    {
        public CustomerCenterActionType Type;
        /// <summary>The URL, for <see cref="CustomerCenterActionType.Url"/>.</summary>
        public string Url;
        /// <summary>The path's identifier, for <see cref="CustomerCenterActionType.Custom"/>.</summary>
        public string Identifier;
    }

    /// <summary>The purchase a Customer Center action applies to.</summary>
    [Serializable]
    public class CustomerCenterPurchase
    {
        /// <summary>Null for an entitlement with no product behind it, such as one granted by hand.</summary>
        public string ProductId;
        public ProductStore? Store;
        /// <summary>
        /// Every entitlement the purchase unlocks or has unlocked, so check <see cref="Entitlement.IsActive"/>
        /// before treating one as current.
        /// </summary>
        public List<Entitlement> Entitlements;
        /// <summary>The subscription, when the purchase is one.</summary>
        public SubscriptionTransaction Subscription;
        /// <summary>The one-time purchase, when the purchase is one.</summary>
        public NonSubscriptionTransaction NonSubscription;
    }
}
