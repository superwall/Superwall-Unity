# Changelog
All notable changes to this package will be documented in this file.

## [0.2.3]

### Android Purchase Controller
* Android purchase controller parity with the iOS purchase controller flow

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
