using System;
using System.Collections.Generic;

namespace Superwall
{
    [Serializable]
    public class PaywallInfo
    {
        public string Identifier;
        public string Name;
        public Experiment Experiment;
        public List<string> ProductIds;
        public List<Product> Products;
        public string Url;
        public string PresentedByPlacementWithName;
        public string PresentedByPlacementWithId;
        public string PresentedByPlacementAt;
        public string PresentedBy;
        public string PresentationSourceType;
        public string ResponseLoadStartTime;
        public string ResponseLoadCompleteTime;
        public string ResponseLoadFailTime;
        public double? ResponseLoadDuration;
        public string WebViewLoadStartTime;
        public string WebViewLoadCompleteTime;
        public string WebViewLoadFailTime;
        public double? WebViewLoadDuration;
        public string ProductsLoadStartTime;
        public string ProductsLoadCompleteTime;
        public string ProductsLoadFailTime;
        public double? ProductsLoadDuration;
        public string PaywalljsVersion;
        public bool? IsFreeTrialAvailable;
        public FeatureGatingBehavior? FeatureGatingBehavior;
        public PaywallCloseReason? CloseReason;
        public List<LocalNotification> LocalNotifications;
        public List<ComputedPropertyRequest> ComputedPropertyRequests;
        public List<Survey> Surveys;
        public Dictionary<string, object> State;
    }

    [Serializable]
    public class Product
    {
        public string Id;
        public string Name;
        public List<Entitlement> Entitlements;
    }

    [Serializable]
    public class LocalNotification
    {
        public string Id;
        public LocalNotificationType Type;
        public string Title;
        public string Subtitle;
        public string Body;
        public int Delay;
    }

    [Serializable]
    public class ComputedPropertyRequest
    {
        public ComputedPropertyRequestType Type;
        public string EventName;
    }

    [Serializable]
    public class Survey
    {
        public string Id;
        public string AssignmentKey;
        public string Title;
        public string Message;
        public List<SurveyOption> Options;
        public SurveyShowCondition PresentationCondition;
        public double PresentationProbability;
        public bool IncludeOtherOption;
        public bool IncludeCloseOption;
    }

    [Serializable]
    public class SurveyOption
    {
        public string Id;
        public string Text;
    }
}
