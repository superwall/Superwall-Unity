# Changelog
All notable changes to this package will be documented in this file.

## [Unreleased]

### Enhancements
* Adds the Customer Center, a screen where users view and manage their subscriptions, request refunds, restore purchases and contact support. Show it with `Superwall.Shared.PresentCustomerCenter(configuration, customerCenterDelegate, onDismiss)` and close it with `DismissCustomerCenter(completion)`. `CustomerCenterConfiguration` sets its screens, paths (`CustomerCenterPath.Restore()`, `ManageSubscription()`, `Refund()`, `ChangePlan()`, `ContactSupport()`, `ForUrl()` and `Custom()`), surveys, support details and accent colour; set the default with `SuperwallOptions.CustomerCenter`, or leave it to use `CustomerCenterConfiguration.Default`. An `ICustomerCenterDelegate` hears which path the user tapped and for which purchase, survey answers, refund requests and the dismissal. `ShouldRestorePurchases` is iOS only: on Android the Customer Center is its own activity, which pauses Unity, so the restore goes ahead without asking.
* Adds `EventTrackingBehavior` (`All`, `SuperwallOnly` or `None`) to choose which events the SDK sends to Superwall. Set it with `SuperwallOptions.EventTrackingBehavior` when configuring, or at any time with `Superwall.Shared.EventTrackingBehavior`, for example to stop all events when the user declines data collection. `SuperwallOptions.IsExternalDataCollectionEnabled` is deprecated in its favour; it keeps working and is ignored when `EventTrackingBehavior` is set.
* Adds `AdConsent`, the user's consent for advertising use of their data, which Superwall passes on with the conversions it uploads to Google Ads and Meta. Set it with `SuperwallOptions.AdConsent` or `Superwall.Shared.AdConsent` from your consent flow. Left unset, the SDKs use the answer stored by an IAB TCF consent banner when GDPR applies, and otherwise report consent as granted.
* Adds `Superwall.Shared.IntegrationAttributes`, the attributes set with `SetIntegrationAttribute` and `SetIntegrationAttributes`.
* Adds `Superwall.Shared.SetInterfaceStyle(InterfaceStyle?)` to show paywalls in light or dark mode whatever the system setting. Pass `null` to follow the system again.
* Adds `Superwall.Shared.CancelAllScheduledNotifications()` to cancel the local notifications Superwall scheduled, such as free trial reminders.
* Adds `Superwall.Shared.RefreshCustomerInfo(completion)`, which fetches the customer info from Superwall's servers on iOS. Android has no such call and completes with the same as `GetCustomerInfo`.
* Adds a static `Superwall.HandleDeepLink(Uri)`, which works before `Configure`, so a deep link that launched the app can be handed over before the SDK is configured. The instance method needs `Superwall.Shared`, which is null until then.
* Android: adds `SetPushToken`, `OptOutOfNotifications`, `OptInToNotifications` and `HandleNotificationOpen` for push notifications sent through Superwall with Firebase Cloud Messaging. They do nothing on iOS.
* Adds `SuperwallOptions.StoreKitVersion` (iOS only) and `SuperwallOptions.EnableExperimentalDeviceVariables`.
* Adds `PaywallOptions.NotificationPermissionsDenied` (iOS only), the alert shown when a paywall asks for notification permission the user has denied. It's `null` by default, which shows no alert, as in SuperwallKit.
* Adds `PaywallOptions.OptimisticLoading` (Android only) to show a paywall before its products have loaded.
* Adds `PaywallPresentationHandler.OnWillDismiss` (iOS only), called when the paywall is about to be dismissed.
* Adds `PaywallSkippedReason.UserIsSubscribed` (Android only).
* Adds the `Analytics`, `GrantedEntitlements` (iOS only), `WebEntitlements`, `CustomerCenter`, and `CustomerInfo`, `JsEvaluator`, `PaywallTransactions`, `NativePurchaseController`, `DeepLinks` and `Billing` (Android only) log scopes.
* Adds the `SingularDeviceId` and `AirbridgeId` integration attributes, plus `FirebaseInstallationId` (iOS only) and `Meta`, `Amplitude`, `Mixpanel`, `GoogleAds`, `GoogleAppSetId` and `Custom` (Android only).
* The native SDKs now report the Unity SDK and its version with the app's device attributes, as the other Superwall wrappers do.

