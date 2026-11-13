using System;
using System.Collections.Generic;

namespace Superwall
{
    [Serializable]
    public class SuperwallEventInfo
    {
        public EventType EventType;
        public Dictionary<string, object> Params;
        public string PlacementName;
        public Dictionary<string, object> DeviceAttributes;
        public string DeepLinkUrl;
        public TriggerResult Result;
        public PaywallInfo PaywallInfo;
        public StoreTransaction Transaction;
        public StoreProduct Product;
        public string Error;
        public string TriggeredPlacementName;
        public int? Attempt;
        public string Name;
        public Survey Survey;
        public SurveyOption SelectedOption;
        public string CustomResponse;
        public PaywallPresentationRequestStatusType? Status;
        public string RestoreType;
        public Dictionary<string, object> UserAttributes;
        public string Token;
        public Dictionary<string, object> UserEnrichment;
        public Dictionary<string, object> DeviceEnrichment;
        public string Message;
        public Dictionary<string, object> IntegrationAttributes;
        public int? ReviewRequestedCount;
        public List<string> MissingProductIdentifiers;
        /// <summary>The permission asked for, for the permission events.</summary>
        public string PermissionName;
        /// <summary>The paywall that asked, for the permission events.</summary>
        public string PaywallIdentifier;
        /// <summary>How many paywalls are being preloaded, for the paywall preload events.</summary>
        public int? PaywallCount;
        /// <summary>iOS only. What the purchase in a transaction complete event was.</summary>
        public TransactionType? TransactionType;
        /// <summary>The action, for the Customer Center action and survey response events.</summary>
        public CustomerCenterAction CustomerCenterAction;
        /// <summary>The path tapped, for the Customer Center action and survey response events.</summary>
        public string CustomerCenterPathId;
        /// <summary>The product a Customer Center event applies to, if any.</summary>
        public string CustomerCenterProductId;
        public string CustomerCenterSurveyId;
        public string CustomerCenterSurveyOptionId;
        public CustomerCenterRefundStatus? CustomerCenterRefundStatus;
        /// <summary>The screen shown, for the Customer Center open event.</summary>
        public CustomerCenterScreenType? CustomerCenterScreen;
        /// <summary>iOS only. How the Customer Center was shown, for its open event.</summary>
        public CustomerCenterPresentationStyle? CustomerCenterPresentationStyle;
        /// <summary>The result of matching the install to an ad, for the attribution match event.</summary>
        public AttributionMatchInfo AttributionMatchInfo;
        /// <summary>The page shown and how the user got there, for the paywall page view event.</summary>
        public PageViewData PageViewData;
        /// <summary>The product bought, for the non-recurring product purchase event. <see cref="Product"/> carries only its identifier there.</summary>
        public TransactionProduct TransactionProduct;
        /// <summary>Why the request ended as it did, for the paywall presentation request event. Null when a paywall is presented.</summary>
        public PaywallPresentationRequestStatusReason StatusReason;
        /// <summary>Android only. How long the shimmer was shown, in milliseconds, for the shimmer view complete event.</summary>
        public double? ShimmerDuration;
        /// <summary>Android only. The URL that failed to load, for the paywall resource load fail event. <see cref="Error"/> has the error.</summary>
        public string ResourceUrl;
        /// <summary>
        /// How many times a review has been requested, for the review requested event and, on Android, the review
        /// granted and denied events.
        /// </summary>
        public int? ReviewCount;
        /// <summary>The parameters the paywall passed, for the custom placement event.</summary>
        public Dictionary<string, object> PlacementParams;
        /// <summary>Android only. The customer info before the change, for the customer info did change event.</summary>
        public CustomerInfo PreviousCustomerInfo;
        /// <summary>Android only. The customer info after the change, for the customer info did change event.</summary>
        public CustomerInfo CustomerInfo;
    }

    /// <summary>The result of matching an install to an ad.</summary>
    [Serializable]
    public class AttributionMatchInfo
    {
        public AttributionMatchProvider Provider;
        /// <summary>Whether the provider matched the install.</summary>
        public bool Matched;
        /// <summary>The source the install was attributed to, if any.</summary>
        public string Source;
        public AttributionMatchConfidence? Confidence;
        /// <summary>The provider's score for a match. Higher is stronger.</summary>
        public double? MatchScore;
        /// <summary>Why there was no match, or why matching failed, if known.</summary>
        public string Reason;
    }

    /// <summary>A page of a multi-page paywall being shown.</summary>
    [Serializable]
    public class PageViewData
    {
        public string PageNodeId;
        /// <summary>The page's position in the flow.</summary>
        public int FlowPosition;
        public string PageName;
        public string NavigationNodeId;
        public string PreviousPageNodeId;
        public int? PreviousFlowPosition;
        /// <summary>How the user got to the page, such as <c>entry</c>, <c>forward</c> or <c>back</c>.</summary>
        public string NavigationType;
        public int? TimeOnPreviousPageMs;
    }

    /// <summary>The product of a non-recurring product purchase event.</summary>
    [Serializable]
    public class TransactionProduct
    {
        public string Id;
        public TransactionProductPrice Price;
        /// <summary>Null when the product has no trial.</summary>
        public TransactionProductTrialPeriod TrialPeriod;
        /// <summary>Null when the product isn't a subscription.</summary>
        public TransactionProductPeriod Period;
        public string Locale;
        public string LanguageCode;
        public TransactionProductCurrency Currency;
    }

    [Serializable]
    public class TransactionProductPrice
    {
        public double Raw;
        public string Localized;
        public string Daily;
        public string Weekly;
        public string Monthly;
        public string Yearly;
    }

    [Serializable]
    public class TransactionProductTrialPeriod
    {
        public int Days;
        public int Weeks;
        public int Months;
        public int Years;
        public string Text;
        /// <summary>When the trial ends, in epoch milliseconds, if known.</summary>
        public long? EndAt;
    }

    [Serializable]
    public class TransactionProductPeriod
    {
        /// <summary>The period as a word, such as <c>month</c>.</summary>
        public string Alt;
        /// <summary>The period as an adverb, such as <c>monthly</c>.</summary>
        public string Ly;
        public SubscriptionPeriodUnit Unit;
        public int Days;
        public int Weeks;
        public int Months;
        public int Years;
    }

    [Serializable]
    public class TransactionProductCurrency
    {
        public string Code;
        public string Symbol;
    }

    /// <summary>Why a paywall presentation request didn't present a paywall.</summary>
    [Serializable]
    public class PaywallPresentationRequestStatusReason
    {
        public PaywallPresentationRequestStatusReasonType Type;
        /// <summary>The experiment, when <see cref="Type"/> is <see cref="PaywallPresentationRequestStatusReasonType.Holdout"/>.</summary>
        public Experiment Experiment;
        /// <summary>Android only. The SDK's explanation of the reason.</summary>
        public string Info;
    }
}
