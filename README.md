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
    // Network environment (Release, ReleaseCandidate, Developer, Local on iOS, or Custom with
    // CustomNetworkEnvironment). Leave it at Release unless the Superwall team tells you otherwise.
    NetworkEnvironment = NetworkEnvironment.Release,

    // Test mode (Automatic, WhenEnabledForUser, Never, Always)
    TestModeBehavior = TestModeBehavior.Automatic,

    // Logging
    Logging = new Logging { Level = LogLevel.Debug },

    // Which events are sent to Superwall (All, SuperwallOnly, None). Null keeps the default, All.
    EventTrackingBehavior = EventTrackingBehavior.All,

    // Ad consent from your consent flow. Null leaves it to an IAB TCF banner, or granted.
    AdConsent = new AdConsent { AdUserData = AdConsentStatus.Granted, AdPersonalization = AdConsentStatus.Denied },

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

Options for one platform do nothing on the other:

| Option | Platform | What it does |
|--------|----------|--------------|
| `DevServer` | iOS | Connects to a running `superwall dev` server: `DevServer.Default` from a simulator, `DevServer.At(url)` from a device. Development builds only |
| `StoreKitVersion` | iOS | StoreKit 1 or 2 |
| `Paywalls.NotificationPermissionsDenied` | iOS | The alert shown when notification permission is denied |
| `Paywalls.ShouldShowWebPurchaseConfirmationAlert` | iOS | Confirms a purchase made on the web |
| `PassIdentifiersToPlayStore` | Android | Passes the user ID to Google Play unhashed |
| `Paywalls.AndroidPresentation` | Android | In the game's window (default) or in its own activity |
| `Paywalls.OptimisticLoading` | Android | Shows the paywall before its products have loaded |
| `Paywalls.PreloadDeviceOverrides` | Android | Turns preloading on or off per `DeviceTier` |
| `Paywalls.LoadingColorResourceName` | Android | The loading spinner's colour, as the name of a colour resource in the app. The Android SDK takes a resource, not a hex value |
| `Paywalls.OnBackPressed` | Android | Called on a back press for paywalls with "Reroute back button" on. While set, the press doesn't dismiss the paywall; call `Dismiss()` yourself |

### In the Editor

There is no native SDK in the Editor or on platforms other than iOS and Android. Calls log what they would do, getters return defaults, and completions run on the next frame: `Configure` reports success, `RegisterPlacement` runs its `feature` block (as when a paywall is skipped), and purchases, restores and `GetProducts` report that there's no native SDK. No paywall is shown.

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
    OnSkip = (reason) => Debug.Log($"Paywall skipped: {reason}"),
    // Like OnSkip, with the experiment when the reason is Holdout
    OnSkipWithExperiment = (reason, experiment) => Debug.Log($"Skipped: {reason} {experiment?.Id}")
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

Implement `ISuperwallDelegate` to receive SDK lifecycle events. Every method has a default implementation that does nothing, so implement only the ones you need:

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

    // Also available: HandleCustomPaywallAction, WillPresentPaywall, DidPresentPaywall, WillDismissPaywall,
    // DidDismissPaywall, PaywallWillOpenURL, PaywallWillOpenDeepLink, HandleLog, WillRedeemLink, DidRedeemLink,
    // CustomerInfoDidChange, UserAttributesDidChange, and HandleSuperwallDeepLink (iOS only: the Android SDK has
    // no such callback, so it is never called there).
}
```

`SuperwallEventInfo` carries each event's values as typed fields: `PaywallInfo`, `Product`, `Transaction`, `Result`, and for specific events `AttributionMatchInfo`, `PageViewData`, `TransactionProduct`, `StatusReason` and others. A field that an event doesn't have is null.

## Embedded Paywalls

`RegisterPlacement` presents a paywall over the whole screen. To show one inside part of your game's screen instead, use `ShowEmbeddedPaywall`, which lays a native view over Unity's:

```csharp
// A rectangle in Unity's screen space: pixels, with the origin at the bottom left.
var rect = new Rect(0, 0, Screen.width, Screen.height * 0.4f);

