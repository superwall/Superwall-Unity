using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Superwall.Internal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Superwall.Tests
{
    /// <summary>
    /// Regression specs for bridge fixes that shipped without a test: each test feeds the payload the native
    /// bridges send today and asserts what the C# side must surface. Grouped by the fix they protect.
    /// The native halves are covered where a harness exists (ci~/android-harness/bridge-tests).
    /// </summary>
    class BridgeRegressionTests
    {
        private RegressionDelegate _delegate;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;
            _delegate = new RegressionDelegate();
            BridgeCallbackHandler.Instance.Delegate = _delegate;
        }

        [TearDown]
        public void TearDown()
        {
            BridgeCallbackHandler.Instance.Delegate = null;
        }

        private static void Send(string method, Dictionary<string, object> data)
        {
            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", method },
                { "data", data },
            }));
        }

        private static Dictionary<string, object> Event(string eventType, Dictionary<string, object> extra = null)
        {
            var data = new Dictionary<string, object> { { "eventType", eventType }, { "params", new Dictionary<string, object>() } };
            if (extra != null)
            {
                foreach (var pair in extra) data[pair.Key] = pair.Value;
            }
            return data;
        }

        // --- SubscriptionStatus keeps its entitlements in both directions ---

        [UnityTest]
        public IEnumerator SubscriptionStatus_ActiveFromNative_CarriesEntitlements()
        {
            // Regression: the status read from native came back with an empty entitlement list.
            Send("subscriptionStatusDidChange", new Dictionary<string, object>
            {
                { "from", new Dictionary<string, object> { { "type", "inactive" } } },
                { "to", new Dictionary<string, object>
                    {
                        { "type", "active" },
                        { "entitlements", new List<object>
                            {
                                new Dictionary<string, object> { { "id", "premium" }, { "isActive", true } },
                            }
                        },
                    }
                },
            });
            yield return null;

            Assert.AreEqual(SubscriptionStatus.StatusType.Inactive, _delegate.From.Type);
            var active = _delegate.To as SubscriptionStatus.ActiveStatus;
            Assert.IsNotNull(active, "to must be an ActiveStatus");
            Assert.AreEqual(1, active.Entitlements.Count);
            Assert.AreEqual("premium", active.Entitlements[0].Id);
        }

        [Test]
        public void SubscriptionStatus_SetterSendsTheActiveEntitlementIds()
        {
            // Regression: the setter sent type-only JSON, which the native SDKs read as Inactive.
            // In the Editor the native call logs its payload, which is what this asserts on. A private
            // instance is used so the test does not configure the shared SDK for the rest of the run.
            LogAssert.Expect(LogType.Log,
                new Regex(@"SetSubscriptionStatus\(\{""type"":""active"",""entitlements"":\[\{""id"":""premium""\}\]\}\)"));

            var sdk = (global::Superwall.Superwall)System.Activator.CreateInstance(typeof(global::Superwall.Superwall), nonPublic: true);
            sdk.SubscriptionStatus = SubscriptionStatus.CreateActive(new List<Entitlement>
            {
                new Entitlement { Id = "premium", IsActive = true },
                new Entitlement { Id = "expired", IsActive = false },
            });
        }

        // --- EventType parses every naming the bridges send ---

        [UnityTest]
        public IEnumerator EventType_ParsesSnakeCamelAndRemappedNames()
        {
            // Regression: SuperwallEventInfo.EventType never matched the enum for Android's snake_case names.
            var cases = new Dictionary<string, EventType>
            {
                { "transaction_complete", EventType.TransactionComplete },   // Android rawName
                { "transactionComplete", EventType.TransactionComplete },    // iOS case name
                { "freeTrial_start", EventType.FreeTrialStart },
                { "deepLink_open", EventType.DeepLink },                      // remapped
                { "paywallWebviewLoad_processTerminated", EventType.PaywallWebviewProcessTerminated }, // remapped
            };

            foreach (var pair in cases)
            {
                _delegate.LastEvent = null;
                Send("handleSuperwallEvent", Event(pair.Key));
                yield return null;
                Assert.IsNotNull(_delegate.LastEvent, pair.Key);
                Assert.AreEqual(pair.Value, _delegate.LastEvent.EventType, pair.Key);
            }
        }

        // --- Delegate events carry product, transaction and paywall info ---

        [UnityTest]
        public IEnumerator TransactionComplete_CarriesProductTransactionAndPaywallInfo()
        {
            Send("handleSuperwallEvent", Event("transaction_complete", new Dictionary<string, object>
            {
                { "product", new Dictionary<string, object> { { "productIdentifier", "pro.yearly" }, { "price", "59.99" } } },
                { "transaction", new Dictionary<string, object>
                    {
                        { "storeTransactionId", "GPA.1234" },
                        { "originalTransactionIdentifier", "orig-1" },
                        { "transactionDate", "2026-10-29T12:00:00Z" },
                    }
                },
                { "paywallInfo", new Dictionary<string, object> { { "identifier", "pw_1" }, { "name", "Main" } } },
            }));
            yield return null;

            var info = _delegate.LastEvent;
            Assert.IsNotNull(info);
            Assert.AreEqual("pro.yearly", info.Product?.ProductIdentifier);
            Assert.AreEqual("GPA.1234", info.Transaction?.StoreTransactionId);
            Assert.AreEqual("orig-1", info.Transaction?.OriginalTransactionIdentifier);
            Assert.AreEqual("2026-10-29T12:00:00Z", info.Transaction?.TransactionDate);
            Assert.AreEqual("pw_1", info.PaywallInfo?.Identifier);
        }

        [UnityTest]
        public IEnumerator EventWithoutPayload_LeavesTheFieldsNull()
        {
            Send("handleSuperwallEvent", Event("app_open"));
            yield return null;

            Assert.IsNull(_delegate.LastEvent.Product);
            Assert.IsNull(_delegate.LastEvent.Transaction);
            Assert.IsNull(_delegate.LastEvent.PaywallInfo);
        }

        // --- Prices parse culture-invariantly ---

        [UnityTest]
        public IEnumerator Price_ParsesTheSameUnderACommaDecimalCulture()
        {
            // Regression: Android sends prices as strings ("199.99"); under pt-BR they parsed as 19999.
            CultureInfo previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
            try
            {
                Send("handleSuperwallEvent", Event("transaction_start", new Dictionary<string, object>
                {
                    { "product", new Dictionary<string, object>
                        {
                            { "productIdentifier", "pro.yearly" },
                            { "price", "199.99" },             // Android: BigDecimal.toString()
                            { "trialPeriodPrice", 1.99 },      // iOS: a JSON number
                        }
                    },
                }));
                yield return null;
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }

            Assert.AreEqual(199.99, _delegate.LastEvent.Product.Price, 1e-9);
            Assert.AreEqual(1.99, _delegate.LastEvent.Product.TrialPeriodPrice, 1e-9);
        }

        // --- Entitlements use one vocabulary across both bridges ---

        [UnityTest]
        public IEnumerator Entitlement_AllFieldsAndTheSharedVocabularyDeserialize()
        {
            // The native bridges emit these names (Android maps GRACE_PERIOD -> inGracePeriod and
            // BILLING_RETRY -> inBillingRetryPeriod); dates are epoch milliseconds.
            Send("subscriptionStatusDidChange", new Dictionary<string, object>
            {
                { "from", new Dictionary<string, object> { { "type", "unknown" } } },
                { "to", new Dictionary<string, object>
                    {
                        { "type", "active" },
                        { "entitlements", new List<object>
                            {
                                new Dictionary<string, object>
                                {
                                    { "id", "premium" },
                                    { "type", "serviceLevel" },
                                    { "isActive", true },
                                    { "productIds", new List<object> { "pro.yearly" } },
                                    { "latestProductId", "pro.yearly" },
                                    { "store", "playStore" },
                                    { "startsAt", 1761739200000L },
                                    { "renewedAt", 1761739200000L },
                                    { "expiresAt", 1793275200000L },
                                    { "isLifetime", false },
                                    { "willRenew", true },
                                    { "state", "inGracePeriod" },
                                    { "offerType", "trial" },
                                },
                                new Dictionary<string, object>
                                {
                                    { "id", "extras" },
                                    { "isActive", true },
                                    { "state", "inBillingRetryPeriod" },
                                    { "offerType", "subscription" }, // Android-only value: no C# member
                                },
                            }
                        },
                    }
                },
            });
            yield return null;

            var entitlements = ((SubscriptionStatus.ActiveStatus)_delegate.To).Entitlements;
            var premium = entitlements[0];
            Assert.AreEqual("pro.yearly", premium.LatestProductId);
            Assert.AreEqual(ProductStore.PlayStore, premium.Store);
            Assert.AreEqual(1761739200000L, premium.StartsAt);
            Assert.AreEqual(1761739200000L, premium.RenewedAt);
            Assert.AreEqual(1793275200000L, premium.ExpiresAt);
            Assert.AreEqual(false, premium.IsLifetime);
            Assert.AreEqual(true, premium.WillRenew);
            Assert.AreEqual(LatestSubscriptionState.InGracePeriod, premium.State);
            Assert.AreEqual(LatestSubscriptionOfferType.Trial, premium.OfferType);

            Assert.AreEqual(LatestSubscriptionState.InBillingRetryPeriod, entitlements[1].State);
            Assert.IsNull(entitlements[1].OfferType, "an Android-only offer type must stay null, not be mislabelled");
        }
    }

    class RegressionDelegate : ISuperwallDelegate
    {
        public SubscriptionStatus From;
        public SubscriptionStatus To;
        public SuperwallEventInfo LastEvent;

        public void SubscriptionStatusDidChange(SubscriptionStatus from, SubscriptionStatus to)
        {
            From = from;
            To = to;
        }

        public void HandleSuperwallEvent(SuperwallEventInfo eventInfo) => LastEvent = eventInfo;
        public void HandleCustomPaywallAction(string name) { }
        public void WillDismissPaywall(PaywallInfo paywallInfo) { }
        public void WillPresentPaywall(PaywallInfo paywallInfo) { }
        public void DidDismissPaywall(PaywallInfo paywallInfo) { }
        public void DidPresentPaywall(PaywallInfo paywallInfo) { }
        public void PaywallWillOpenURL(string url) { }
        public void PaywallWillOpenDeepLink(string url) { }
        public void HandleLog(string level, string scope, string message, Dictionary<string, object> info, string error) { }
        public void WillRedeemLink() { }
        public void DidRedeemLink(RedemptionResult result) { }
        public void HandleSuperwallDeepLink(string fullURL, List<string> pathComponents, Dictionary<string, string> queryParameters) { }
        public void CustomerInfoDidChange(CustomerInfo from, CustomerInfo to) { }
        public void UserAttributesDidChange(Dictionary<string, object> newAttributes) { }
    }
}
