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
        /// <summary>Identifies the cached copy of the paywall.</summary>
        public string CacheKey;
        /// <summary>Identifies the build of the paywall.</summary>
        public string BuildId;
        public string ShimmerLoadStartTime;
        public string ShimmerLoadCompleteTime;
        /// <summary>Whether the paywall can be scrolled.</summary>
        public bool? IsScrollEnabled;
        /// <summary>Identifies this presentation of the paywall.</summary>
        public string PresentationId;
        /// <summary>iOS only. Whether the paywall was loaded from files bundled with the app.</summary>
        public bool? IsLocal;
        /// <summary>How the paywall is presented: its style, the delay before it shows, and the size of a drawer or popup.</summary>
        public PaywallPresentationInfo Presentation;
        /// <summary>iOS only. Whether the paywall treats the user as eligible for introductory offers.</summary>
        public IntroOfferEligibility? IntroOfferEligibility;
        /// <summary>Android only. The paywall's ID in Superwall's database.</summary>
        public string DatabaseId;
        /// <summary>Android only. The customer info the paywall was presented with.</summary>
        public CustomerInfo CustomerInfo;
    }

    /// <summary>How a paywall is presented.</summary>
    [Serializable]
    public class PaywallPresentationInfo
    {
        public PaywallPresentationStyle Style;
        /// <summary>The delay before the paywall is presented, in milliseconds.</summary>
        public long Delay;
        /// <summary>The height of a <see cref="PaywallPresentationStyle.Drawer"/> or <see cref="PaywallPresentationStyle.Popup"/>, as a percentage of the screen. Null for the other styles.</summary>
        public double? Height;
        /// <summary>The width of a <see cref="PaywallPresentationStyle.Popup"/>, as a percentage of the screen. Null for the other styles.</summary>
        public double? Width;
        /// <summary>The corner radius of a <see cref="PaywallPresentationStyle.Drawer"/> or <see cref="PaywallPresentationStyle.Popup"/>. Null for the other styles.</summary>
        public double? CornerRadius;
    }

    [Serializable]
    public class Product
    {
        /// <summary>
        /// The product's identifier. On Android this is the composite identifier, which includes the base plan and
        /// offer (<c>product:basePlan:offer</c>); on iOS it is the bare product identifier. Use
        /// <see cref="ProductIdentifier"/> for the bare identifier on both.
        /// </summary>
        public string Id;
        /// <summary>The product's identifier in its store, without base plan or offer, on both platforms.</summary>
        public string ProductIdentifier;
        public string Name;
        public List<Entitlement> Entitlements;
        /// <summary>The store the product is sold through.</summary>
        public ProductStore? Store;
        /// <summary>The product's details when <see cref="Store"/> is <see cref="ProductStore.AppStore"/>. Null otherwise.</summary>
        public AppStoreProduct AppStoreProduct;
        /// <summary>The product's details when <see cref="Store"/> is <see cref="ProductStore.Stripe"/>. Null otherwise.</summary>
        public StripeProduct StripeProduct;
        /// <summary>The product's details when <see cref="Store"/> is <see cref="ProductStore.Paddle"/>. Null otherwise.</summary>
        public PaddleProduct PaddleProduct;
        /// <summary>Android only. The product's details when <see cref="Store"/> is <see cref="ProductStore.PlayStore"/>. Null otherwise.</summary>
        public PlayStoreProduct PlayStoreProduct;
        /// <summary>The product's details when <see cref="Store"/> is <see cref="ProductStore.Custom"/>. Null otherwise.</summary>
        public CustomStoreProduct CustomProduct;
    }

    [Serializable]
    public class AppStoreProduct
    {
        public string Id;
        /// <summary>iOS only. The billing plan set for the product on the dashboard, if any.</summary>
        public BillingPlanType? BillingPlanType;
    }

    [Serializable]
    public class StripeProduct
    {
        public string Id;
        public int? TrialDays;
        /// <summary>Android only. The Stripe environment, such as <c>live</c> or <c>sandbox</c>.</summary>
        public string Environment;
    }

    [Serializable]
    public class PaddleProduct
    {
        public string Id;
        /// <summary>Android only.</summary>
        public int? TrialDays;
        /// <summary>Android only. The Paddle environment, such as <c>live</c> or <c>sandbox</c>.</summary>
        public string Environment;
    }

    /// <summary>Android only. A Google Play product, with the base plan and offer the paywall uses.</summary>
    [Serializable]
    public class PlayStoreProduct
    {
        public string Id;
        public string BasePlanIdentifier;
        public PlayStoreOfferType OfferType;
        /// <summary>The offer, when <see cref="OfferType"/> is <see cref="PlayStoreOfferType.Specified"/>.</summary>
        public string OfferIdentifier;
        /// <summary>The identifier with base plan and offer: <c>product:basePlan:offer</c>.</summary>
        public string FullIdentifier;
    }

    [Serializable]
    public class CustomStoreProduct
    {
        public string Id;
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
