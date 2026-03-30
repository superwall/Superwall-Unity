using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Superwall;
using Superwall.Internal;

namespace Superwall.Tests
{
    class BridgeCallbackHandlerTests
    {
        [UnityTest]
        public IEnumerator Initialize_CreatesPersistentGameObject()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;

            var go = GameObject.Find("SuperwallBridge");
            Assert.IsNotNull(go, "SuperwallBridge GameObject should exist");

            var handler = go.GetComponent<BridgeCallbackHandler>();
            Assert.IsNotNull(handler, "BridgeCallbackHandler component should be attached");
            Assert.AreEqual(handler, BridgeCallbackHandler.Instance);
        }

        [UnityTest]
        public IEnumerator AsyncCallback_IsInvokedAndRemoved()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;

            string receivedJson = null;
            string callbackId = "test-callback-1";

            BridgeCallbackHandler.Instance.RegisterAsyncCallback(callbackId, (json) =>
            {
                receivedJson = json;
            });

            var payload = Json.Serialize(new Dictionary<string, object>
            {
                { "method", "asyncResponse" },
                { "data", new Dictionary<string, object>
                    {
                        { "callbackId", callbackId },
                        { "result", "success" }
                    }
                }
            });

            BridgeCallbackHandler.Instance.OnCallback(payload);
            yield return null;

            Assert.IsNotNull(receivedJson, "Async callback should have been invoked");
        }

        [UnityTest]
        public IEnumerator FeatureHandler_IsInvokedOnCallback()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;

            bool featureCalled = false;
            string featureId = "feature-123";

            BridgeCallbackHandler.Instance.RegisterFeatureHandler(featureId, () =>
            {
                featureCalled = true;
            });

            var payload = Json.Serialize(new Dictionary<string, object>
            {
                { "method", "onFeature" },
                { "data", new Dictionary<string, object>
                    {
                        { "handlerId", featureId }
                    }
                }
            });

            BridgeCallbackHandler.Instance.OnCallback(payload);
            yield return null;

            Assert.IsTrue(featureCalled, "Feature handler should have been called");
        }

        [UnityTest]
        public IEnumerator PresentationHandler_OnError_IsRouted()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;

            string receivedError = null;
            string handlerId = "handler-456";
            var handler = new PaywallPresentationHandler
            {
                OnError = (err) => { receivedError = err; }
            };

            BridgeCallbackHandler.Instance.RegisterPresentationHandler(handlerId, handler);

            var payload = Json.Serialize(new Dictionary<string, object>
            {
                { "method", "onError" },
                { "data", new Dictionary<string, object>
                    {
                        { "handlerId", handlerId },
                        { "error", "paywall load failed" }
                    }
                }
            });

            BridgeCallbackHandler.Instance.OnCallback(payload);
            yield return null;

            Assert.AreEqual("paywall load failed", receivedError);
        }

        [UnityTest]
        public IEnumerator Delegate_HandleCustomPaywallAction_IsRouted()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;

            string receivedAction = null;
            var mockDelegate = new MockSuperwallDelegate();
            mockDelegate.OnCustomPaywallAction = (name) => { receivedAction = name; };

            BridgeCallbackHandler.Instance.Delegate = mockDelegate;

            var payload = Json.Serialize(new Dictionary<string, object>
            {
                { "method", "handleCustomPaywallAction" },
                { "data", new Dictionary<string, object>
                    {
                        { "name", "open_settings" }
                    }
                }
            });

            BridgeCallbackHandler.Instance.OnCallback(payload);
            yield return null;

            Assert.AreEqual("open_settings", receivedAction);
        }

        [UnityTest]
        public IEnumerator Delegate_PaywallWillOpenURL_IsRouted()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;

            string receivedUrl = null;
            var mockDelegate = new MockSuperwallDelegate();
            mockDelegate.OnPaywallWillOpenURL = (url) => { receivedUrl = url; };

            BridgeCallbackHandler.Instance.Delegate = mockDelegate;

            var payload = Json.Serialize(new Dictionary<string, object>
            {
                { "method", "paywallWillOpenURL" },
                { "data", new Dictionary<string, object>
                    {
                        { "url", "https://example.com/terms" }
                    }
                }
            });

            BridgeCallbackHandler.Instance.OnCallback(payload);
            yield return null;

            Assert.AreEqual("https://example.com/terms", receivedUrl);
        }

        [UnityTest]
        public IEnumerator OnCallback_InvalidJson_DoesNotThrow()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;

            Assert.DoesNotThrow(() =>
            {
                BridgeCallbackHandler.Instance.OnCallback("not valid json");
            });
        }

        [UnityTest]
        public IEnumerator OnCallback_MissingMethod_DoesNotThrow()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;

            var payload = Json.Serialize(new Dictionary<string, object>
            {
                { "data", new Dictionary<string, object>() }
            });

            Assert.DoesNotThrow(() =>
            {
                BridgeCallbackHandler.Instance.OnCallback(payload);
            });
        }

        [UnityTest]
        public IEnumerator OnCallback_UnknownMethod_DoesNotThrow()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;

            var payload = Json.Serialize(new Dictionary<string, object>
            {
                { "method", "totallyUnknownMethod" },
                { "data", new Dictionary<string, object>() }
            });

            Assert.DoesNotThrow(() =>
            {
                BridgeCallbackHandler.Instance.OnCallback(payload);
            });
        }

        [UnityTest]
        public IEnumerator UnregisterPresentationHandler_RemovesHandler()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;

            bool called = false;
            string handlerId = "handler-remove-test";
            var handler = new PaywallPresentationHandler
            {
                OnError = (err) => { called = true; }
            };

            BridgeCallbackHandler.Instance.RegisterPresentationHandler(handlerId, handler);
            BridgeCallbackHandler.Instance.UnregisterPresentationHandler(handlerId);

            var payload = Json.Serialize(new Dictionary<string, object>
            {
                { "method", "onError" },
                { "data", new Dictionary<string, object>
                    {
                        { "handlerId", handlerId },
                        { "error", "test" }
                    }
                }
            });

            BridgeCallbackHandler.Instance.OnCallback(payload);
            yield return null;

            Assert.IsFalse(called, "Handler should not be called after unregistering");
        }
    }

    class MockSuperwallDelegate : ISuperwallDelegate
    {
        public System.Action<string> OnCustomPaywallAction;
        public System.Action<string> OnPaywallWillOpenURL;

        public void SubscriptionStatusDidChange(SubscriptionStatus from, SubscriptionStatus to) { }
        public void HandleSuperwallEvent(SuperwallEventInfo eventInfo) { }
        public void HandleCustomPaywallAction(string name) => OnCustomPaywallAction?.Invoke(name);
        public void WillDismissPaywall(PaywallInfo paywallInfo) { }
        public void WillPresentPaywall(PaywallInfo paywallInfo) { }
        public void DidDismissPaywall(PaywallInfo paywallInfo) { }
        public void DidPresentPaywall(PaywallInfo paywallInfo) { }
        public void PaywallWillOpenURL(string url) => OnPaywallWillOpenURL?.Invoke(url);
        public void PaywallWillOpenDeepLink(string url) { }
        public void HandleLog(string level, string scope, string message, Dictionary<string, object> info, string error) { }
        public void WillRedeemLink() { }
        public void DidRedeemLink(RedemptionResult result) { }
        public void HandleSuperwallDeepLink(string fullURL, List<string> pathComponents, Dictionary<string, string> queryParameters) { }
        public void CustomerInfoDidChange(CustomerInfo from, CustomerInfo to) { }
        public void UserAttributesDidChange(Dictionary<string, object> newAttributes) { }
    }
}
