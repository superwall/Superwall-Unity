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
        Developer
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
        Revoked
    }

    [Serializable]
    public enum LatestSubscriptionOfferType
    {
        Trial,
        Code,
        Promotional,
        Winback
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

    [Serializable]
    public enum CustomCallbackResultStatus
    {
        Success,
        Failure
    }
}
