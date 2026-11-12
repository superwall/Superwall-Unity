using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Superwall.Internal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Superwall.Tests
{
    /// <summary>
    /// The C# half of the Customer Center: the configuration it sends to the native SDKs, and how the events the
    /// bridges send for one presentation reach that presentation's delegate and onDismiss.
    /// </summary>
    class CustomerCenterTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;
        }

        private static void Send(string method, Dictionary<string, object> data)
        {
            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", method },
                { "data", data },
            }));
        }

        private static Dictionary<string, object> Serialize(CustomerCenterConfiguration configuration)
        {
            var serialize = typeof(global::Superwall.Superwall).GetMethod("SerializeCustomerCenterConfiguration",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            // Through JSON, as the bridges receive it.
            return (Dictionary<string, object>)Json.Deserialize(Json.Serialize(serialize.Invoke(null, new object[] { configuration })));
        }

        private static Dictionary<string, object> Path(Dictionary<string, object> screen, int index) =>
            (Dictionary<string, object>)((List<object>)screen["paths"])[index];

        // --- Configuration ---

        [Test]
        public void Default_MatchesTheNativeDefault()
        {
            var json = Serialize(CustomerCenterConfiguration.Default);
            var management = (Dictionary<string, object>)json["managementScreen"];
            var types = new List<string>();
            foreach (Dictionary<string, object> path in (List<object>)management["paths"]) types.Add((string)path["type"]);
            CollectionAssert.AreEqual(new[] { "restore", "changePlan", "refund", "manageSubscription", "contactSupport" }, types);

            var survey = (Dictionary<string, object>)Path(management, 3)["survey"];
            Assert.AreEqual("cancel_survey", survey["id"]);
            Assert.IsFalse(survey.ContainsKey("title"), "a missing title uses the localized question");
            var optionIds = new List<string>();
            foreach (Dictionary<string, object> option in (List<object>)survey["options"]) optionIds.Add((string)option["id"]);
            CollectionAssert.AreEqual(new[] { "too_expensive", "dont_use", "bought_by_mistake" }, optionIds);

            // Left out so the native SDKs use their own path IDs and localized titles.
            Assert.IsFalse(Path(management, 0).ContainsKey("id"));
            Assert.IsFalse(Path(management, 0).ContainsKey("title"));
            var noPurchases = (Dictionary<string, object>)json["noPurchasesScreen"];
            Assert.AreEqual(1, ((List<object>)noPurchases["paths"]).Count);
            Assert.AreEqual(true, json["showsAccountDetails"]);
            Assert.IsFalse(json.ContainsKey("accent"));
        }

        [Test]
        public void Configuration_SendsEveryPathTypeWithItsValues()
        {
            var configuration = new CustomerCenterConfiguration
            {
                ManagementScreen = new CustomerCenterScreen
                {
                    Title = "Your plan",
                    Paths = new List<CustomerCenterPath>
                    {
                        CustomerCenterPath.Refund(windowMillis: 86400000),
                        CustomerCenterPath.ChangePlan(new List<string> { "pro_yearly" }),
                        CustomerCenterPath.ForUrl("https://example.com/faq", "FAQ", CustomerCenterOpenMethod.External),
                        CustomerCenterPath.Custom("sign_out", "Sign out", id: "logout"),
                    }
                },
                Support = new CustomerCenterSupport { Email = "help@example.com", WarnsAboutUpdates = false },
                Accent = new CustomerCenterColorPair { Light = "#FF0000", Dark = "#00FF00" },
                WarnsAboutDuplicateSubscriptions = false,
            };

            var json = Serialize(configuration);
            var management = (Dictionary<string, object>)json["managementScreen"];
            Assert.AreEqual("Your plan", management["title"]);
            Assert.AreEqual(86400000L, Path(management, 0)["windowMillis"]);
            CollectionAssert.AreEqual(new object[] { "pro_yearly" }, (List<object>)Path(management, 1)["productIds"]);
            Assert.AreEqual("https://example.com/faq", Path(management, 2)["url"]);
            Assert.AreEqual("external", Path(management, 2)["openMethod"]);
            Assert.AreEqual("FAQ", Path(management, 2)["title"]);
            Assert.AreEqual("sign_out", Path(management, 3)["identifier"]);
            Assert.AreEqual("logout", Path(management, 3)["id"]);
            // A screen left null is the default's: both native SDKs need one.
            Assert.AreEqual(1, ((List<object>)((Dictionary<string, object>)json["noPurchasesScreen"])["paths"]).Count);
            var support = (Dictionary<string, object>)json["support"];
            Assert.AreEqual("help@example.com", support["email"]);
            Assert.AreEqual(false, support["warnsAboutUpdates"]);
            Assert.AreEqual(true, support["checksAppStoreForUpdates"]);
            Assert.IsFalse(support.ContainsKey("latestAppVersion"));
            var accent = (Dictionary<string, object>)json["accent"];
            Assert.AreEqual("#FF0000", accent["light"]);
            Assert.AreEqual(false, json["warnsAboutDuplicateSubscriptions"]);
        }

        [Test]
        public void Options_SendTheCustomerCenterOnlyWhenSet()
        {
            var serialize = typeof(global::Superwall.Superwall).GetMethod("SerializeOptions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var unset = (Dictionary<string, object>)Json.Deserialize((string)serialize.Invoke(null, new object[] { new SuperwallOptions() }));
            Assert.IsFalse(unset.ContainsKey("customerCenter"));

            var options = new SuperwallOptions { CustomerCenter = CustomerCenterConfiguration.Default };
            var set = (Dictionary<string, object>)Json.Deserialize((string)serialize.Invoke(null, new object[] { options }));
            Assert.IsTrue(set.ContainsKey("customerCenter"));
        }

        // --- Delegate ---

        [UnityTest]
        public IEnumerator Events_ReachTheDelegateOfTheirPresentation()
        {
            var recorder = new RecordingDelegate();
            BridgeCallbackHandler.Instance.RegisterCustomerCenter("cc-1", recorder, null);

            Send("customerCenterDidSelectAction", new Dictionary<string, object>
            {
                { "presentationId", "cc-1" },
                { "action", new Dictionary<string, object> { { "type", "url" }, { "url", "https://example.com/faq" } } },
                { "pathId", "example.com/faq" },
                { "purchase", new Dictionary<string, object>
                    {
                        { "productId", "pro_monthly" },
                        { "store", "playStore" },
                        { "entitlements", new List<object> { new Dictionary<string, object> { { "id", "pro" }, { "isActive", true } } } },
                        { "subscription", new Dictionary<string, object> { { "transactionId", "GPA.1" }, { "productId", "pro_monthly" }, { "purchaseDate", 1761739200000L }, { "willRenew", true } } },
                    }
                },
            });
            Send("customerCenterDidCompleteSurvey", new Dictionary<string, object>
            {
                { "presentationId", "cc-1" },
                { "surveyId", "cancel_survey" },
                { "optionId", "too_expensive" },
                { "action", new Dictionary<string, object> { { "type", "manageSubscription" } } },
                { "pathId", "manage_subscription" },
            });
            Send("customerCenterDidCompleteRefundRequest", new Dictionary<string, object>
            {
                { "presentationId", "cc-1" }, { "productId", "pro_monthly" }, { "status", "userCancelled" },
            });
            // Another presentation's events don't reach this delegate.
            Send("customerCenterDidDismiss", new Dictionary<string, object> { { "presentationId", "cc-other" } });
            yield return null;

            Assert.AreEqual(CustomerCenterActionType.Url, recorder.Action.Type);
            Assert.AreEqual("https://example.com/faq", recorder.Action.Url);
            Assert.AreEqual("example.com/faq", recorder.PathId);
            Assert.AreEqual("pro_monthly", recorder.Purchase.ProductId);
            Assert.AreEqual(ProductStore.PlayStore, recorder.Purchase.Store);
            Assert.AreEqual("pro", recorder.Purchase.Entitlements[0].Id);
            Assert.AreEqual("GPA.1", recorder.Purchase.Subscription.TransactionId);
            Assert.IsTrue(recorder.Purchase.Subscription.WillRenew);
            Assert.IsNull(recorder.Purchase.NonSubscription);
            Assert.AreEqual("too_expensive", recorder.SurveyOptionId);
            Assert.AreEqual(CustomerCenterActionType.ManageSubscription, recorder.SurveyAction.Type);
            Assert.AreEqual(CustomerCenterRefundStatus.UserCancelled, recorder.RefundStatus);
            Assert.IsFalse(recorder.Dismissed);
        }

        [UnityTest]
        public IEnumerator ShouldRestorePurchases_RepliesWithTheDelegatesAnswer()
        {
            BridgeCallbackHandler.Instance.RegisterCustomerCenter("cc-2", new RecordingDelegate { AllowRestore = false }, null);
            LogAssert.Expect(LogType.Log, "[Superwall] RespondToCustomerCenterShouldRestore(callbackId=restore-1, proceed=False)");
            Send("customerCenterShouldRestorePurchases", new Dictionary<string, object> { { "presentationId", "cc-2" }, { "callbackId", "restore-1" } });
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShouldRestorePurchases_WithoutADelegate_Restores()
        {
            // Native holds the restore until C# replies, so a presentation C# knows nothing about must still get one.
            LogAssert.Expect(LogType.Log, "[Superwall] RespondToCustomerCenterShouldRestore(callbackId=restore-2, proceed=True)");
            Send("customerCenterShouldRestorePurchases", new Dictionary<string, object> { { "presentationId", "unknown" }, { "callbackId", "restore-2" } });
            yield return null;
        }

        [UnityTest]
        public IEnumerator OnDismiss_RunsOnceAndReleasesThePresentation()
        {
            var recorder = new RecordingDelegate();
            var dismissals = 0;
            BridgeCallbackHandler.Instance.RegisterCustomerCenter("cc-3", recorder, () => dismissals++);

            Send("customerCenterDidDismiss", new Dictionary<string, object> { { "presentationId", "cc-3" } });
            Send("customerCenterOnDismiss", new Dictionary<string, object> { { "presentationId", "cc-3" } });
            Send("customerCenterOnDismiss", new Dictionary<string, object> { { "presentationId", "cc-3" } });
            Send("customerCenterDidSelectAction", new Dictionary<string, object>
            {
                { "presentationId", "cc-3" }, { "action", new Dictionary<string, object> { { "type", "restore" } } }, { "pathId", "restore" },
            });
            yield return null;

            Assert.IsTrue(recorder.Dismissed);
            Assert.AreEqual(1, dismissals);
            Assert.IsNull(recorder.Action, "events after the presentation ended don't reach its delegate");
        }

        [Test]
        public void Present_SendsTheConfigurationAcrossTheBridge()
        {
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[Superwall\] PresentCustomerCenter\(\{""managementScreen"""));
            var superwall = (global::Superwall.Superwall)System.Activator.CreateInstance(typeof(global::Superwall.Superwall), nonPublic: true);
            superwall.PresentCustomerCenter(CustomerCenterConfiguration.Default);
        }

        private class RecordingDelegate : ICustomerCenterDelegate
        {
            public bool AllowRestore = true;
            public CustomerCenterAction Action;
            public string PathId;
            public CustomerCenterPurchase Purchase;
            public string SurveyOptionId;
            public CustomerCenterAction SurveyAction;
            public CustomerCenterRefundStatus? RefundStatus;
            public bool Dismissed;

            public void ShouldRestorePurchases(System.Action<bool> proceed) => proceed(AllowRestore);

            public void DidSelectAction(CustomerCenterAction action, string pathId, CustomerCenterPurchase purchase)
            {
                Action = action;
                PathId = pathId;
                Purchase = purchase;
            }

            public void DidCompleteSurvey(string surveyId, string optionId, CustomerCenterAction action, string pathId)
            {
                SurveyOptionId = optionId;
                SurveyAction = action;
            }

            public void DidCompleteRefundRequest(string productId, CustomerCenterRefundStatus status) => RefundStatus = status;

            public void DidDismiss() => Dismissed = true;
        }
    }
}
