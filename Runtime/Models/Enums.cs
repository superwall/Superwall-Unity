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
        All
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
        PlacementNotFound
    }

    [Serializable]
    public enum ProductStore
    {
        AppStore,
        Stripe,
        Paddle,
        PlayStore,
        Superwall,
        Other
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
        AppstackId
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
        PermissionDenied
    }

    [Serializable]
    public enum CustomCallbackResultStatus
    {
        Success,
        Failure
    }
}
