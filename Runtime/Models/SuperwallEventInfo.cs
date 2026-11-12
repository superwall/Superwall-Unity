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
    }
}