### Fixes
* `SetIntegrationAttribute` and `SetIntegrationAttributes` did nothing on either platform: C# sent the enum member's name (`AdjustId`) and both native bridges only accept the SDKs' own names (`adjustId`), so every call was dropped. They now reach the native SDKs. A null value removes the attribute, as natively. On Android each call now changes only the attributes passed; the Android SDK replaces the whole set on every call, so the bridge merges into the current set to match iOS.
* `CustomerInfo.Subscriptions` and `CustomerInfo.NonSubscriptions` were always null, from `GetCustomerInfo` and from `CustomerInfoDidChange`. Both platforms now send every transaction, with dates as epoch milliseconds like the entitlement dates.
* `SuperwallEventInfo` now carries the values of every event that has them: `PlacementName` and `Result` (trigger fire), `TriggeredPlacementName`, `Attempt` and `MissingProductIdentifiers` (paywall response and product loading), `Name` (custom placements), `Survey`, `SelectedOption` and `CustomResponse` (survey responses), `Status` (presentation requests), `DeepLinkUrl`, `DeviceAttributes`, `UserAttributes`, `IntegrationAttributes`, `UserEnrichment` and `DeviceEnrichment`, `Token` (iOS AdServices), `Message` (restore failures), `ReviewRequestedCount`, `Error` for the webview, product and resource load failures, and `PaywallInfo` for the webview load, Stripe checkout, teleport and page view events. Before, only the paywall open/close/decline and transaction events had any of them.
* `RedemptionInfo.Ownership`, `.PurchaserInfo` and `.PaywallInfo` were always null in `DidRedeemLink`. Both platforms now send them.
* `LatestPaywallInfo` returned only the identifier, name, URL, product IDs and experiment. It now has every field, like the `PaywallInfo` the delegate receives.
* `Entitlements.Web` was always empty. Both platforms now send the web entitlements.
* `HandleLog`'s `info` was always null. Both platforms now send it.
* Setting a user attribute to null now removes it, as it does natively. Before, the attribute was kept with a null value. On Android, nested dictionaries and lists in user attributes and placement parameters now reach the SDK as maps and lists.
* iOS: `Entitlement.Store` was always null, because the bridge sent the store's type name instead of the store. It now arrives as on Android.
* iOS: `Survey.AssignmentKey` was always null. It is now sent, as on Android.
* iOS: `SetLocalResources(null)` passed a null pointer to a parameter that can't take one. It now clears the resources.
* Android: `StoreProduct` from `GetProducts` and events now has the period and trial period strings, `TrialPeriodEndDate`, `TrialPeriodEndDateString`, `RegionCode` and `Attributes`, which were only filled on iOS. Products in `TransactionStart`, `TransactionAbandon` and `TransactionComplete` events now carry every field instead of only the identifier.
* Android: `GetPresentationResult` never called its completion when the native call failed. It now reports `PaywallNotAvailable`, as iOS does.
* Android: `SuperwallOptions.Paywalls.OverrideProductsByName` was ignored by `Configure`. It is now applied.
* Android: only 12 of the 22 `LogScope` values were applied; the others were dropped, and if none was left every scope was logged. Every value is now applied (`DebugViewController`, `LocalizationViewController` and `PaywallViewController` turn on the Android SDK's `debugView`, `localizationView` and `paywallView`).
* Android: `HandleDeepLink` threw before `Configure`. It now calls the SDK's static handler, which keeps the link until the SDK is configured.
* Android: a `GetProducts` call that fails now logs the error instead of silently returning no products.

### Behaviour changes
* Android: a paywall skipped because the user is subscribed now reaches `PaywallPresentationHandler.OnSkip` as `PaywallSkippedReason.UserIsSubscribed`. Before, it arrived as `NoAudienceMatch`.
* `PaywallOptions.ShouldShowWebPurchaseConfirmationAlert` now defaults to `true`, as in SuperwallKit. Before, passing any `SuperwallOptions` turned the iOS web purchase confirmation alert off. Set it to `false` to keep it off.
* A new `Entitlement` is now active (`IsActive` defaults to `true`), as `Entitlement(id:)` is natively. `SubscriptionStatus.CreateActive(new List<Entitlement> { new Entitlement { Id = "pro" } })` used to be sent as active with no entitlements, because the setter skips inactive ones.
* `PaywallOptions.RestoreFailed` now sends only the texts you changed. Android shows its translation of each text left at its default; before, the English defaults always replaced them.

### Dependencies
* Android: pins the native SDK to 2.10.2 (was `2.10.0`). [View Android SDK release notes](https://github.com/superwall/Superwall-Android/releases/tag/2.10.2).

## [0.6.0]

### Breaking Changes
* iOS: SuperwallKit now comes from Swift Package Manager instead of CocoaPods. The post-build processor adds the `https://github.com/superwall/Superwall-iOS` package (from 4.19.1 up to the next major version) to the exported Xcode project, links `SuperwallKit` to `UnityFramework` and copies SuperwallKit's resource bundle into it. It no longer writes a Podfile, so CocoaPods is no longer required. Open `Unity-iPhone.xcodeproj`; Xcode resolves the package on first open or build. Projects that use other pods (for example through External Dependency Manager) keep their Podfile and workspace as before.
* iOS: a Target minimum iOS Version below 15.0 is no longer raised for SuperwallKit, because Swift packages build for the app's own deployment target. The build still logs a warning; raise the target to 15.0 or the Xcode build fails to link SuperwallKit.

### Fixes
* iOS: 0.4.1 and 0.5.0 required SuperwallKit 4.19.1 from CocoaPods, but SuperwallKit 4.19.0 and later aren't published there (the newest pod is 4.18.3), so `pod install` failed with `None of your spec sources contain a spec satisfying the dependency: SuperwallKit (< 5.0, >= 4.19.1)` and the Xcode build couldn't link SuperwallKit. Getting it from Swift Package Manager fixes this.

### Migration
* A Replace build needs no changes. An Append build over an iOS project exported by an earlier version keeps the old Podfile: the post-build processor removes its SuperwallKit pod and runs `pod install`, so the workspace doesn't link SuperwallKit from both the pod and the Swift package. If `pod install` can't run, the build logs a warning with the command to run yourself, or make a Replace build.

## [0.5.0]

### Enhancements
* Custom products now work with a purchase controller on iOS and Android. A custom product is one whose store is set to Custom on the Superwall dashboard, sold through your own payment system instead of the App Store or Google Play. They now reach a new `IPurchaseController.PurchaseCustomProduct(CustomProductPurchase purchase, completion)` method with the product's ID, the `CustomTransactionId` Superwall generated for the purchase (send it back from your server as `custom_transaction_id` to attribute the revenue), and on Android any subscription it replaces. The method has a default implementation that fails the purchase, so existing controllers compile unchanged.

### Fixes
* iOS sent custom products to `PurchaseFromAppStore` as if they were App Store products, so a controller tried to buy them from StoreKit. Android failed them without reaching C#. Both now call `PurchaseCustomProduct`.

## [0.4.1]

### Enhancements
* Android: subscription replacement. When a paywall's Purchase action is set to replace an existing subscription, a custom purchase controller now receives the subscription being replaced through a new `IPurchaseController.PurchaseFromGooglePlay(productId, basePlanId, offerId, ProductReplacement replacement, completion)` overload. Pass `replacement.OldPurchaseToken` and `replacement.Mode` to the billing flow's `SubscriptionUpdateParams` to upgrade, downgrade or switch the user's subscription instead of buying a second one. The overload has a default implementation that calls the existing four-argument method, so existing controllers compile and behave as before. Purchases without a custom controller handle the replacement automatically.
* `EventType` gains the events SuperwallKit 4.18 and superwall-android 2.10 added: `TeleportOpen` and `TeleportReturn` (iOS), and `CustomerCenterOpen`, `CustomerCenterClose`, `CustomerCenterAction`, `CustomerCenterSurveyResponse` and `CustomerCenterRefundRequest`. Before, they reached `HandleSuperwallEvent` with a warning and the default `EventType`.

### iOS
* SuperwallKit 4.18 and later need Xcode 26 to build. The exported Xcode project no longer compiles with Xcode 16.
* SuperwallKit 4.18 and later need iOS 15. If Target minimum iOS Version is lower, the generated Podfile now uses iOS 15 so `pod install` succeeds, and the build logs a warning to raise the game's own target.

### Android
* `LogScope.StoreKitManager` now enables the Android SDK's `billing` log scope, which replaces the deprecated `storeKitManager`. Android billing logs reach `SuperwallDelegate.HandleLog` with the scope `"billing"` instead of `"storeKitManager"`.

### Dependencies
* Android: pins the native SDK to 2.10.0 (was `2.8.7`). View the Android SDK release notes for [2.9.0](https://github.com/superwall/Superwall-Android/releases/tag/2.9.0) and [2.10.0](https://github.com/superwall/Superwall-Android/releases/tag/2.10.0).
* iOS: pins the native SDK to 4.19.1. The generated Podfile already requires SuperwallKit `>= 4.19.1, < 5.0`; the CI harness now builds the bridge against exactly 4.19.1 (was `from: 4.12.10`). [View iOS SDK release notes](https://github.com/superwall/Superwall-iOS/releases/tag/4.19.1).

## [0.4.0]

### Enhancements
* Custom callbacks now work on iOS and Android. `PaywallPresentationHandler.OnCustomCallback` runs when a paywall requests a callback, and the result you return drives the paywall's onSuccess/onFailure branch, with its `Data` readable as `callbacks.NAME.data.KEY`. Before, the field existed but neither native bridge called it, so every callback failed straight away without reaching C#.
* Added `CustomCallbackResult.Success(data)` and `CustomCallbackResult.Failure(data)`.

### Breaking Changes
* `OnCustomCallback` is now `Func<CustomCallback, Task<CustomCallbackResult>>`, so a handler can wait on a network request without blocking the main thread. Write it as an `async` lambda. If it throws, is cancelled or returns null, the paywall gets a failure.

### Android
* Custom callbacks need the default in-window presentation. With `AndroidPaywallPresentation.Activity`, Unity is paused while the paywall is open, so each callback fails straight away and logs an error instead of leaving the paywall waiting.

### Fixes
* iOS paywalls now follow the orientations the game allows through `Screen.autorotateTo*` and `Screen.orientation`. Before, a landscape-only game could be rotated to portrait while a paywall was open, and stayed in portrait after it closed.

### Dependencies
* iOS: the generated Podfile now requires SuperwallKit `>= 4.19.1, < 5.0`. 4.12.10 added the custom callback API and 4.19.1 keeps paywalls to the game's orientations. A Podfile kept by an Append build is updated, unless you changed the SuperwallKit requirement by hand.

## [0.3.0]

### Enhancements
* Android paywalls are now shown inside the game's activity by default, so Unity keeps running while a paywall is on screen. Before, the paywall opened in its own activity, which paused Unity's player loop. A C# purchase controller then never received the purchase request and the paywall spun forever, and every delegate callback was held until the paywall closed. Set `SuperwallOptions.Paywalls.AndroidPresentation = AndroidPaywallPresentation.Activity` to keep the old behaviour.

### Dependencies
* Android: bumped the `superwall-android` pin from 2.7.23 to 2.8.7, which adds the in-window paywall host, and the Google Play Billing pin from 8.0.0 to 9.1.0 to match it. `androidx.activity` stays at 1.10.1, so the compileSdk 35 requirement is unchanged. Games that also use Unity IAP get Billing 9.1.0 through Gradle's version resolution.

### Fixes
* Android purchase controller requests that can't reach Unity now fail straight away instead of waiting for a reply that never comes. A request the native SDK has given up on is cancelled on the C# side and no longer starts a store transaction with no paywall on screen. A purchase controller that throws now replies `Failed`, and each request is answered at most once.
* Android `SubscriptionStatus.Active` getter serialization emitted only `{ type: "active" }` and dropped its entitlement set, although the setter accepted entitlements. Unity therefore read `Active []` after writing `Active [premium]`, causing repeated entitlement pushes and leaving the merge guard blocked. Android now serializes the active entitlement set in the same shape as iOS.
* Both native bridges serialized only 4 of the 13 fields of an `Entitlement` (`id`, a hardcoded `type`, `isActive`, `productIds`), so `LatestProductId`, `Store`, `StartsAt`, `RenewedAt`, `ExpiresAt`, `IsLifetime`, `WillRenew`, `State` and `OfferType` were always null on the C# side even though `DeserializeEntitlement` reads them. A consumer could not tell a trial from a paid subscription, nor read an expiry or renewal state, from the entitlement. Every field is now emitted on iOS and Android with one shared vocabulary (enum case names matching the C# enums, dates as epoch milliseconds).
* `SuperwallEventInfo.EventType` was never populated: iOS serialized `String(describing: event)`, which on SuperwallKit versions without a `CustomStringConvertible` conformance dumps the case together with its associated values (`transactionComplete(transaction: ..., product: ...)`), and Android sends the snake_case `rawName` (`transaction_complete`). Neither matched the `EventType` enum, so every delegate event arrived as the default `FirstSeen` and any `switch` on it was dead. iOS now emits only the case name, and the C# parser accepts both camelCase and snake_case, logging a warning instead of failing silently when a name is still unknown. `EventType` gains the members both native SDKs already emit but C# lacked (`PaywallPageView`, `TestModeModalOpen`/`Close`, `CelExpressionResult`, `ErrorThrown`, `ReviewGranted`/`Denied`, `AttributionMatch`, `StripeCheckoutStart`/`Submit`/`Complete`/`Fail`), and the two placement names whose member is spelled differently (`deepLink_open` → `DeepLink`, `paywallWebviewLoad_processTerminated` → `PaywallWebviewProcessTerminated`) are mapped explicitly.
* `SuperwallEventInfo.Product`, `.Transaction`, `.PaywallInfo`, `.Error` and `.RestoreType` were never populated on either platform: the bridges forwarded only `eventType` and the flattened `params`. The store transaction (and with it the store transaction id) was therefore unreachable from `handleSuperwallEvent`. Both bridges now serialize the associated values of the paywall and transaction events (`paywallOpen/Close/Decline`, `transactionStart/Abandon/Complete/Fail/Restore/Timeout`, `subscriptionStart`, `freeTrialStart`, `nonRecurringProductPurchase`), and the C# side reads them. Transaction dates are ISO 8601 strings, matching the existing `StoreTransaction` model.

## [0.2.6]

### Dependencies
* Android: bumped the `superwall-android` pin from 2.7.21 to 2.7.23. Transitive dependencies are unchanged (still `androidx.activity` 1.10.1 / Billing 8.0.0), so the compile SDK and `minSdk` requirements of this package are unaffected. Upstream in that range:
  * Fixes paywall text inputs silently dropping non-Latin keyboard text (Cyrillic, Korean, Thai, emoji). The webview no longer replaces Chromium's `InputConnection` unless `isGameControllerEnabled` is set, restoring composition, autocorrect, swipe typing and autofill.
  * Errors thrown when the user lacks billing no longer propagate to the app.
  * The backend is always notified of purchased entitlements.

## [0.2.5]

### Fixes
* Android: `SuperwallSDK.androidlib` no longer hardcodes `compileSdkVersion 34` — it now inherits the host project's compile SDK from `unity.compileSdkVersion` (written into `gradle.properties` by Unity from Player Settings), with a fallback of 35. Fixes `checkReleaseAarMetadata` failures after superwall-android started requiring compileSdk 35+ via androidx.activity 1.10.1.
* Android: `superwall-android` is now pinned to an exact version (2.7.21) instead of the floating `2.+`, so a Maven-side release can no longer break consumer builds overnight. The pin will be bumped deliberately with each Unity SDK release.
* Android: the `kotlin-android` plugin is only applied on AGP < 9. AGP 9+ (custom gradle templates / future Unity versions) ships built-in Kotlin, and applying the external plugin there crashed Gradle evaluation (`BaseExtension` removed / duplicate `kotlin` extension). The module's build script now uses new-DSL-safe syntax and builds under both AGP 8.x and AGP 9.
* Android: `restorePurchases` bridge callback never fired — superwall-android 2.7.x changed `restorePurchases()` to return `Result<RestorationResult>`, which the bridge still matched as a bare `RestorationResult` (both `when` branches were unreachable). Now unwrapped correctly.
* iOS: the generated Podfile no longer hardcodes `platform :ios, '16.0'` — it honors the minimum iOS version from Player Settings (fallback 15.0), so Pods build for the same deployment target as the host app and older simulators/devices stay selectable in Xcode.

### CI
* The Android harness job is now a matrix over AGP 8.5.2/Gradle 8.7 and AGP 9.0.1/Gradle 9.3.1, with the harness AGP version parameterized via `-PagpVersion`.

## [0.2.4]

### Fixes
* Fix configure result emitting an unknown error on success
* iOS `_SuperwallBridge_ShowAlert` ABI mismatch (3 pointers vs C# `(string)`) — now takes a single `alertJson` string matching the C# extern. iOS additionally fires the `onCloseCallbackId` immediately so the pending C# callback is cleaned up.
* Android `showAlert` was reading `actionCallbackId` / `closeCallbackId`, but C# emits `onActionCallbackId` / `onCloseCallbackId` — alert callbacks never fired. Fixed.
* iOS bridge: removed access to internal `Superwall.shared.options` — `SetLocalResources` now stashes the resource map and applies it via `SuperwallOptions.localResources` during `Configure` (must be called before `Configure` on iOS; logs a warning otherwise).
* Post-build `pod install` now locates `pod` across common install paths (`/usr/local/bin`, `/opt/homebrew/bin`, rbenv/asdf shims, `which pod` under a login shell) and runs under a login shell so user PATH from `~/.zshrc`/`~/.bash_profile` is honored. Clear manual-install instructions on failure.
* Post-build `pod install` now sets `LANG=en_US.UTF-8` and `LC_ALL=en_US.UTF-8` in the subprocess so CocoaPods no longer crashes with `Encoding::CompatibilityError` when Unity is launched from Finder (inherits launchd's empty `LANG`).

### Tests
* Added `Tests/Runtime/BridgeContractTests.cs` covering the async-response contract, Configure success/failure paths, and the delegate/handler payload shapes for the regressions fixed in this release.

### CI
* Added `.github/workflows/native.yml` — builds the iOS Swift bridge via SPM on macOS and the Android `.androidlib` via Gradle on Ubuntu, on every push and PR. No Unity license required.
* Added parked `.github/workflows/unity.yml` for full game-ci Unity build + test runs, documented with the licensing secrets and wrapping-project steps needed to enable it.
* CI harness scaffolding lives under `ci~/` (tilde-suffixed so Unity ignores it during package import — never copied into player builds).

### Compatibility
* Minimum Unity version lowered from `6000.4` to `6000.3`.

## [0.2.3]

## Enhancements

### Android Purchase Controller
* Android purchase controller parity with the iOS purchase controller flow
* `SetIntegrationAttribute` / `SetIntegrationAttributes` now route to `Superwall.instance.setIntegrationAttributes` with `AttributionProvider` parsing
* `GetOverrideProductsByName` / `SetOverrideProductsByName` now read/write `Superwall.instance.overrideProductsByName`
* `GetEntitlementsByProductIds` now resolves through `entitlements.byProductIds(...)` and returns serialized entitlement set
* `Consume(token, completion)` now invokes the suspend `Superwall.instance.consume(token)` and reports `success`/`failed` with token or error
### iOS Local Resources
* `SetLocalResources(Dictionary<string, string>)` is now implemented on iOS — sets `Superwall.shared.options.localResources`, supporting both `file://` URLs and raw paths

## Fixes
* Minor issue fixes
* Fix mapping issue with skip handlers and product shape for getProducts

## [0.2.2]

### Breaking Changes
* `Configure` completion signature changed from `Action<bool>` to `Action<ConfigurationResult>` to match the native SDKs' `Result<Unit>` semantics. The result exposes `IsSuccess` and a typed `FailedResult.Error` on failure. Android now propagates SDK init errors through this; iOS still always signals success since SuperwallKit's completion has no failure variant.

### Cleanup
* Renamed asmdef files to `Superwall.*`

## [0.2.1]

## Enhancements

### New APIs
* `SetLocalResources(Dictionary<string, string>)` — map asset names to local file paths for paywall WebViews (Android only)

### Delegate Fixes
* Android: added `willRedeemLink`, `didRedeemLink`, `userAttributesDidChange` delegate callbacks
* iOS: added `handleSuperwallDeepLink`, `userAttributesDidChange` delegate callbacks
* iOS: added `ShowAlert` no-op stub to prevent missing symbol crash

## [0.2.0]

### Android Support
* Full Android support via bundled `.androidlib` Gradle module — no manual `mainTemplate.gradle` setup needed
* Kotlin bridge compiled with Kotlin 2.0.21 to match Superwall Android SDK 2.x
* Custom `ActivityProvider` for Unity ensures paywall presentation works correctly
* AndroidManifest with required activity declarations merged automatically

### New APIs
* `Purchase(productId, callback)` — programmatic purchase without a paywall
* `GetProducts(productIds, callback)` — fetch product details by ID
* `GetAssignments(callback)` — get experiment assignments without confirming
* `ShowAlert(title, message, ...)` — show alerts on the current paywall
* `RefreshConfiguration()` — force SDK config refresh

### Options
* Full `SuperwallOptions` parsing on both platforms (was incomplete)
* Added `PaywallOptions.UseCachedTemplates` and `PaywallOptions.TimeoutAfter`
* `TestModeBehavior`, `NetworkEnvironment`, `Paywalls.*`, `Logging.Scopes` now properly passed to native SDKs

### Delegate & Callbacks
* All `ISuperwallDelegate` callbacks now receive deserialized objects instead of null
* `SubscriptionStatus` getter properly deserializes native state (was always returning Unknown)
* All async getters (`Entitlements`, `CustomerInfo`, `PaywallInfo`, `PresentationResult`, `ConfirmedAssignment`, `RestorationResult`) now deserialize correctly
* Fixed async callback mechanism — was dropping response data, causing `Configure` completion to always return false

### iOS
* Purchase controller flow implemented with async continuations
* Integration attributes mapping implemented
* Full options parity with Android

### Cleanup
* Removed legacy `com.ian_unity558.com.superwall.sdk` package
* Removed stale `EnsureAndroidGradleDependency` editor script (replaced by `.androidlib`)

## [0.1.1] 

* Android package support
* Handler callback arguments
* More properties implemented
* Improved option support

## [0.1.0] 

### This is the first release of *\<com.superwall.sdk\>*.

* iOS support, registering and callbacks