EmbeddedPaywall paywall = Superwall.Shared.ShowEmbeddedPaywall("shop_banner", rect, handler: new EmbeddedPaywallHandler
{
    OnPresent = info => Debug.Log($"Showing {info.Name}"),
    OnSkip = (reason, experiment) => Debug.Log($"Not shown: {reason}"),
    OnError = error => Debug.LogError(error),
    OnFinished = (info, result, shouldDismiss) =>
    {
        // shouldDismiss: the paywall has been taken off the screen already.
        if (result.Type == PaywallResult.ResultType.Purchased) UnlockShop();
    }
});

paywall.SetRect(newRect); // move or resize it
paywall.Remove();         // take it away
```

Things to know:

- The paywall is a native view, so it is drawn above everything Unity renders, including UI that overlaps its rectangle.
- The rectangle keeps its share of the screen when the screen rotates. Call `SetRect` when your layout changes.
- Whether a paywall is shown follows the placement's audiences, as with `RegisterPlacement`. The presentation style set on the dashboard doesn't apply.
- Show each placement in one embedded paywall at a time.
- `EmbeddedPaywallHandler.OnCustomCallback` is iOS only: the Android SDK has no custom callbacks for embedded paywalls.

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

## Customer Center

The Customer Center is a screen where users view and manage their subscriptions, request refunds, restore purchases and contact support.

```csharp
var configuration = CustomerCenterConfiguration.Default;
configuration.Support.Email = "support@example.com";
configuration.ManagementScreen.Paths.Add(CustomerCenterPath.ForUrl("https://example.com/faq", "FAQ"));

Superwall.Shared.PresentCustomerCenter(configuration, new MyCustomerCenterDelegate(), onDismiss: () => { });

