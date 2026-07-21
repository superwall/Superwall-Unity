# Changelog
All notable changes to this package will be documented in this file.

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
