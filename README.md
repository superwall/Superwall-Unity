# Superwall SDK for Unity

Superwall lets you remotely configure paywalls, run A/B tests, and manage monetization — all without shipping app updates.

This package provides a Unity C# wrapper around the native [SuperwallKit](https://github.com/superwall/Superwall-iOS) (iOS) and [Superwall Android SDK](https://github.com/superwall/Superwall-Android).

## Requirements

- Unity 6+ (6000.4+)
- iOS 16.0+
- Android minSdk 26+

## Installation

In Unity, go to **Window > Package Manager > + > Add package from git URL** and enter:

```
https://github.com/superwall/Superwall-Unity.git
```

### Platform Dependencies

**iOS** — The post-build processor automatically creates a Podfile and runs `pod install`. Requires [CocoaPods](https://cocoapods.org/).

**Android** — Add to your `mainTemplate.gradle`:
```groovy
implementation 'com.superwall.sdk:superwall-android:2.+'
implementation 'com.android.billingclient:billing:8.0.0'
```

## Quick Start

```csharp
using Superwall;

// 1. Configure (call once on app start)
var options = new SuperwallOptions
{
    Logging = new Logging { Level = LogLevel.Debug }
};
Superwall.Configure("your-api-key", options: options);

// 2. Register a placement
Superwall.Shared.RegisterPlacement("my_placement", feature: () =>
{
    // This runs when the user has access (purchased, restored, or non-gated)
    Debug.Log("Feature unlocked!");
});

// 3. Identify users (optional)
Superwall.Shared.Identify("user_123");
```

## Configuration

```csharp
var options = new SuperwallOptions
{
    // Network environment (Release, ReleaseCandidate, Developer)
    NetworkEnvironment = NetworkEnvironment.Release,

    // Test mode (Automatic, WhenEnabledForUser, Never, Always)
    TestModeBehavior = TestModeBehavior.Automatic,

    // Logging
    Logging = new Logging { Level = LogLevel.Debug },

    // Paywall behavior
    Paywalls = new PaywallOptions
    {
        IsHapticFeedbackEnabled = true,
        ShouldPreload = true,
        AutomaticallyDismiss = true,
        RestoreFailed = new RestoreFailed
        {
            Title = "No Subscription Found",
            Message = "We couldn't find an active subscription for your account.",
            CloseButtonTitle = "Okay"
        }
    }
};
```

## Registering Placements

Placements are the core way to show paywalls. Create them on the [Superwall Dashboard](https://superwall.com/dashboard) and register them in code:

```csharp
// Basic — just register
Superwall.Shared.RegisterPlacement("onboarding");

// With parameters
Superwall.Shared.RegisterPlacement("premium_feature",
    parameters: new Dictionary<string, object> { { "source", "settings" } });

// With feature block — runs when user has access
Superwall.Shared.RegisterPlacement("pro_mode", feature: () =>
{
    EnableProMode();
});

// With presentation handler — get callbacks for paywall lifecycle
var handler = new PaywallPresentationHandler
{
    OnPresent = (info) => Debug.Log($"Paywall shown: {info.Identifier}"),
    OnDismiss = (info, result) => Debug.Log($"Paywall dismissed: {result.Type}"),
    OnError = (error) => Debug.LogError($"Paywall error: {error}"),
    OnSkip = (reason) => Debug.Log($"Paywall skipped: {reason}")
};

Superwall.Shared.RegisterPlacement("upgrade", handler: handler, feature: () =>
{
    UnlockPremium();
});
```

## Delegate

Implement `ISuperwallDelegate` to receive SDK lifecycle events:

```csharp
public class MySuperwallHandler : MonoBehaviour, ISuperwallDelegate
{
    void Start()
    {
        Superwall.Shared.SetDelegate(this);
    }

    public void SubscriptionStatusDidChange(SubscriptionStatus from, SubscriptionStatus to)
    {
        Debug.Log($"Status changed: {from.Type} -> {to.Type}");
    }

    public void HandleSuperwallEvent(SuperwallEventInfo eventInfo)
    {
        Debug.Log($"Event: {eventInfo.EventType}");
    }

    public void HandleCustomPaywallAction(string name) { }
    public void WillPresentPaywall(PaywallInfo info) { }
    public void DidPresentPaywall(PaywallInfo info) { }
    public void WillDismissPaywall(PaywallInfo info) { }
    public void DidDismissPaywall(PaywallInfo info) { }
    public void PaywallWillOpenURL(string url) { }
    public void PaywallWillOpenDeepLink(string url) { }
    public void HandleLog(string level, string scope, string message,
        Dictionary<string, object> info, string error) { }
    public void WillRedeemLink() { }
    public void DidRedeemLink(RedemptionResult result) { }
    public void HandleSuperwallDeepLink(string fullURL, List<string> pathComponents,
        Dictionary<string, string> queryParameters) { }
    public void CustomerInfoDidChange(CustomerInfo from, CustomerInfo to) { }
    public void UserAttributesDidChange(Dictionary<string, object> newAttributes) { }
}
```

## Purchase Controller

For custom purchase handling (e.g., using your own billing integration), implement `IPurchaseController`:

```csharp
public class MyPurchaseController : IPurchaseController
{
    public void PurchaseFromAppStore(string productId, Action<PurchaseResult> completion)
    {
        // Handle iOS purchase, then call:
        completion(PurchaseResult.Purchased());
    }

    public void PurchaseFromGooglePlay(string productId, string basePlanId,
        string offerId, Action<PurchaseResult> completion)
    {
        // Handle Android purchase, then call:
        completion(PurchaseResult.Purchased());
    }

    public void RestorePurchases(Action<RestorationResult> completion)
    {
        // Handle restore, then call:
        completion(RestorationResult.Restored());
    }
}

// Pass it during configure:
Superwall.Configure("api-key", purchaseController: new MyPurchaseController());
```

## User Identity

```csharp
// Identify after login
Superwall.Shared.Identify("user_123");

// With options
Superwall.Shared.Identify("user_123",
    new IdentityOptions { RestorePaywallAssignments = true });

// Reset on logout
Superwall.Shared.Reset();

// Properties
string userId = Superwall.Shared.UserId;
bool loggedIn = Superwall.Shared.IsLoggedIn;
```

## User Attributes

```csharp
Superwall.Shared.SetUserAttributes(new Dictionary<string, object>
{
    { "name", "John" },
    { "level", 42 },
    { "isPremium", true }
});

var attrs = Superwall.Shared.GetUserAttributes();
```

## Subscription Status

```csharp
// Read
var status = Superwall.Shared.SubscriptionStatus;
if (status.Type == SubscriptionStatus.StatusType.Active)
{
    var active = (SubscriptionStatus.ActiveStatus)status;
    Debug.Log($"Entitlements: {active.Entitlements.Count}");
}

// Set manually
Superwall.Shared.SubscriptionStatus = SubscriptionStatus.CreateActive(entitlements);
```

## Other APIs

```csharp
// Preload paywalls
Superwall.Shared.PreloadAllPaywalls();
Superwall.Shared.PreloadPaywallsForPlacements(new List<string> { "onboarding", "upgrade" });

// Deep links
Superwall.Shared.HandleDeepLink("https://example.com/link");

// Dismiss current paywall
Superwall.Shared.Dismiss();

// Check state
bool presented = Superwall.Shared.IsPaywallPresented;
bool configured = Superwall.Shared.IsConfigured;

// Log level
Superwall.Shared.LogLevel = LogLevel.Debug;

// Locale
Superwall.Shared.LocaleIdentifier = "en_US";
```

## API Reference

| Method / Property | Description |
|---|---|
| `Configure(apiKey, options?, purchaseController?, completion?)` | Initialize the SDK (static) |
| `Shared` | Access the configured singleton |
| `RegisterPlacement(name, params?, handler?, feature?)` | Register a placement |
| `Identify(userId, options?)` | Identify a user |
| `Reset()` | Reset user identity |
| `SetDelegate(delegate)` | Set lifecycle delegate |
| `UserId` | Current user ID |
| `IsLoggedIn` | Whether user is identified |
| `SubscriptionStatus` | Get/set subscription status |
| `Entitlements` | Current entitlements |
| `SetUserAttributes(dict)` | Set user attributes |
| `GetUserAttributes()` | Get user attributes |
| `PreloadAllPaywalls()` | Preload all paywalls |
| `HandleDeepLink(url)` | Handle a deep link |
| `Dismiss()` | Dismiss current paywall |
| `IsPaywallPresented` | Whether a paywall is showing |
| `IsConfigured` | Whether SDK is configured |
| `LogLevel` | Get/set log level |
| `GetPresentationResult(placement, params?, callback)` | Check what would happen for a placement |
| `ConfirmAllAssignments(callback)` | Confirm all experiment assignments |
| `RestorePurchases(callback)` | Restore purchases |
| `GetCustomerInfo(callback)` | Get customer info |
| `GetDeviceAttributes(callback)` | Get device attributes |

## License

See [LICENSE](LICENSE) for details.
