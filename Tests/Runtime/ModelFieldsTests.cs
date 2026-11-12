using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Superwall.Internal;
using UnityEngine.TestTools;

namespace Superwall.Tests
{
    /// <summary>
    /// The model fields and event values added for parity with the native SDKs, fed in the shape both bridges send
    /// (see ci~/android-harness/bridge-tests/ModelFieldsTest.kt for the Android half).
    /// </summary>
    class ModelFieldsTests
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

        private static Dictionary<string, object> Event(string eventType, Dictionary<string, object> extra)
        {
            var data = new Dictionary<string, object> { { "eventType", eventType }, { "params", new Dictionary<string, object>() } };
            foreach (var pair in extra) data[pair.Key] = pair.Value;
            return data;
        }

        [UnityTest]
        public IEnumerator TransactionComplete_CarriesTheNewTransactionProductAndPaywallFields()
        {
            Send("handleSuperwallEvent", Event("transaction_complete", new Dictionary<string, object>
            {
                { "transactionType", "freeTrialStart" },
                { "transaction", new Dictionary<string, object>
                    {
                        { "state", "purchased" },
                        { "payment", new Dictionary<string, object> { { "productIdentifier", "pro" }, { "quantity", 1 }, { "discountIdentifier", "intro" } } },
                        { "appAccountToken", "6F9619FF-8B86-D011-B42D-00C04FC964FF" },
                        { "purchaseToken", "token-1" },
                        { "signature", "sig" },
                    }
                },
                { "product", new Dictionary<string, object>
                    {
                        { "productIdentifier", "pro" },
                        { "fullIdentifier", "pro:monthly:trial" },
                        { "productType", "subs" },
                        { "storeFrontCountryCode", "US" },
                        { "isCustomProduct", true },
                        { "customTransactionId", "ctx-1" },
                    }
                },
                { "paywallInfo", new Dictionary<string, object>
                    {
                        { "identifier", "pw" },
                        { "cacheKey", "cache-1" },
                        { "buildId", "build-1" },
                        { "shimmerLoadStartTime", "2026-01-01T00:00:00Z" },
                        { "shimmerLoadCompleteTime", "2026-01-01T00:00:01Z" },
                        { "isScrollEnabled", true },
                        { "presentationId", "presentation-1" },
                        { "isLocal", false },
                        { "products", new List<object> { new Dictionary<string, object> { { "id", "pro" }, { "store", "custom" } } } },
                    }
                },
            }));
            yield return null;

            var e = _delegate.LastEvent;
            Assert.AreEqual(TransactionType.FreeTrialStart, e.TransactionType);
            Assert.AreEqual(StoreTransactionState.Purchased, e.Transaction.State);
            Assert.AreEqual("pro", e.Transaction.Payment.ProductIdentifier);
            Assert.AreEqual(1, e.Transaction.Payment.Quantity);
            Assert.AreEqual("intro", e.Transaction.Payment.DiscountIdentifier);
            Assert.AreEqual("6F9619FF-8B86-D011-B42D-00C04FC964FF", e.Transaction.AppAccountToken);
            Assert.AreEqual("token-1", e.Transaction.PurchaseToken);
            Assert.AreEqual("sig", e.Transaction.Signature);
            Assert.AreEqual("pro:monthly:trial", e.Product.FullIdentifier);
            Assert.AreEqual("subs", e.Product.ProductType);
            Assert.AreEqual("US", e.Product.StoreFrontCountryCode);
            Assert.IsTrue(e.Product.IsCustomProduct);
            Assert.AreEqual("ctx-1", e.Product.CustomTransactionId);
            Assert.AreEqual("cache-1", e.PaywallInfo.CacheKey);
            Assert.AreEqual("build-1", e.PaywallInfo.BuildId);
            Assert.AreEqual("2026-01-01T00:00:00Z", e.PaywallInfo.ShimmerLoadStartTime);
            Assert.AreEqual("2026-01-01T00:00:01Z", e.PaywallInfo.ShimmerLoadCompleteTime);
            Assert.AreEqual(true, e.PaywallInfo.IsScrollEnabled);
            Assert.AreEqual("presentation-1", e.PaywallInfo.PresentationId);
            Assert.AreEqual(false, e.PaywallInfo.IsLocal);
            Assert.AreEqual(ProductStore.Custom, e.PaywallInfo.Products[0].Store);
        }

        [UnityTest]
        public IEnumerator UppercaseTransactionState_IsParsed()
        {
            // SuperwallKit's raw values are upper case; the bridge lowercases them, but either must parse.
            Send("handleSuperwallEvent", Event("transaction_complete", new Dictionary<string, object>
            {
                { "transaction", new Dictionary<string, object> { { "state", "DEFERRED" } } },
            }));
            yield return null;
            Assert.AreEqual(StoreTransactionState.Deferred, _delegate.LastEvent.Transaction.State);
        }

