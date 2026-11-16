using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Superwall.Internal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Superwall.Tests
{
    /// <summary>
    /// The parts of the parity API that have a life in C#: an embedded paywall from show to remove, the Configure
    /// result, teardown followed by a second Configure, completions where there is no native SDK, and a delegate
    /// that implements one method. What the native side does with each call is covered by the bridge tests in ci~/.
    /// </summary>
    class ParityLifecycleTests
    {
        private class OneMethodDelegate : ISuperwallDelegate
        {
            public List<string> Urls = new List<string>();
            public void PaywallWillOpenURL(string url) { Urls.Add(url); }
        }

        private class NoMethodDelegate : ISuperwallDelegate { }

        private class EmptyPurchaseController : IPurchaseController
        {
            public void PurchaseFromAppStore(string productId, Action<PurchaseResult> completion) { }
            public void PurchaseFromGooglePlay(string productId, string basePlanId, string offerId, Action<PurchaseResult> completion) { }
            public void RestorePurchases(Action<RestorationResult> completion) { }
        }

        private readonly List<(string call, string viewId, float[] rect)> _nativeCalls = new List<(string, string, float[])>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;
            _nativeCalls.Clear();
            global::Superwall.Superwall.EmbeddedPaywallCallsWithoutNative = (call, viewId, rect) => _nativeCalls.Add((call, viewId, rect));
        }

        [TearDown]
        public void TearDown()
        {
            global::Superwall.Superwall.EmbeddedPaywallCallsWithoutNative = null;
            // Also leaves no configured instance behind for the other fixtures.
            global::Superwall.Superwall.ResetAfterTeardown();
        }

        private static global::Superwall.Superwall UnconfiguredInstance() =>
            (global::Superwall.Superwall)Activator.CreateInstance(typeof(global::Superwall.Superwall), nonPublic: true);

        private static void Send(string method, Dictionary<string, object> data)
        {
            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", method },
                { "data", data },
            }));
        }

        private static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++) yield return null;
        }

        // --- Embedded paywall ---

        [Test]
        public void EmbeddedPaywall_IsShownMovedAndRemoved_AsFractionsOfTheScreen()
        {
            var superwall = UnconfiguredInstance();
            float width = Screen.width, height = Screen.height;

            // The bottom half of the screen. Unity measures y from the bottom, native from the top.
            var paywall = superwall.ShowEmbeddedPaywall("campaign", new Rect(0, 0, width, height / 2));
            Assert.AreEqual(1, _nativeCalls.Count);
            Assert.AreEqual("show", _nativeCalls[0].call);
            var viewId = _nativeCalls[0].viewId;
            CollectionAssert.AreEqual(new[] { 0f, 0.5f, 1f, 0.5f }, _nativeCalls[0].rect);

            // The top quarter, inset by a tenth of the width on each side.
            paywall.SetRect(new Rect(width * 0.1f, height * 0.75f, width * 0.8f, height * 0.25f));
            Assert.AreEqual("setRect", _nativeCalls[1].call);
            Assert.AreEqual(viewId, _nativeCalls[1].viewId);
            Assert.AreEqual(0.1f, _nativeCalls[1].rect[0], 0.0001f);
            Assert.AreEqual(0f, _nativeCalls[1].rect[1], 0.0001f);
            Assert.AreEqual(0.8f, _nativeCalls[1].rect[2], 0.0001f);
            Assert.AreEqual(0.25f, _nativeCalls[1].rect[3], 0.0001f);

            paywall.Remove();
            Assert.AreEqual("remove", _nativeCalls[2].call);
            Assert.AreEqual(viewId, _nativeCalls[2].viewId);
            Assert.IsTrue(paywall.IsRemoved);

            // Nothing more reaches native for a paywall that is gone.
            paywall.SetRect(new Rect(0, 0, width, height));
            paywall.Remove();
            Assert.AreEqual(3, _nativeCalls.Count);
        }

        [Test]
        public void EmbeddedPaywalls_AreToldApart()
        {
            var superwall = UnconfiguredInstance();
            var first = superwall.ShowEmbeddedPaywall("banner", new Rect(0, 0, 100, 100));
            var second = superwall.ShowEmbeddedPaywall("footer", new Rect(0, 0, 100, 100));
            Assert.AreNotEqual(_nativeCalls[0].viewId, _nativeCalls[1].viewId);

            second.Remove();
            Assert.AreEqual(_nativeCalls[1].viewId, _nativeCalls[2].viewId);
            Assert.IsFalse(first.IsRemoved);
            Assert.IsTrue(second.IsRemoved);
            first.Remove();
        }

        [Test]
        public void EmbeddedPaywall_ThatFinishedForGood_IsNotRemovedFromNativeAgain()
        {
            var superwall = UnconfiguredInstance();
            var finished = 0;
            var paywall = superwall.ShowEmbeddedPaywall("campaign", new Rect(0, 0, 100, 100), null,
                new EmbeddedPaywallHandler { OnFinished = (info, result, shouldDismiss) => finished++ });
            var viewId = _nativeCalls[0].viewId;

            Send("embeddedPaywallOnFinished", new Dictionary<string, object>
            {
                { "viewId", viewId }, { "shouldDismiss", true }, { "paywallInfo", new Dictionary<string, object>() },
                { "result", new Dictionary<string, object> { { "type", "restored" } } },
            });

            Assert.AreEqual(1, finished);
            Assert.IsTrue(paywall.IsRemoved);
            // Native took the view down itself before it reported.
            paywall.Remove();
            Assert.AreEqual(1, _nativeCalls.Count);
        }

        [Test]
        public void EmbeddedPaywall_ThatIsSkippedOrFails_TellsNativeToForgetIt()
        {
            var superwall = UnconfiguredInstance();
            var skipped = superwall.ShowEmbeddedPaywall("campaign", new Rect(0, 0, 100, 100));
            var failed = superwall.ShowEmbeddedPaywall("campaign", new Rect(0, 0, 100, 100));
            var skippedId = _nativeCalls[0].viewId;
            var failedId = _nativeCalls[1].viewId;

            // No handler was given: the events still end the paywall.
            Send("embeddedPaywallOnSkip", new Dictionary<string, object> { { "viewId", skippedId }, { "reason", "noAudienceMatch" } });
            Send("embeddedPaywallOnError", new Dictionary<string, object> { { "viewId", failedId }, { "error", "boom" } });

            Assert.AreEqual(("remove", skippedId), (_nativeCalls[2].call, _nativeCalls[2].viewId));
            Assert.AreEqual(("remove", failedId), (_nativeCalls[3].call, _nativeCalls[3].viewId));
            Assert.IsTrue(skipped.IsRemoved);
            Assert.IsTrue(failed.IsRemoved);
        }

        [Test]
        public void EmbeddedPaywall_ThatStaysUpAfterAPurchase_CanStillBeMovedAndRemoved()
        {
            var superwall = UnconfiguredInstance();
            var paywall = superwall.ShowEmbeddedPaywall("campaign", new Rect(0, 0, 100, 100));
            var viewId = _nativeCalls[0].viewId;

            Send("embeddedPaywallOnFinished", new Dictionary<string, object>
            {
                { "viewId", viewId }, { "shouldDismiss", false }, { "paywallInfo", new Dictionary<string, object>() },
                { "result", new Dictionary<string, object> { { "type", "purchased" }, { "productId", "pro" } } },
            });

            Assert.IsFalse(paywall.IsRemoved);
            paywall.SetRect(new Rect(0, 0, 50, 50));
            paywall.Remove();
            Assert.AreEqual("setRect", _nativeCalls[1].call);
            Assert.AreEqual("remove", _nativeCalls[2].call);
        }

        [Test]
        public void EmbeddedPaywall_AnswersItsCustomCallbacks_UntilItIsRemoved()
        {
            var superwall = UnconfiguredInstance();
            var handlersBefore = BridgeCallbackHandler.Instance.PresentationHandlerCount;
            var names = new List<string>();
            var paywall = superwall.ShowEmbeddedPaywall("campaign", new Rect(0, 0, 100, 100), null, new EmbeddedPaywallHandler
            {
                OnCustomCallback = callback =>
                {
                    names.Add(callback.Name);
                    return Task.FromResult(CustomCallbackResult.Success());
                },
            });
            var viewId = _nativeCalls[0].viewId;
            var request = new Dictionary<string, object>
            {
                { "handlerId", viewId }, { "callbackId", "cb-1" },
                { "customCallback", new Dictionary<string, object> { { "name", "createAccount" } } },
            };

            Send("onCustomCallback", request);
            Assert.AreEqual(new List<string> { "createAccount" }, names);

            paywall.Remove();
            Assert.AreEqual(handlersBefore, BridgeCallbackHandler.Instance.PresentationHandlerCount);
            // Late: the paywall gets a failure, and the handler isn't run.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("No OnCustomCallback handler"));
            Send("onCustomCallback", request);
            Assert.AreEqual(1, names.Count);
        }

        // --- Configure result ---

        [Test]
        public void ConfigureResult_IsAFailure_WhenNativeSaysTheConfigurationFailed()
        {
            // What the iOS bridge sends when SuperwallKit's configuration status is `failed`, and Android on an error.
            var failed = global::Superwall.Superwall.ParseConfigurationResult(
                "{\"success\":false,\"error\":\"Superwall couldn't fetch its configuration.\"}");
            Assert.IsFalse(failed.IsSuccess);
            Assert.AreEqual(ConfigurationResult.ResultType.Failed, failed.Type);
            Assert.AreEqual("Superwall couldn't fetch its configuration.", ((ConfigurationResult.FailedResult)failed).Error);

            Assert.IsTrue(global::Superwall.Superwall.ParseConfigurationResult("{\"success\":true}").IsSuccess);
        }

        [Test]
        public void ConfigureResult_IsAFailure_WhenTheAnswerCannotBeRead()
        {
            foreach (var json in new[] { null, "", "{}", "[]", "{\"success\":\"true\"}", "{\"success\":false}", "{\"success\":false,\"error\":null}" })
            {
                var result = global::Superwall.Superwall.ParseConfigurationResult(json);
                Assert.IsFalse(result.IsSuccess, json);
                Assert.AreEqual("Unknown error", ((ConfigurationResult.FailedResult)result).Error, json);
            }
        }

        // --- Teardown, then Configure again ---

        [UnityTest]
        public IEnumerator Teardown_ForgetsEverything_AndConfigureStartsAfresh()
        {
            var handler = BridgeCallbackHandler.Instance;
            var firstController = new EmptyPurchaseController();
            var options = new SuperwallOptions();
            options.Paywalls.OnBackPressed = info => { };
            ConfigurationResult firstResult = null;

            var first = global::Superwall.Superwall.Configure("pk_first", options, firstController, result => firstResult = result);
            Assert.AreSame(first, global::Superwall.Superwall.Shared);
            first.SetDelegate(new NoMethodDelegate());
            first.RegisterPlacement("campaign", null, new PaywallPresentationHandler(), () => Assert.Fail("torn down before it ran"));
            first.ShowEmbeddedPaywall("banner", new Rect(0, 0, 100, 100));
            first.GetProducts(new List<string> { "pro" }, products => Assert.Fail("torn down before it completed"));
            handler.RegisterCustomerCenter("cc-1", null, () => Assert.Fail("torn down before it was dismissed"));

            Assert.AreSame(firstController, handler.PurchaseController);
            Assert.IsNotNull(handler.Delegate);
            Assert.IsNotNull(handler.OnBackPressed);
            Assert.Greater(handler.PendingAsyncCallbackCount, 0);
            Assert.Greater(handler.EmbeddedPaywallCount, 0);

            // Configuring again while configured changes nothing.
            LogAssert.Expect(LogType.Warning, "[Superwall] SDK has already been configured.");
            Assert.AreSame(first, global::Superwall.Superwall.Configure("pk_ignored"));
            Assert.AreSame(firstController, handler.PurchaseController);

            global::Superwall.Superwall.ResetAfterTeardown();

            Assert.IsNull(handler.Delegate);
            Assert.IsNull(handler.PurchaseController);
            Assert.IsNull(handler.OnBackPressed);
            Assert.AreEqual(0, handler.PendingAsyncCallbackCount);
            Assert.AreEqual(0, handler.PresentationHandlerCount);
            Assert.AreEqual(0, handler.EmbeddedPaywallCount);
            LogAssert.Expect(LogType.Warning, "[Superwall] SDK has not been configured. Call Superwall.Configure() first.");
            Assert.IsNull(global::Superwall.Superwall.Shared);

            // Nothing queued for the old instance runs afterwards, nor does a late native reply to it.
            yield return Frames(3);
            Send("customerCenterOnDismiss", new Dictionary<string, object> { { "presentationId", "cc-1" } });
            Assert.IsNull(firstResult);

            var secondController = new EmptyPurchaseController();
            ConfigurationResult secondResult = null;
            var second = global::Superwall.Superwall.Configure("pk_second", null, secondController, result => secondResult = result);

            Assert.AreNotSame(first, second);
            Assert.AreSame(second, global::Superwall.Superwall.Shared);
            Assert.AreSame(secondController, handler.PurchaseController);
            // The new instance was configured without the first one's options.
            Assert.IsNull(handler.OnBackPressed);
            Assert.IsNull(handler.Delegate);

            yield return Frames(3);
            Assert.IsNotNull(secondResult);
            Assert.IsTrue(secondResult.IsSuccess);
            Assert.IsNull(firstResult);
        }

        [Test]
        public void Teardown_WhereItIsNotSupported_LeavesTheInstanceConfigured()
        {
            var superwall = global::Superwall.Superwall.Configure("pk_test");
            Assert.IsFalse(global::Superwall.Superwall.Teardown());
            Assert.AreSame(superwall, global::Superwall.Superwall.Shared);
        }

        // --- No native SDK: completions arrive with the next frame ---

        [UnityTest]
        public IEnumerator WithoutANativeSdk_ACompletionRunsOnALaterFrame_FromUpdate()
        {
            var superwall = UnconfiguredInstance();
            Dictionary<string, StoreProduct> products = null;
            var featureRan = false;

            superwall.GetProducts(new List<string> { "pro" }, result => products = result);
            superwall.RegisterPlacement("campaign", feature: () => featureRan = true);
            Assert.IsNull(products, "never inside the call that asked");
            Assert.IsFalse(featureRan);

            // Nothing here drives the handler: its own Update does, as in a player.
            for (var frame = 0; frame < 5 && (products == null || !featureRan); frame++) yield return null;

            Assert.IsNotNull(products);
            Assert.IsTrue(featureRan);
        }

        [Test]
        public void WithoutANativeSdk_ACallMadeFromACompletion_WaitsForTheNextFrame()
        {
            var superwall = UnconfiguredInstance();
            var handler = BridgeCallbackHandler.Instance;
            handler.RunSimulatedCallbacks();
            var order = new List<string>();

            superwall.GetAssignments(first =>
            {
                order.Add("first");
                superwall.GetAssignments(second => order.Add("second"));
            });

            handler.RunSimulatedCallbacks();
            Assert.AreEqual(new List<string> { "first" }, order, "a native reply never arrives inside the frame that asked");
            handler.RunSimulatedCallbacks();
            Assert.AreEqual(new List<string> { "first", "second" }, order);
        }

        [Test]
        public void WithoutANativeSdk_ACompletionThatThrows_DoesNotStopTheOthers()
        {
            var superwall = UnconfiguredInstance();
            var handler = BridgeCallbackHandler.Instance;
            handler.RunSimulatedCallbacks();
            var secondRan = false;

            superwall.GetAssignments(result => throw new InvalidOperationException("game code failed"));
            superwall.GetAssignments(result => secondRan = true);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("game code failed"));
            handler.RunSimulatedCallbacks();
            Assert.IsTrue(secondRan);
            Assert.AreEqual(0, handler.PendingAsyncCallbackCount);
        }

        // --- ISuperwallDelegate's default methods ---

        private static readonly (string method, Dictionary<string, object> data)[] DelegateCallbacks =
        {
            ("subscriptionStatusDidChange", new Dictionary<string, object> { { "from", new Dictionary<string, object> { { "type", "unknown" } } }, { "to", new Dictionary<string, object> { { "type", "inactive" } } } }),
            ("handleSuperwallEvent", new Dictionary<string, object> { { "eventType", "app_open" }, { "params", new Dictionary<string, object>() } }),
            ("handleCustomPaywallAction", new Dictionary<string, object> { { "name", "help" } }),
            ("willDismissPaywall", new Dictionary<string, object> { { "identifier", "pw" } }),
            ("willPresentPaywall", new Dictionary<string, object> { { "identifier", "pw" } }),
            ("didDismissPaywall", new Dictionary<string, object> { { "identifier", "pw" } }),
            ("didPresentPaywall", new Dictionary<string, object> { { "identifier", "pw" } }),
            ("paywallWillOpenURL", new Dictionary<string, object> { { "url", "https://example.com" } }),
            ("paywallWillOpenDeepLink", new Dictionary<string, object> { { "url", "myapp://shop" } }),
            ("handleLog", new Dictionary<string, object> { { "level", "debug" }, { "scope", "network" }, { "message", "hello" } }),
            ("willRedeemLink", new Dictionary<string, object>()),
            ("didRedeemLink", new Dictionary<string, object> { { "type", "invalidCode" }, { "code", "abc" } }),
            ("handleSuperwallDeepLink", new Dictionary<string, object> { { "fullURL", "https://example.com/a" }, { "pathComponents", new List<object> { "a" } }, { "queryParameters", new Dictionary<string, object>() } }),
            ("customerInfoDidChange", new Dictionary<string, object> { { "from", new Dictionary<string, object>() }, { "to", new Dictionary<string, object>() } }),
            ("userAttributesDidChange", new Dictionary<string, object> { { "newAttributes", new Dictionary<string, object>() } }),
        };

        [Test]
        public void Delegate_ThatImplementsOneMethod_HearsThatOne_AndIgnoresTheRest()
        {
            var superwallDelegate = new OneMethodDelegate();
            BridgeCallbackHandler.Instance.Delegate = superwallDelegate;

            // An exception in any of them would be logged as an error, which fails the test.
            foreach (var (method, data) in DelegateCallbacks) Send(method, data);

            Assert.AreEqual(15, DelegateCallbacks.Length, "one for each method of ISuperwallDelegate");
            Assert.AreEqual(typeof(ISuperwallDelegate).GetMethods().Length, DelegateCallbacks.Length);
            Assert.AreEqual(new List<string> { "https://example.com" }, superwallDelegate.Urls);
        }

        [Test]
        public void Delegate_ThatImplementsNothing_IsStillADelegate()
        {
            ISuperwallDelegate superwallDelegate = new NoMethodDelegate();
            BridgeCallbackHandler.Instance.Delegate = superwallDelegate;
            foreach (var (method, data) in DelegateCallbacks) Send(method, data);

            // Called directly, as game code holding the interface would.
            superwallDelegate.SubscriptionStatusDidChange(SubscriptionStatus.CreateUnknown(), SubscriptionStatus.CreateInactive());
            superwallDelegate.HandleSuperwallEvent(new SuperwallEventInfo());
            superwallDelegate.HandleCustomPaywallAction("help");
            superwallDelegate.WillDismissPaywall(new PaywallInfo());
            superwallDelegate.WillPresentPaywall(new PaywallInfo());
            superwallDelegate.DidDismissPaywall(new PaywallInfo());
            superwallDelegate.DidPresentPaywall(new PaywallInfo());
            superwallDelegate.PaywallWillOpenURL("https://example.com");
            superwallDelegate.PaywallWillOpenDeepLink("myapp://shop");
            superwallDelegate.HandleLog("debug", "network", "hello", null, null);
            superwallDelegate.WillRedeemLink();
            superwallDelegate.DidRedeemLink(null);
            superwallDelegate.HandleSuperwallDeepLink("https://example.com", new List<string>(), new Dictionary<string, string>());
            superwallDelegate.CustomerInfoDidChange(new CustomerInfo(), new CustomerInfo());
            superwallDelegate.UserAttributesDidChange(new Dictionary<string, object>());
        }
    }
}
