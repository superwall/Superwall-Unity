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

        // --- Integration attributes reach native under the native names ---

        private static global::Superwall.Superwall UnconfiguredInstance() =>
            (global::Superwall.Superwall)System.Activator.CreateInstance(typeof(global::Superwall.Superwall), nonPublic: true);

        [Test]
        public void IntegrationAttribute_IsSentUnderItsNativeName()
        {
            // Regression: C# sent "AdjustId"; both bridges match "adjustId" exactly and dropped every call.
            LogAssert.Expect(LogType.Log, "[Superwall] SetIntegrationAttribute(adjustId, adj-123)");
            UnconfiguredInstance().SetIntegrationAttribute(IntegrationAttribute.AdjustId, "adj-123");
        }

        [Test]
        public void IntegrationAttributes_EveryMemberIsANativeName_AndNullIsKept()
        {
            // The names SuperwallKit's IntegrationAttribute and superwall-android's AttributionProvider use.
            var nativeNames = new HashSet<string>
            {
                "adjustId", "amplitudeDeviceId", "amplitudeUserId", "appsflyerId", "brazeAliasName", "brazeAliasLabel",
                "onesignalId", "fbAnonId", "firebaseAppInstanceId", "iterableUserId", "iterableCampaignId",
                "iterableTemplateId", "mixpanelDistinctId", "mparticleId", "clevertapId", "airshipChannelId",
                "kochavaDeviceId", "tenjinId", "posthogUserId", "customerioId", "appstackId",
            };

            var attributes = new Dictionary<IntegrationAttribute, string>();
            foreach (IntegrationAttribute attribute in System.Enum.GetValues(typeof(IntegrationAttribute)))
                attributes[attribute] = "id";
            attributes[IntegrationAttribute.TenjinId] = null; // null removes the attribute natively

            string logged = null;
            Application.LogCallback capture = (condition, stackTrace, type) =>
            {
                if (condition.StartsWith("[Superwall] SetIntegrationAttributes(")) logged = condition;
            };
            Application.logMessageReceived += capture;
            try
            {
                UnconfiguredInstance().SetIntegrationAttributes(attributes);
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }

            Assert.IsNotNull(logged);
            var json = logged.Substring("[Superwall] SetIntegrationAttributes(".Length).TrimEnd(')');
            var sent = (Dictionary<string, object>)Json.Deserialize(json);
            Assert.AreEqual(attributes.Count, sent.Count);
            foreach (var key in sent.Keys)
                Assert.IsTrue(nativeNames.Contains(key), $"'{key}' is not a native integration attribute name");
            Assert.IsTrue(sent.ContainsKey("tenjinId") && sent["tenjinId"] == null, "a null value must be sent as null");
        }

        // --- Defaults match the native SDKs ---

        [Test]
        public void Defaults_MatchNative()
        {
            Assert.IsTrue(new PaywallOptions().ShouldShowWebPurchaseConfirmationAlert,
                "SuperwallKit shows the web purchase confirmation alert by default");
            Assert.IsTrue(new Entitlement { Id = "premium" }.IsActive, "a new Entitlement is active, as Entitlement(id:) is natively");
        }

        [Test]
        public void SubscriptionStatus_SetterKeepsAnEntitlementBuiltWithOnlyItsId()
        {
            // Regression: IsActive defaulted to false, so CreateActive(new Entitlement { Id = "pro" }) was sent as
            // active with no entitlements.
            LogAssert.Expect(LogType.Log,
                new Regex(@"SetSubscriptionStatus\(\{""type"":""active"",""entitlements"":\[\{""id"":""pro""\}\]\}\)"));
            UnconfiguredInstance().SubscriptionStatus = SubscriptionStatus.CreateActive(new List<Entitlement> { new Entitlement { Id = "pro" } });
        }

        [Test]
        public void Options_RestoreFailedSendsOnlyTheTextsTheAppChanged()
        {
            // Regression: the English defaults were always sent, replacing Android's translations of them.
            var serialize = typeof(global::Superwall.Superwall).GetMethod("SerializeOptions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var options = new SuperwallOptions();
            options.Paywalls.RestoreFailed.Title = "Nothing to restore";

            var json = (Dictionary<string, object>)Json.Deserialize((string)serialize.Invoke(null, new object[] { options }));
            var paywalls = (Dictionary<string, object>)json["paywalls"];
            var restoreFailed = (Dictionary<string, object>)paywalls["restoreFailed"];
            Assert.AreEqual("Nothing to restore", restoreFailed["title"]);
            Assert.IsFalse(restoreFailed.ContainsKey("message"));
            Assert.IsFalse(restoreFailed.ContainsKey("closeButtonTitle"));
            Assert.AreEqual(true, paywalls["shouldShowWebPurchaseConfirmationAlert"]);
        }

        // --- Customer info carries its transactions ---

        [UnityTest]
        public IEnumerator CustomerInfo_CarriesSubscriptionsAndNonSubscriptions()
        {
            // Regression: Subscriptions and NonSubscriptions were always null.
            Send("customerInfoDidChange", new Dictionary<string, object>
            {
                { "from", new Dictionary<string, object> { { "userId", "u" } } },
                { "to", new Dictionary<string, object>
                    {
                        { "userId", "u" },
                        { "entitlements", new List<object>() },
                        { "subscriptions", new List<object>
                            {
                                new Dictionary<string, object>
                                {
                                    { "transactionId", "t1" }, { "productId", "pro.yearly" },
                                    { "purchaseDate", 1761739200000L }, { "willRenew", true }, { "isRevoked", false },
                                    { "isInGracePeriod", false }, { "isInBillingRetryPeriod", true }, { "isActive", true },
                                    { "expirationDate", 1793275200000L }, { "offerType", "trial" },
                                    { "subscriptionGroupId", "group" }, { "store", "appStore" },
                                },
                            }
                        },
                        { "nonSubscriptions", new List<object>
                            {
                                new Dictionary<string, object>
                                {
                                    { "transactionId", "t2" }, { "productId", "coins" }, { "purchaseDate", 1761739200000L },
                                    { "isConsumable", true }, { "isRevoked", false }, { "store", "playStore" },
                                },
                            }
                        },
                    }
                },
            });
            yield return null;

            var info = _delegate.CustomerInfoTo;
            Assert.AreEqual(1, info.Subscriptions.Count);
            var subscription = info.Subscriptions[0];
            Assert.AreEqual("t1", subscription.TransactionId);
            Assert.AreEqual("pro.yearly", subscription.ProductId);
            Assert.AreEqual(1761739200000L, subscription.PurchaseDate);
            Assert.IsTrue(subscription.WillRenew);
            Assert.IsTrue(subscription.IsInBillingRetryPeriod);
            Assert.IsTrue(subscription.IsActive);
            Assert.AreEqual(1793275200000L, subscription.ExpirationDate);
            Assert.AreEqual(LatestSubscriptionOfferType.Trial, subscription.OfferType);
            Assert.AreEqual("group", subscription.SubscriptionGroupId);
            Assert.AreEqual(ProductStore.AppStore, subscription.Store);

            Assert.AreEqual(1, info.NonSubscriptions.Count);
            Assert.AreEqual("coins", info.NonSubscriptions[0].ProductId);
            Assert.IsTrue(info.NonSubscriptions[0].IsConsumable);
            Assert.AreEqual(ProductStore.PlayStore, info.NonSubscriptions[0].Store);
        }

        // --- Event payloads fill the SuperwallEventInfo fields ---

        [UnityTest]
        public IEnumerator EventPayloads_FillTheirFields()
        {
            // Regression: 19 of the fields were never set on either platform.
            Send("handleSuperwallEvent", Event("trigger_fire", new Dictionary<string, object>
            {
                { "placementName", "campaign_trigger" },
                { "result", new Dictionary<string, object>
                    {
                        { "type", "holdout" },
                        { "experiment", new Dictionary<string, object> { { "id", "exp" }, { "groupId", "g" },
                            { "variant", new Dictionary<string, object> { { "id", "v" }, { "type", "holdout" } } } } },
                    }
                },
            }));
            yield return null;
            Assert.AreEqual("campaign_trigger", _delegate.LastEvent.PlacementName);
            var holdout = _delegate.LastEvent.Result as TriggerResult.HoldoutResult;
            Assert.IsNotNull(holdout);
            Assert.AreEqual("exp", holdout.Experiment.Id);

            Send("handleSuperwallEvent", Event("survey_response", new Dictionary<string, object>
            {
                { "survey", new Dictionary<string, object> { { "id", "s1" }, { "assignmentKey", "k" }, { "title", "Why?" } } },
                { "selectedOption", new Dictionary<string, object> { { "id", "o1" }, { "text", "Too expensive" } } },
                { "customResponse", "more" },
                { "paywallInfo", new Dictionary<string, object> { { "identifier", "pw_1" } } },
            }));
            yield return null;
            Assert.AreEqual("s1", _delegate.LastEvent.Survey.Id);
            Assert.AreEqual("k", _delegate.LastEvent.Survey.AssignmentKey);
            Assert.AreEqual("Too expensive", _delegate.LastEvent.SelectedOption.Text);
            Assert.AreEqual("more", _delegate.LastEvent.CustomResponse);
            Assert.AreEqual("pw_1", _delegate.LastEvent.PaywallInfo.Identifier);

            Send("handleSuperwallEvent", Event("paywallPresentationRequest", new Dictionary<string, object> { { "status", "noPresentation" } }));
            yield return null;
            Assert.AreEqual(PaywallPresentationRequestStatusType.NoPresentation, _delegate.LastEvent.Status);

            Send("handleSuperwallEvent", Event("paywallProductsLoad_missingProducts", new Dictionary<string, object>
            {
                { "triggeredPlacementName", "campaign_trigger" },
                { "missingProductIdentifiers", new List<object> { "pro.monthly" } },
                { "attempt", 2 },
            }));
            yield return null;
            Assert.AreEqual("campaign_trigger", _delegate.LastEvent.TriggeredPlacementName);
            CollectionAssert.AreEqual(new List<string> { "pro.monthly" }, _delegate.LastEvent.MissingProductIdentifiers);
            Assert.AreEqual(2, _delegate.LastEvent.Attempt);

            Send("handleSuperwallEvent", Event("restoreFail", new Dictionary<string, object> { { "message", "No purchases" } }));
            yield return null;
            Assert.AreEqual("No purchases", _delegate.LastEvent.Message);

            Send("handleSuperwallEvent", Event("deepLink_open", new Dictionary<string, object> { { "deepLinkUrl", "myapp://x" } }));
            yield return null;
            Assert.AreEqual("myapp://x", _delegate.LastEvent.DeepLinkUrl);

            Send("handleSuperwallEvent", Event("review_requested", new Dictionary<string, object> { { "reviewRequestedCount", 3 } }));
            yield return null;
            Assert.AreEqual(3, _delegate.LastEvent.ReviewRequestedCount);

            Send("handleSuperwallEvent", Event("enrichment_complete", new Dictionary<string, object>
            {
                { "userEnrichment", new Dictionary<string, object> { { "a", 1 } } },
                { "deviceEnrichment", new Dictionary<string, object> { { "b", 2 } } },
            }));
            yield return null;
            Assert.IsTrue(_delegate.LastEvent.UserEnrichment.ContainsKey("a"));
            Assert.IsTrue(_delegate.LastEvent.DeviceEnrichment.ContainsKey("b"));

            Send("handleSuperwallEvent", Event("custom_placement", new Dictionary<string, object> { { "name", "tapped_buy" } }));
            yield return null;
            Assert.AreEqual("tapped_buy", _delegate.LastEvent.Name);

            Send("handleSuperwallEvent", Event("user_attributes", new Dictionary<string, object>
            {
                { "userAttributes", new Dictionary<string, object> { { "level", 4 } } },
            }));
            yield return null;
            Assert.IsTrue(_delegate.LastEvent.UserAttributes.ContainsKey("level"));

            Send("handleSuperwallEvent", Event("integration_attributes", new Dictionary<string, object>
            {
                { "integrationAttributes", new Dictionary<string, object> { { "adjustId", "a" } } },
            }));
            yield return null;
            Assert.AreEqual("a", _delegate.LastEvent.IntegrationAttributes["adjustId"]);

            Send("handleSuperwallEvent", Event("device_attributes", new Dictionary<string, object>
            {
                { "deviceAttributes", new Dictionary<string, object> { { "osVersion", "17" } } },
            }));
            yield return null;
            Assert.AreEqual("17", _delegate.LastEvent.DeviceAttributes["osVersion"]);

            Send("handleSuperwallEvent", Event("adServicesTokenRequest_complete", new Dictionary<string, object> { { "token", "tok" } }));
            yield return null;
            Assert.AreEqual("tok", _delegate.LastEvent.Token);
        }

        // --- Redemption info is complete ---

        [UnityTest]
        public IEnumerator DidRedeemLink_CarriesOwnershipPurchaserAndPaywall()
        {
            // Regression: only the entitlements of a redemption reached C#.
            Send("didRedeemLink", new Dictionary<string, object>
            {
                { "type", "success" },
                { "code", "CODE" },
                { "redemptionInfo", new Dictionary<string, object>
                    {
                        { "ownership", new Dictionary<string, object> { { "type", "device" }, { "deviceId", "d1" } } },
                        { "purchaserInfo", new Dictionary<string, object>
                            {
                                { "appUserId", "buyer" },
                                { "email", "buyer@example.com" },
                                { "storeIdentifiers", new Dictionary<string, object>
                                    {
                                        { "type", "unknown" },
                                        { "store", "SUPERWALL" },
                                        { "additionalInfo", new Dictionary<string, object> { { "ref", "x" } } },
                                    }
                                },
                            }
                        },
                        { "paywallInfo", new Dictionary<string, object>
                            {
                                { "identifier", "pw_1" }, { "placementName", "p" },
                                { "placementParams", new Dictionary<string, object> { { "k", "v" } } },
                                { "variantId", "var" }, { "experimentId", "exp" },
                            }
                        },
                        { "entitlements", new List<object> { new Dictionary<string, object> { { "id", "premium" }, { "isActive", true } } } },
                    }
                },
            });
            yield return null;

            var success = _delegate.LastRedemption as RedemptionResult.SuccessResult;
            Assert.IsNotNull(success);
            var info = success.RedemptionInfo;
            var device = info.Ownership as Ownership.DeviceOwnership;
            Assert.IsNotNull(device);
            Assert.AreEqual("d1", device.DeviceId);
            Assert.AreEqual("buyer", info.PurchaserInfo.AppUserId);
            Assert.AreEqual("buyer@example.com", info.PurchaserInfo.Email);
            var unknown = info.PurchaserInfo.StoreIdentifiers as StoreIdentifiers.UnknownStoreIdentifiers;
            Assert.IsNotNull(unknown);
            Assert.AreEqual("SUPERWALL", unknown.Store);
            Assert.AreEqual("x", unknown.AdditionalInfo["ref"]);
            Assert.AreEqual("pw_1", info.PaywallInfo.Identifier);
            Assert.AreEqual("v", info.PaywallInfo.PlacementParams["k"]);
            Assert.AreEqual("var", info.PaywallInfo.VariantId);
            Assert.AreEqual("exp", info.PaywallInfo.ExperimentId);
            Assert.AreEqual("premium", info.Entitlements[0].Id);
        }

        // --- HandleLog passes its info on ---

        [UnityTest]
        public IEnumerator HandleLog_PassesInfo()
        {
            Send("handleLog", new Dictionary<string, object>
            {
                { "level", "debug" }, { "scope", "network" }, { "message", "request" },
                { "info", new Dictionary<string, object> { { "url", "https://api" } } },
            });
            yield return null;
            Assert.AreEqual("https://api", _delegate.LastLogInfo["url"]);
        }
    }

    class RegressionDelegate : ISuperwallDelegate
    {
        public SubscriptionStatus From;
        public SubscriptionStatus To;
        public SuperwallEventInfo LastEvent;
        public Dictionary<string, object> LastLogInfo;
        public RedemptionResult LastRedemption;
        public CustomerInfo CustomerInfoTo;

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
        public void HandleLog(string level, string scope, string message, Dictionary<string, object> info, string error) => LastLogInfo = info;
        public void WillRedeemLink() { }
        public void DidRedeemLink(RedemptionResult result) => LastRedemption = result;
        public void HandleSuperwallDeepLink(string fullURL, List<string> pathComponents, Dictionary<string, string> queryParameters) { }
        public void CustomerInfoDidChange(CustomerInfo from, CustomerInfo to) => CustomerInfoTo = to;
        public void UserAttributesDidChange(Dictionary<string, object> newAttributes) { }
    }
}