        [UnityTest]
        public IEnumerator PermissionPreloadAndCustomerCenterEvents_CarryTheirValues()
        {
            Send("handleSuperwallEvent", Event("permission_granted", new Dictionary<string, object>
            {
                { "permissionName", "notification" }, { "paywallIdentifier", "pw" },
            }));
            yield return null;
            Assert.AreEqual(EventType.PermissionGranted, _delegate.LastEvent.EventType);
            Assert.AreEqual("notification", _delegate.LastEvent.PermissionName);
            Assert.AreEqual("pw", _delegate.LastEvent.PaywallIdentifier);

            Send("handleSuperwallEvent", Event("paywallPreload_start", new Dictionary<string, object> { { "paywallCount", 4 } }));
            yield return null;
            Assert.AreEqual(4, _delegate.LastEvent.PaywallCount);

            Send("handleSuperwallEvent", Event("customerCenter_open", new Dictionary<string, object>
            {
                { "customerCenterScreen", "noPurchases" }, { "customerCenterPresentationStyle", "sheet" },
            }));
            yield return null;
            Assert.AreEqual(CustomerCenterScreenType.NoPurchases, _delegate.LastEvent.CustomerCenterScreen);
            Assert.AreEqual(CustomerCenterPresentationStyle.Sheet, _delegate.LastEvent.CustomerCenterPresentationStyle);

            Send("handleSuperwallEvent", Event("customerCenter_surveyResponse", new Dictionary<string, object>
            {
                { "customerCenterSurveyId", "cancel_survey" },
                { "customerCenterSurveyOptionId", "dont_use" },
                { "customerCenterAction", new Dictionary<string, object> { { "type", "custom" }, { "identifier", "sign_out" } } },
                { "customerCenterPathId", "logout" },
                { "customerCenterProductId", "pro" },
            }));
            yield return null;
            var survey = _delegate.LastEvent;
            Assert.AreEqual("cancel_survey", survey.CustomerCenterSurveyId);
            Assert.AreEqual("dont_use", survey.CustomerCenterSurveyOptionId);
            Assert.AreEqual(CustomerCenterActionType.Custom, survey.CustomerCenterAction.Type);
            Assert.AreEqual("sign_out", survey.CustomerCenterAction.Identifier);
            Assert.AreEqual("logout", survey.CustomerCenterPathId);
            Assert.AreEqual("pro", survey.CustomerCenterProductId);

            Send("handleSuperwallEvent", Event("customerCenter_refundRequest", new Dictionary<string, object>
            {
                { "customerCenterProductId", "pro" }, { "customerCenterRefundStatus", "error" },
            }));
            yield return null;
            Assert.AreEqual(CustomerCenterRefundStatus.Error, _delegate.LastEvent.CustomerCenterRefundStatus);
        }

        [UnityTest]
        public IEnumerator CustomerInfo_CarriesActiveSubscriptionsAndEntitlementRenewal()
        {
            Send("customerInfoDidChange", new Dictionary<string, object>
            {
                { "from", new Dictionary<string, object> { { "userId", "u" } } },
                { "to", new Dictionary<string, object>
                    {
                        { "userId", "u" },
                        { "activeSubscriptionProductIds", new List<object> { "pro_monthly" } },
                        { "entitlements", new List<object>
                            {
                                new Dictionary<string, object>
                                {
                                    { "id", "pro" }, { "isActive", true }, { "store", "custom" },
                                    { "isRevoked", false }, { "renewsAt", 1793275200000L }, { "isInGracePeriod", true },
                                },
                            }
                        },
                    }
                },
            });
            yield return null;

            var to = _delegate.CustomerInfoTo;
            CollectionAssert.AreEqual(new[] { "pro_monthly" }, to.ActiveSubscriptionProductIds);
            var entitlement = to.Entitlements[0];
            Assert.AreEqual(ProductStore.Custom, entitlement.Store);
            Assert.AreEqual(false, entitlement.IsRevoked);
            Assert.AreEqual(1793275200000L, entitlement.RenewsAt);
            Assert.AreEqual(true, entitlement.IsInGracePeriod);
        }

        [UnityTest]
        public IEnumerator Redemption_CarriesUserAttributesAndTheProductIdentifier()
        {
            Send("didRedeemLink", new Dictionary<string, object>
            {
                { "type", "success" },
                { "code", "CODE" },
                { "redemptionInfo", new Dictionary<string, object>
                    {
                        { "ownership", new Dictionary<string, object> { { "type", "device" }, { "deviceId", "d" } } },
                        { "paywallInfo", new Dictionary<string, object> { { "identifier", "pw" }, { "productIdentifier", "pro_yearly" } } },
                        { "userAttributes", new Dictionary<string, object> { { "goal", "fitness" } } },
                        { "entitlements", new List<object>() },
                    }
                },
            });
            yield return null;

            var info = ((RedemptionResult.SuccessResult)_delegate.LastRedemption).RedemptionInfo;
            Assert.AreEqual("pro_yearly", info.PaywallInfo.ProductIdentifier);
            Assert.AreEqual("fitness", info.UserAttributes["goal"]);
        }
    }
}
