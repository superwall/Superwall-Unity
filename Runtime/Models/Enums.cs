using System;

namespace Superwall
{
    [Serializable]
    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error,
        None
    }

    [Serializable]
    public enum LogScope
    {
        LocalizationManager,
        BounceButton,
        CoreData,
        ConfigManager,
        IdentityManager,
        DebugManager,
        DebugViewController,
        LocalizationViewController,
        GameControllerManager,
        Device,
        Network,
        PaywallEvents,
        ProductsManager,
        StoreKitManager,
        Placements,
        Receipts,
        SuperwallCore,
        PaywallPresentation,
        Transactions,
        PaywallViewController,
        Cache,
        All,
        // Appended, never reordered. Each is applied only on the platform whose SDK has it and ignored on the other.
        /// <summary>iOS only.</summary>
        Analytics,
        WebEntitlements,
        /// <summary>iOS only.</summary>
        GrantedEntitlements,
        CustomerCenter,
        /// <summary>Android only.</summary>
        CustomerInfo,
        /// <summary>Android only.</summary>
        JsEvaluator,
        /// <summary>Android only.</summary>
        PaywallTransactions,
        /// <summary>Android only.</summary>
        NativePurchaseController,
        /// <summary>Android only.</summary>
        DeepLinks,
        /// <summary>Android only. The same scope <see cref="StoreKitManager"/> turns on there.</summary>
        Billing
    }

    [Serializable]
    public enum NetworkEnvironment
    {
        Release,
        ReleaseCandidate,
        Developer,
        /// <summary>iOS only. A Superwall backend running on localhost. Android has no such environment and uses <see cref="Release"/>.</summary>
        Local,
        /// <summary>The environment described by <see cref="SuperwallOptions.CustomNetworkEnvironment"/>.</summary>
        Custom
    }

    [Serializable]
    public enum TestModeBehavior
    {
        Automatic,
        WhenEnabledForUser,
        Never,
        Always
    }

    [Serializable]
    public enum FeatureGatingBehavior
    {
        Gated,
        NonGated
    }

    [Serializable]
    public enum PaywallCloseReason
    {
        SystemLogic,
        ForNextPaywall,
        WebViewFailedToLoad,
        ManualClose,
        None
    }

    [Serializable]
    public enum LocalNotificationType
    {
        TrialStarted,
        Unsupported
    }

    [Serializable]
    public enum ComputedPropertyRequestType
    {
        MinutesSince,
        HoursSince,
        DaysSince,
        MonthsSince,
        YearsSince,
        PlacementsInHour,
        PlacementsInDay,
        PlacementsInWeek,
        PlacementsInMonth,
        PlacementsSinceInstall
    }

    [Serializable]
    public enum SurveyShowCondition
    {
        OnManualClose,
        OnPurchase
    }

    [Serializable]
    public enum TransactionBackgroundView
    {
        Spinner,
        None
    }

    [Serializable]
    public enum ConfigurationStatus
    {
        Pending,
        Configured,
        Failed
    }

    [Serializable]
    public enum VariantType
    {
        Treatment,
        Holdout
    }

    [Serializable]
    public enum PaywallSkippedReason
    {
        Holdout,
        NoAudienceMatch,
        PlacementNotFound,
        /// <summary>Android only. The user is subscribed and the placement's paywall is only shown to non-subscribers.</summary>
        UserIsSubscribed
    }

    [Serializable]
    public enum ProductStore
    {
        AppStore,
        Stripe,
        Paddle,
        PlayStore,
        Superwall,
        Other,
        /// <summary>A product sold through your own payment system: its store is set to Custom on the dashboard.</summary>
        Custom
    }

    [Serializable]
    public enum EntitlementType
    {
        ServiceLevel
    }

    [Serializable]
    public enum LatestSubscriptionState
    {
        InGracePeriod,
        Subscribed,
        Expired,
        InBillingRetryPeriod,
        Revoked,
        /// <summary>Android only. The store reported a state the SDK doesn't know.</summary>
        Unknown
    }

    [Serializable]
    public enum LatestSubscriptionOfferType
    {
        Trial,
        Code,
        Promotional,
        Winback,
        /// <summary>Android only. A regular paid period, with no offer applied.</summary>
        Subscription,
        /// <summary>Android only. The latest period was revoked.</summary>
        Revoked
    }

    [Serializable]
    public enum PaywallPresentationRequestStatusType
    {
        Presentation,
        NoPresentation,
        Timeout
    }

    [Serializable]
    public enum IntegrationAttribute
    {
        AdjustId,
        AmplitudeDeviceId,
        AmplitudeUserId,
        AppsflyerId,
        BrazeAliasName,
        BrazeAliasLabel,
        OnesignalId,
        FbAnonId,
        FirebaseAppInstanceId,
        IterableUserId,
        IterableCampaignId,
        IterableTemplateId,
        MixpanelDistinctId,
        MparticleId,
        ClevertapId,
        AirshipChannelId,
        KochavaDeviceId,
        TenjinId,
        PosthogUserId,
        CustomerioId,
        AppstackId,
        // Appended, never reordered. Each is set only on the platform whose SDK has it and ignored on the other.
        /// <summary>iOS only.</summary>
        FirebaseInstallationId,
        SingularDeviceId,
        AirbridgeId,
        /// <summary>Android only.</summary>
        Meta,
        /// <summary>Android only.</summary>
        Amplitude,
        /// <summary>Android only.</summary>
        Mixpanel,
        /// <summary>Android only.</summary>
        GoogleAds,
        /// <summary>Android only.</summary>
        GoogleAppSetId,
        /// <summary>Android only.</summary>
        Custom
    }

    [Serializable]
    public enum EventType
    {
        FirstSeen,
        AppOpen,
        AppLaunch,
        IdentityAlias,
        AppInstall,
        RestoreStart,
        RestoreComplete,
        RestoreFail,
        SessionStart,
        DeviceAttributes,
        SubscriptionStatusDidChange,
        AppClose,
        DeepLink,
        TriggerFire,
        PaywallOpen,
        PaywallClose,
        PaywallDecline,
        TransactionStart,
        TransactionFail,
        TransactionAbandon,
        TransactionComplete,
        SubscriptionStart,
        FreeTrialStart,
        TransactionRestore,
        TransactionTimeout,
        UserAttributes,
        NonRecurringProductPurchase,
        PaywallResponseLoadStart,
        PaywallResponseLoadNotFound,
        PaywallResponseLoadFail,
        PaywallResponseLoadComplete,
        PaywallWebviewLoadStart,
        PaywallWebviewLoadFail,
        PaywallWebviewLoadComplete,
        PaywallWebviewLoadTimeout,
        PaywallWebviewLoadFallback,
        PaywallProductsLoadRetry,
        PaywallProductsLoadStart,
        PaywallProductsLoadFail,
        PaywallProductsLoadComplete,
        PaywallPreloadStart,
        PaywallPreloadComplete,
        PaywallResourceLoadFail,
        SurveyResponse,
        PaywallPresentationRequest,
        TouchesBegan,
        SurveyClose,
        Reset,
        ConfigRefresh,
        CustomPlacement,
        ConfigAttributes,
        ConfirmAllAssignments,
        ConfigFail,
        AdServicesTokenRequestStart,
        AdServicesTokenRequestFail,
        AdServicesTokenRequestComplete,
        ShimmerViewStart,
        ShimmerViewComplete,
        RedemptionStart,
        RedemptionComplete,
        RedemptionFail,
        EnrichmentStart,
        EnrichmentComplete,
        EnrichmentFail,
        NetworkDecodingFail,
        PaywallWebviewProcessTerminated,
        PaywallProductsLoadMissingProducts,
        CustomerInfoDidChange,
        IntegrationAttributes,
        ReviewRequested,
        PermissionRequested,
        PermissionGranted,
        PermissionDenied,
        // Names both native SDKs emit (SuperwallKit 4.16 / superwall-android 2.7) that had no member here,
        // so they were reported with a warning and left at the default. Appended, never reordered: the
        // enum is serialized by name, not by value, but consumers may still persist the ordinal.
        PaywallPageView,
        TestModeModalOpen,
        TestModeModalClose,
        CelExpressionResult,
        ErrorThrown,
        ReviewGranted,
        ReviewDenied,
        AttributionMatch,
        StripeCheckoutStart,
        StripeCheckoutSubmit,
        StripeCheckoutComplete,
        StripeCheckoutFail,
        // SuperwallKit 4.18 / superwall-android 2.10 (teleport events are iOS only).
        TeleportOpen,
        TeleportReturn,
        CustomerCenterOpen,
        CustomerCenterClose,
        CustomerCenterAction,
        CustomerCenterSurveyResponse,
        CustomerCenterRefundRequest
    }

    /// <summary>Which events the SDK sends to Superwall.</summary>
    [Serializable]
    public enum EventTrackingBehavior
    {
        /// <summary>Every event. The default.</summary>
        All,
        /// <summary>Only Superwall's own events: no events you track, trigger fires or user attribute updates.</summary>
        SuperwallOnly,
        /// <summary>No events at all, for example when the user declines data collection.</summary>
        None
    }

    [Serializable]
    public enum AdConsentStatus
    {
        Granted,
        Denied
    }

    /// <summary>Whether paywalls show in light or dark mode, regardless of the system setting.</summary>
    [Serializable]
    public enum InterfaceStyle
    {
        Light,
        Dark
    }

    /// <summary>iOS only. The StoreKit version SuperwallKit uses.</summary>
    [Serializable]
    public enum StoreKitVersion
    {
        StoreKit1,
        StoreKit2
    }

    /// <summary>iOS only. What the purchase in a transaction complete event was.</summary>
    [Serializable]
    public enum TransactionType
    {
        NonRecurringProductPurchase,
        FreeTrialStart,
        SubscriptionStart
    }

    [Serializable]
    public enum CustomCallbackResultStatus
    {
        Success,
        Failure
    }

    /// <summary>How a paywall is presented, as set on the dashboard.</summary>
    [Serializable]
    public enum PaywallPresentationStyle
    {
        Modal,
        Fullscreen,
        FullscreenNoAnimation,
        Push,
        /// <summary>Slides up to cover part of the screen. See <see cref="PaywallPresentationInfo.Height"/> and <see cref="PaywallPresentationInfo.CornerRadius"/>.</summary>
        Drawer,
        /// <summary>Pops over the screen. See <see cref="PaywallPresentationInfo.Height"/>, <see cref="PaywallPresentationInfo.Width"/> and <see cref="PaywallPresentationInfo.CornerRadius"/>.</summary>
        Popup,
        None
    }

    /// <summary>iOS only. Whether the paywall treats the user as eligible for introductory offers.</summary>
    [Serializable]
    public enum IntroOfferEligibility
    {
        Eligible,
        Ineligible,
        /// <summary>The store decides.</summary>
        Automatic
    }

    /// <summary>iOS only. The App Store billing plan of a product.</summary>
    [Serializable]
    public enum BillingPlanType
    {
        UpFront,
        Monthly
    }

    /// <summary>Android only. Which Google Play offer a paywall product uses.</summary>
    [Serializable]
    public enum PlayStoreOfferType
    {
        /// <summary>The SDK picks the best offer the user is eligible for.</summary>
        Automatic,
        /// <summary>The base plan, with no offer.</summary>
        NoOffer,
        /// <summary>The offer named by <see cref="PlayStoreProduct.OfferIdentifier"/>.</summary>
        Specified
    }

    [Serializable]
    public enum SubscriptionPeriodUnit
    {
        Day,
        Week,
        Month,
        Year
    }

    /// <summary>iOS only. How a discount is paid for.</summary>
    [Serializable]
    public enum DiscountPaymentMode
    {
        PayAsYouGo,
        PayUpFront,
        FreeTrial
    }

    /// <summary>iOS only.</summary>
    [Serializable]
    public enum DiscountType
    {
        Introductory,
        Promotional
    }

    /// <summary>Who matched an install to an ad, for the attribution match event.</summary>
    [Serializable]
    public enum AttributionMatchProvider
    {
        Mmp,
        /// <summary>iOS only.</summary>
        AppleSearchAds
    }

    [Serializable]
    public enum AttributionMatchConfidence
    {
        High,
        Medium,
        Low
    }

    /// <summary>Why a paywall presentation request ended as it did, for the paywall presentation request event.</summary>
    [Serializable]
    public enum PaywallPresentationRequestStatusReasonType
    {
        DebuggerPresented,
        PaywallAlreadyPresented,
        /// <summary>The user is in a holdout group. See <see cref="PaywallPresentationRequestStatusReason.Experiment"/>.</summary>
        Holdout,
        NoAudienceMatch,
        PlacementNotFound,
        NoPaywallViewController,
        NoPresenter,
        NoConfig,
        SubscriptionStatusTimeout
    }

    /// <summary>Android only. The SDK's estimate of how capable the device is.</summary>
    [Serializable]
    public enum DeviceTier
    {
        UltraLow,
        Low,
        Mid,
        High,
        UltraHigh,
        Unknown
    }
}
