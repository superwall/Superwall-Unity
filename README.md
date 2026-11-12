# Superwall SDK for Unity

Superwall lets you remotely configure paywalls, run A/B tests, and manage monetization — all without shipping app updates.

This package provides a Unity C# wrapper around the native [SuperwallKit](https://github.com/superwall/Superwall-iOS) (iOS) and [Superwall Android SDK](https://github.com/superwall/Superwall-Android).

## Requirements

- Unity 6+ (6000.3+)
- iOS 15.0+
- Android minSdk 25+, compileSdk 35+ (set **Target API Level** to 35 or higher in Player Settings)

## Installation

In Unity, go to **Window > Package Manager > + > Add package from git URL** and enter:

```
https://github.com/superwall/Superwall-Unity.git
```

### Platform Dependencies

**iOS** — No CocoaPods needed. The post-build processor adds [SuperwallKit](https://github.com/superwall/Superwall-iOS) to the exported Xcode project as a Swift package (Swift Package Manager), linked to the `UnityFramework` target. Xcode resolves it the first time you open or build `Unity-iPhone.xcodeproj`. Requires Xcode 26 and a Target minimum iOS Version of 15.0 or later.

**Android** — No manual setup needed. The package includes a Gradle library module that automatically pulls the Superwall Android SDK (pinned per release) and billing dependencies from Maven. The module inherits the compile SDK from your Player Settings and builds under both AGP 8.x (Unity 6000.3) and AGP 9+ (Unity 6000.4+).

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

## Custom Callbacks

A paywall can ask your game to do some work and act on the result, for example creating an account through your own API and showing a server error on failure. Set `OnCustomCallback` on the presentation handler:

```csharp
var handler = new PaywallPresentationHandler
{
    OnCustomCallback = async (callback) =>
    {
        if (callback.Name != "createAccount")
            return CustomCallbackResult.Failure();

        // Variables holds the values the paywall sent, and is null if it sent none.
        var email = callback.Variables?["email"] as string;
        var signup = await MyApi.CreateAccountAsync(email);

        return signup.Succeeded
            ? CustomCallbackResult.Success()
            : CustomCallbackResult.Failure(new Dictionary<string, object> { { "message", signup.Error } });
    }
};

Superwall.Shared.RegisterPlacement("signup", handler: handler);
```

The result picks the paywall's onSuccess or onFailure branch, and `Data` is readable there as `callbacks.createAccount.data.message`. Values can be strings, numbers, booleans, lists and dictionaries.

- A blocking callback holds the paywall's tap action until your task completes, so give network requests a timeout.
- If the handler throws, is cancelled or returns null, or the placement has no `OnCustomCallback`, the paywall gets a failure.
- On Android this needs the default in-window presentation. With `AndroidPaywallPresentation.Activity`, Unity is paused while the paywall is open, so callbacks always fail.

`HandleCustomPaywallAction` on the delegate is different: it tells your game a button was tapped, and can't send anything back to the paywall.

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
    // iOS only: the Android SDK has no such callback, so it is never called there.
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

### Subscription replacement (Android)

When a paywall's Purchase action is set to replace an existing subscription, Superwall passes the
subscription being replaced to an overload of `PurchaseFromGooglePlay`. Implement it to upgrade,
downgrade or switch the user's Google Play subscription instead of buying a second one:

```csharp
public void PurchaseFromGooglePlay(string productId, string basePlanId, string offerId,
    ProductReplacement replacement, Action<PurchaseResult> completion)
{
    if (replacement != null)
    {
        // Pass replacement.OldPurchaseToken and replacement.Mode to the billing flow's
        // SubscriptionUpdateParams (RevenueCat: oldProductId + googleReplacementMode).
    }
    // ...purchase, then call completion(...)
}
```

`replacement` is null for normal purchases. Implementing the overload is optional: by default it
calls the four-argument `PurchaseFromGooglePlay` and ignores the replacement.

### Custom products

A custom product is one whose store is set to **Custom** on the Superwall dashboard: you sell it
through your own payment system (Stripe, Paddle, a web checkout or another store) rather than the
App Store or Google Play. On both platforms Superwall sends these to `PurchaseCustomProduct`
instead of `PurchaseFromAppStore` or `PurchaseFromGooglePlay`:

```csharp
public void PurchaseCustomProduct(CustomProductPurchase purchase, Action<PurchaseResult> completion)
{
    // Start your checkout for purchase.ProductId. Pass purchase.CustomTransactionId along and have
    // your server report it to Superwall as custom_transaction_id, so the revenue is attributed
    // to the paywall that sold it.
    MyCheckout.Buy(purchase.ProductId, purchase.CustomTransactionId, succeeded =>
    {
        if (succeeded)
        {
            // Superwall doesn't grant entitlements for custom products: set them yourself.
            Superwall.Shared.SubscriptionStatus = SubscriptionStatus.CreateActive(myEntitlements);
            completion(PurchaseResult.Purchased());
        }
        else
        {
            completion(PurchaseResult.Cancelled());
        }
    });
}
```

On Android, `purchase.Replacement` is set when the purchase replaces a subscription the user has.
For custom products its `OldProductId` comes from the active entitlements you reported, and
`OldPurchaseToken` is null. Implementing `PurchaseCustomProduct` is optional: by default it fails
the purchase.

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

## Programmatic Purchases

Purchase a product directly without showing a paywall:

```csharp
Superwall.Shared.Purchase("product_id", (result) =>
{
    Debug.Log($"Purchase result: {result.Type}"); // Purchased, Cancelled, Pending, Failed
});
```

## Products

Fetch product details by ID:

```csharp
Superwall.Shared.GetProducts(new List<string> { "monthly", "annual" }, (products) =>
{
    foreach (var kvp in products)
        Debug.Log($"{kvp.Key}: {kvp.Value.LocalizedPrice}");
});
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

// Show alert on current paywall (Android only; on iOS it does nothing and calls onClose straight away)
Superwall.Shared.ShowAlert(title: "Hey!", message: "Check this out");

// Force refresh config
Superwall.Shared.RefreshConfiguration();

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
| `Purchase(productId, callback)` | Programmatic purchase |
| `GetProducts(productIds, callback)` | Fetch product details |
| `GetPresentationResult(placement, params?, callback)` | Check what would happen for a placement |
| `GetAssignments(callback)` | Get experiment assignments |
| `ConfirmAllAssignments(callback)` | Confirm all experiment assignments |
| `RestorePurchases(callback)` | Restore purchases |
| `GetCustomerInfo(callback)` | Get customer info |
| `GetDeviceAttributes(callback)` | Get device attributes |
| `ShowAlert(title?, message?, ...)` | Show alert on current paywall (Android only; iOS calls `onClose` at once) |
| `RefreshConfiguration()` | Force config refresh |

## License

See [LICENSE](LICENSE) for details.