class MyCustomerCenterDelegate : ICustomerCenterDelegate
{
    // Every method is optional.
    public void DidSelectAction(CustomerCenterAction action, string pathId, CustomerCenterPurchase purchase)
    {
        if (action.Type == CustomerCenterActionType.Custom) { /* action.Identifier */ }
    }
}
```

Set the configuration once with `SuperwallOptions.CustomerCenter` to use it whenever `PresentCustomerCenter` isn't given one. `ICustomerCenterDelegate.ShouldRestorePurchases` is only called on iOS: on Android the Customer Center opens in its own activity, which pauses Unity, so restores go ahead without asking.

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

// Deep links. The static overload also works before Configure, for a link that launched the app.
Superwall.HandleDeepLink(new Uri("https://example.com/link"));

// Event tracking and ad consent, for example after the user answers your consent prompt
Superwall.Shared.EventTrackingBehavior = EventTrackingBehavior.None;
Superwall.Shared.AdConsent = new AdConsent { AdUserData = AdConsentStatus.Denied, AdPersonalization = AdConsentStatus.Denied };

// Paywalls in dark mode, or null to follow the system
Superwall.Shared.SetInterfaceStyle(InterfaceStyle.Dark);

// Cancel the local notifications Superwall scheduled (free trial reminders)
Superwall.Shared.CancelAllScheduledNotifications();

// Push notifications through Superwall (Android only; they do nothing on iOS)
Superwall.Shared.SetPushToken(fcmToken);
Superwall.Shared.HandleNotificationOpen(remoteMessageData);

// Dismiss current paywall, optionally hearing when it's gone
Superwall.Shared.Dismiss();
Superwall.Shared.Dismiss(() => Debug.Log("Dismissed"));

// Entitlements a product unlocks
List<Entitlement> unlocked = Superwall.Shared.GetEntitlementsByProductId("pro_monthly");

// Entitlements your own backend granted, merged into the subscription status (iOS only)
Superwall.Shared.GrantedEntitlements = new List<Entitlement> { new Entitlement { Id = "pro" } };

// Game controllers. Needs SuperwallOptions.IsGameControllerEnabled.
Superwall.Shared.SetGameControllerForwardingEnabled(true);          // iOS: forwards every connected controller
Superwall.Shared.DispatchGameControllerKeyEvent(96, isPressed: true); // Android: KEYCODE_BUTTON_A
Superwall.Shared.DispatchGameControllerMotionEvent(x, y);            // Android: a thumbstick

// Android only (they do nothing on iOS)
Superwall.Shared.OpenRestoreOnWeb();                  // the web page for restoring web purchases
string accountId = Superwall.Shared.ExternalAccountId; // what the SDK passes Google Play as the account ID
Superwall.Shared.DismissForNextPaywall();
Superwall.Teardown();                                 // remove the SDK instance, so Configure can run again

// Android only: report purchases you make yourself, with SuperwallOptions.ShouldObservePurchases on
Superwall.Shared.ObservePurchaseStart("pro_monthly");
Superwall.Shared.ObservePurchaseResult(billingResponseCode: 0,
    purchases: new List<GooglePlayPurchase> { new GooglePlayPurchase(originalJson, signature) });
Superwall.Shared.ObservePurchaseError("pro_monthly", "Card declined");

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
| `HandleDeepLink(Uri)` | Handle a deep link (static, works before `Configure`) |
| `HandleDeepLink(url)` | Handle a deep link (needs `Configure` first) |
| `EventTrackingBehavior` | Get/set which events are sent to Superwall |
| `AdConsent` | Get/set the user's ad consent |
| `IntegrationAttributes` | The integration attributes set |
| `SetInterfaceStyle(style)` | Show paywalls in light or dark mode, or `null` for the system setting |
| `CancelAllScheduledNotifications()` | Cancel Superwall's scheduled local notifications |
| `SetPushToken(token)`, `OptOutOfNotifications()`, `OptInToNotifications()`, `HandleNotificationOpen(data)` | Push notifications through Superwall (Android only) |
| `Dismiss()`, `Dismiss(completion)` | Dismiss current paywall |
| `DismissForNextPaywall(completion?)` | Dismiss the paywall to present another (Android only) |
| `ShowEmbeddedPaywall(placement, screenRect, params?, handler?)` | Show a paywall inside part of the screen |
| `GetEntitlementsByProductIds(ids)`, `GetEntitlementsByProductId(id)` | The entitlements products unlock |
| `GrantedEntitlements` | Get/set entitlements your backend granted (iOS only) |
| `ExternalAccountId` | The account ID the SDK passes to Google Play (Android only) |
| `OpenRestoreOnWeb()` | Open the web page for restoring web purchases (Android only) |
| `ObservePurchaseStart(productId)`, `ObservePurchaseResult(code, purchases?, debugMessage?)`, `ObservePurchaseError(productId, error)` | Report purchases made outside Superwall (Android only) |
| `SetGameControllerForwardingEnabled(enabled)` | Forward connected game controllers to the paywall (iOS only) |
| `DispatchGameControllerKeyEvent(keyCode, isPressed)`, `DispatchGameControllerMotionEvent(x, y)` | Forward game controller input to the paywall (Android only) |
| `Teardown()` | Remove the SDK instance so `Configure` can be called again (static, Android only) |
| `IsPaywallPresented` | Whether a paywall is showing |
| `IsConfigured` | Whether SDK is configured |
| `LogLevel` | Get/set log level |
| `Purchase(productId, callback)` | Programmatic purchase |
| `GetProducts(productIds, callback)` | Fetch product details. A callback with a second argument also receives the error, if any |
| `GetPresentationResult(placement, params?, callback)` | Check what would happen for a placement |
| `GetAssignments(callback)` | Get experiment assignments |
| `ConfirmAllAssignments(callback)` | Confirm all experiment assignments |
| `RestorePurchases(callback)` | Restore purchases |
| `GetCustomerInfo(callback)` | Get customer info |
| `RefreshCustomerInfo(callback)` | Fetch customer info from Superwall's servers (iOS; on Android the same as `GetCustomerInfo`) |
| `GetDeviceAttributes(callback)` | Get device attributes |
| `ShowAlert(title?, message?, ...)` | Show alert on current paywall (Android only; iOS calls `onClose` at once) |
| `RefreshConfiguration()` | Force config refresh |
| `PresentCustomerCenter(configuration?, delegate?, onDismiss?)` | Show the Customer Center |
| `DismissCustomerCenter(completion?)` | Close the Customer Center |

## License

See [LICENSE](LICENSE) for details.
