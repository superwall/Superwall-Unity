using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Superwall.Internal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Superwall.Tests
{
    /// <summary>
    /// The C# half of the native API added for parity with SuperwallKit and superwall-android: what each call sends
    /// across the bridge (read from the Editor log line, which carries the same arguments) and what C# makes of each
    /// payload the bridges send back. The native halves are covered in ci~/android-harness/bridge-tests.
    /// </summary>
    class NativeApiTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;
        }

        private static global::Superwall.Superwall UnconfiguredInstance() =>
            (global::Superwall.Superwall)Activator.CreateInstance(typeof(global::Superwall.Superwall), nonPublic: true);

        private static Dictionary<string, object> SerializeOptions(SuperwallOptions options)
        {
            var serialize = typeof(global::Superwall.Superwall).GetMethod("SerializeOptions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (Dictionary<string, object>)Json.Deserialize((string)serialize.Invoke(null, new object[] { options }));
        }

        private static void Send(string method, Dictionary<string, object> data)
        {
            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", method },
                { "data", data },
            }));
        }

        // --- Options ---

        [Test]
        public void Options_LeaveUnsetNativeDefaultsOut()
        {
            var json = SerializeOptions(new SuperwallOptions());
            // Sending these would override the native defaults: an assigned ad consent outranks a TCF banner's.
            Assert.IsFalse(json.ContainsKey("eventTrackingBehavior"));
            Assert.IsFalse(json.ContainsKey("adConsent"));
            Assert.IsFalse(json.ContainsKey("storeKitVersion"));
            Assert.AreEqual(false, json["enableExperimentalDeviceVariables"]);
            var paywalls = (Dictionary<string, object>)json["paywalls"];
            Assert.IsFalse(paywalls.ContainsKey("notificationPermissionsDenied"));
            Assert.AreEqual(false, paywalls["optimisticLoading"]);
        }

        [Test]
        public void Options_SendTheNewSettingsUnderTheBridgeKeys()
        {
            var options = new SuperwallOptions
            {
                EventTrackingBehavior = EventTrackingBehavior.SuperwallOnly,
                AdConsent = new AdConsent { AdPersonalization = AdConsentStatus.Denied },
                StoreKitVersion = StoreKitVersion.StoreKit1,
                EnableExperimentalDeviceVariables = true,
            };
            options.Paywalls.OptimisticLoading = true;
            options.Paywalls.NotificationPermissionsDenied = new NotificationPermissionsDenied { Title = "Notifications are off" };

            var json = SerializeOptions(options);
            Assert.AreEqual("superwallOnly", json["eventTrackingBehavior"]);
            var consent = (Dictionary<string, object>)json["adConsent"];
            Assert.AreEqual("granted", consent["adUserData"]);
            Assert.AreEqual("denied", consent["adPersonalization"]);
            Assert.AreEqual("storeKit1", json["storeKitVersion"]);
            Assert.AreEqual(true, json["enableExperimentalDeviceVariables"]);
            var paywalls = (Dictionary<string, object>)json["paywalls"];
            Assert.AreEqual(true, paywalls["optimisticLoading"]);
            var denied = (Dictionary<string, object>)paywalls["notificationPermissionsDenied"];
            Assert.AreEqual("Notifications are off", denied["title"]);
            Assert.AreEqual(new NotificationPermissionsDenied().Message, denied["message"]);
            Assert.AreEqual("Open Settings", denied["actionButtonTitle"]);
            Assert.AreEqual("Not now", denied["closeButtonTitle"]);
        }

        [Test]
        public void EventTrackingBehavior_IsSentUnderTheNativeNames()
        {
            // SuperwallKit and superwall-android both name them all, superwallOnly and none.
            LogAssert.Expect(LogType.Log, "[Superwall] SetEventTrackingBehavior(superwallOnly)");
            LogAssert.Expect(LogType.Log, "[Superwall] SetEventTrackingBehavior(none)");
            var superwall = UnconfiguredInstance();
            superwall.EventTrackingBehavior = EventTrackingBehavior.SuperwallOnly;
            superwall.EventTrackingBehavior = EventTrackingBehavior.None;
        }

        [Test]
        public void AdConsent_IsSentAsBothStatuses()
        {
            LogAssert.Expect(LogType.Log, "[Superwall] SetAdConsent({\"adUserData\":\"denied\",\"adPersonalization\":\"granted\"})");
            UnconfiguredInstance().AdConsent = new AdConsent { AdUserData = AdConsentStatus.Denied };
        }

        [Test]
        public void SetInterfaceStyle_NullFollowsTheSystem()
        {
            LogAssert.Expect(LogType.Log, "[Superwall] SetInterfaceStyle(dark)");
            LogAssert.Expect(LogType.Log, "[Superwall] SetInterfaceStyle()");
            var superwall = UnconfiguredInstance();
            superwall.SetInterfaceStyle(InterfaceStyle.Dark);
            superwall.SetInterfaceStyle(null);
        }

        [Test]
        public void Notifications_OptInAndOutAndOpensReachTheBridge()
        {
            LogAssert.Expect(LogType.Log, "[Superwall] SetNotificationsEnabled(False)");
            LogAssert.Expect(LogType.Log, "[Superwall] SetNotificationsEnabled(True)");
            LogAssert.Expect(LogType.Log, "[Superwall] HandleNotificationOpen({\"superwall_data\":\"payload\"})");
            var superwall = UnconfiguredInstance();
            superwall.OptOutOfNotifications();
            superwall.OptInToNotifications();
            superwall.HandleNotificationOpen(new Dictionary<string, string> { { "superwall_data", "payload" } });
        }

        [Test]
        public void HandleDeepLink_WorksBeforeConfigure()
        {
            // The instance method needs Superwall.Shared, which is null until Configure: a link that launched the app
            // couldn't be handed over. The static one goes straight to the native static handlers.
            LogAssert.Expect(LogType.Log, "[Superwall] HandleDeepLink(myapp://superwall/redeem?code=abc)");
            Assert.IsFalse(global::Superwall.Superwall.HandleDeepLink(new Uri("myapp://superwall/redeem?code=abc")));
            Assert.IsFalse(global::Superwall.Superwall.HandleDeepLink((Uri)null));
        }

        [Test]
        public void PackageVersion_MatchesPackageJson()
        {
            // Sent to the native SDKs as the wrapper version, so it must move with every release.
            var packageJson = Path.Combine(Path.GetDirectoryName(ThisFile()), "..", "..", "package.json");
            var package = (Dictionary<string, object>)Json.Deserialize(File.ReadAllText(packageJson));
            var field = typeof(global::Superwall.Superwall).GetField("PackageVersion",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.AreEqual(package["version"], field.GetValue(null));
        }

        private static string ThisFile([CallerFilePath] string path = "") => path;

        // --- Presentation handler ---

        [UnityTest]
        public IEnumerator OnWillDismiss_ReceivesThePaywallAndResult()
        {
            PaywallInfo info = null;
            PaywallResult result = null;
            var handler = new PaywallPresentationHandler { OnWillDismiss = (i, r) => { info = i; result = r; } };
            BridgeCallbackHandler.Instance.RegisterPresentationHandler("will-dismiss", handler);

            Send("onWillDismiss", new Dictionary<string, object>
            {
                { "handlerId", "will-dismiss" },
                { "paywallInfo", new Dictionary<string, object> { { "identifier", "pw" } } },
                { "result", new Dictionary<string, object> { { "type", "purchased" }, { "productId", "pro_monthly" } } },
            });
            yield return null;

            Assert.AreEqual("pw", info?.Identifier);
            Assert.IsNotNull(result);
            BridgeCallbackHandler.Instance.UnregisterPresentationHandler("will-dismiss");
        }

        [UnityTest]
        public IEnumerator OnSkip_UserIsSubscribed_IsNotReportedAsNoAudienceMatch()
        {
            // Regression: Android's "userIsSubscribed" fell through to NoAudienceMatch.
            PaywallSkippedReason? reason = null;
            BridgeCallbackHandler.Instance.RegisterPresentationHandler("skip",
                new PaywallPresentationHandler { OnSkip = r => reason = r });

            Send("onSkip", new Dictionary<string, object> { { "handlerId", "skip" }, { "reason", "userIsSubscribed" } });
            yield return null;

            Assert.AreEqual(PaywallSkippedReason.UserIsSubscribed, reason);
            BridgeCallbackHandler.Instance.UnregisterPresentationHandler("skip");
        }
    }
}
