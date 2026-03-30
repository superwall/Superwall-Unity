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
    }
}
