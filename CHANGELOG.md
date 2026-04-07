# Changelog
All notable changes to this package will be documented in this file.

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
