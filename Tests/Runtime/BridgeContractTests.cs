using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Superwall;
using Superwall.Internal;

namespace Superwall.Tests
{
    /// <summary>
    /// Verifies the JSON shape contracts the native bridges must adhere to. Each test feeds a
    /// payload mirroring what the iOS/Android bridges actually emit and asserts the C# side
    /// surfaces it correctly.
    ///
    /// Add a regression test here whenever a shape divergence between native and C# is found.
    /// </summary>
    class BridgeContractTests
    {
        // --- AsyncResponse contract ---

        [UnityTest]
        public IEnumerator AsyncResponse_PayloadReachesCallback_WithCallbackIdStripped()
        {
            // Regression: HandleAsyncResponse used to look for data["response"], a key no native
            // bridge emits. The result: every async callback received null, breaking Configure
            // (always returned ConfigurationResult.Failed("Unknown error")), GetProducts,
            // GetCustomerInfo, GetAssignments, etc.
            BridgeCallbackHandler.Initialize();
            yield return null;

            string captured = null;
            const string id = "test-async-shape";
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(id, (json) => captured = json);

            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", "asyncResponse" },
                { "data", new Dictionary<string, object>
                    {
                        { "callbackId", id },
                        { "success", true },
                        { "error", "" }
                    }
                }
            }));
            yield return null;

            Assert.IsNotNull(captured, "Callback must receive the response payload as JSON");
            var parsed = Json.Deserialize(captured) as Dictionary<string, object>;
            Assert.IsNotNull(parsed);
            Assert.IsTrue((bool)parsed["success"], "success field must survive the round trip");
            Assert.IsFalse(parsed.ContainsKey("callbackId"),
                "callbackId must be stripped before reaching the user callback");
        }

        [UnityTest]
        public IEnumerator AsyncResponse_UnknownCallbackId_DoesNotInvokeAnyCallback()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;

            bool fired = false;
            BridgeCallbackHandler.Instance.RegisterAsyncCallback("registered-id",
                (_) => fired = true);

            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", "asyncResponse" },
                { "data", new Dictionary<string, object>
                    {
                        { "callbackId", "some-other-id" },
                        { "success", true }
                    }
                }
            }));
            yield return null;

            Assert.IsFalse(fired,
                "Registered callback must not fire for a different callbackId");
        }

        [UnityTest]
        public IEnumerator AsyncResponse_CallbackIsRemovedAfterInvocation()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;

            int invocations = 0;
            const string id = "single-use-id";
            BridgeCallbackHandler.Instance.RegisterAsyncCallback(id, (_) => invocations++);

            string payload = Json.Serialize(new Dictionary<string, object>
            {
                { "method", "asyncResponse" },
                { "data", new Dictionary<string, object>
                    {
                        { "callbackId", id },
                        { "success", true }
                    }
                }
            });

            BridgeCallbackHandler.Instance.OnCallback(payload);
            BridgeCallbackHandler.Instance.OnCallback(payload);
            yield return null;

            Assert.AreEqual(1, invocations,
                "Async callbacks are single-use; a repeated dispatch must not fire them again");
        }

        // --- Configure success/failure paths ---

        [UnityTest]
        public IEnumerator ConfigureCallback_NativeSuccess_PropagatesSuccess()
        {
            // Mirrors the inline closure registered in Superwall.Configure.
            BridgeCallbackHandler.Initialize();
            yield return null;

            ConfigurationResult result = null;
            const string id = "configure-success";

            BridgeCallbackHandler.Instance.RegisterAsyncCallback(id, (json) =>
            {
                var data = Json.Deserialize(json) as Dictionary<string, object>;
                if (data != null && data.ContainsKey("success") && (bool)data["success"])
                {
                    result = ConfigurationResult.Success();
                }
                else
                {
                    string error = data != null && data.ContainsKey("error")
                        ? data["error"] as string : "Unknown error";
                    result = ConfigurationResult.Failed(error);
                }
            });

            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", "asyncResponse" },
                { "data", new Dictionary<string, object>
                    {
                        { "callbackId", id },
                        { "success", true }
                    }
                }
            }));
            yield return null;

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
        }

        [UnityTest]
        public IEnumerator ConfigureCallback_NativeFailure_PropagatesErrorMessage()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;

            ConfigurationResult result = null;
            const string id = "configure-failure";

            BridgeCallbackHandler.Instance.RegisterAsyncCallback(id, (json) =>
            {
                var data = Json.Deserialize(json) as Dictionary<string, object>;
                if (data != null && data.ContainsKey("success") && (bool)data["success"])
                {
                    result = ConfigurationResult.Success();
                }
                else
                {
                    string error = data != null && data.ContainsKey("error")
                        ? data["error"] as string : "Unknown error";
                    result = ConfigurationResult.Failed(error);
                }
            });

            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", "asyncResponse" },
                { "data", new Dictionary<string, object>
                    {
                        { "callbackId", id },
                        { "success", false },
                        { "error", "bad api key" }
                    }
                }
            }));
            yield return null;

            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsSuccess);
            var failed = result as ConfigurationResult.FailedResult;
            Assert.IsNotNull(failed);
            Assert.AreEqual("bad api key", failed.Error);
        }

        // --- Delegate payload shape contracts ---

        [UnityTest]
        public IEnumerator Delegate_UserAttributesDidChange_ReadsNewAttributesKey()
        {
            // Regression: Android previously sent the key "attributes" while the C# handler
            // reads "newAttributes" — Unity delegate received null on Android.
            BridgeCallbackHandler.Initialize();
            yield return null;

            Dictionary<string, object> captured = null;
            var mockDelegate = new MockSuperwallDelegateContract();
            mockDelegate.OnUserAttributesDidChange = (attrs) => captured = attrs;
            BridgeCallbackHandler.Instance.Delegate = mockDelegate;

            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", "userAttributesDidChange" },
                { "data", new Dictionary<string, object>
                    {
                        { "newAttributes", new Dictionary<string, object>
                            {
                                { "tier", "gold" },
                                { "count", 7 }
                            }
                        }
                    }
                }
            }));
            yield return null;

            Assert.IsNotNull(captured);
            Assert.AreEqual("gold", captured["tier"]);
        }

        [UnityTest]
        public IEnumerator Delegate_DidRedeemLink_DeserializesSuccessShape()
        {
            // Regression: Android used to wrap the result as {result: {status: ...}} which the
            // C# DeserializeRedemptionResult (looking for top-level "type" and "code") couldn't
            // parse. The shape is now flat across iOS and Android.
            BridgeCallbackHandler.Initialize();
            yield return null;

            RedemptionResult captured = null;
            var mockDelegate = new MockSuperwallDelegateContract();
            mockDelegate.OnDidRedeemLink = (r) => captured = r;
            BridgeCallbackHandler.Instance.Delegate = mockDelegate;

            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", "didRedeemLink" },
                { "data", new Dictionary<string, object>
                    {
                        { "type", "success" },
                        { "code", "PROMO123" },
                        { "redemptionInfo", new Dictionary<string, object>
                            {
                                { "entitlements", new List<object>() }
                            }
                        }
                    }
                }
            }));
            yield return null;

            Assert.IsNotNull(captured);
            Assert.AreEqual(RedemptionResult.ResultType.Success, captured.Type);
            var success = captured as RedemptionResult.SuccessResult;
            Assert.IsNotNull(success);
            Assert.AreEqual("PROMO123", success.Code);
        }

        [UnityTest]
        public IEnumerator Delegate_DidRedeemLink_DeserializesInvalidCode()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;

            RedemptionResult captured = null;
            var mockDelegate = new MockSuperwallDelegateContract();
            mockDelegate.OnDidRedeemLink = (r) => captured = r;
            BridgeCallbackHandler.Instance.Delegate = mockDelegate;

            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", "didRedeemLink" },
                { "data", new Dictionary<string, object>
                    {
                        { "type", "invalidCode" },
                        { "code", "WRONG" }
                    }
                }
            }));
            yield return null;

            Assert.IsNotNull(captured);
            Assert.AreEqual(RedemptionResult.ResultType.InvalidCode, captured.Type);
            Assert.AreEqual("WRONG", ((RedemptionResult.InvalidCodeResult)captured).Code);
        }

        [UnityTest]
        public IEnumerator Delegate_CustomerInfoDidChange_DeserializesEntitlements()
        {
            // Regression: Android previously sent only {userId} on both sides, dropping
            // entitlements. The shape now matches iOS.
            BridgeCallbackHandler.Initialize();
            yield return null;

            CustomerInfo capturedTo = null;
            var mockDelegate = new MockSuperwallDelegateContract();
            mockDelegate.OnCustomerInfoDidChange = (from, to) => capturedTo = to;
            BridgeCallbackHandler.Instance.Delegate = mockDelegate;

            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", "customerInfoDidChange" },
                { "data", new Dictionary<string, object>
                    {
                        { "from", new Dictionary<string, object>
                            {
                                { "userId", "u1" },
                                { "entitlements", new List<object>() }
                            }
                        },
                        { "to", new Dictionary<string, object>
                            {
                                { "userId", "u1" },
                                { "entitlements", new List<object>
                                    {
                                        new Dictionary<string, object>
                                        {
                                            { "id", "premium" },
                                            { "isActive", true }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }));
            yield return null;

            Assert.IsNotNull(capturedTo);
            Assert.AreEqual("u1", capturedTo.UserId);
            Assert.AreEqual(1, capturedTo.Entitlements.Count);
            Assert.AreEqual("premium", capturedTo.Entitlements[0].Id);
        }

        // --- PresentationHandler payload shape contracts ---

        [UnityTest]
        public IEnumerator PresentationHandler_OnDismiss_ReceivesPaywallResult()
        {
            // Regression: Android previously dropped the PaywallResult argument from onDismiss,
            // so Unity could not distinguish purchased/declined/restored on Android.
            BridgeCallbackHandler.Initialize();
            yield return null;

            PaywallResult captured = null;
            const string handlerId = "h1";
            BridgeCallbackHandler.Instance.RegisterPresentationHandler(handlerId,
                new PaywallPresentationHandler
                {
                    OnDismiss = (info, result) => captured = result
                });

            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", "onDismiss" },
                { "data", new Dictionary<string, object>
                    {
                        { "handlerId", handlerId },
                        { "paywallInfo", new Dictionary<string, object> { { "identifier", "pw" } } },
                        { "result", new Dictionary<string, object>
                            {
                                { "type", "purchased" },
                                { "productId", "com.app.monthly" }
                            }
                        }
                    }
                }
            }));
            yield return null;

            Assert.IsNotNull(captured);
            Assert.AreEqual(PaywallResult.ResultType.Purchased, captured.Type);
            Assert.AreEqual("com.app.monthly",
                ((PaywallResult.PurchasedResult)captured).ProductId);
        }

        [UnityTest]
        public IEnumerator PresentationHandler_OnSkip_AcceptsLowercaseReasonStrings()
        {
            // Regression: Android used to send PascalCase (`reason::class.simpleName`); both
            // platforms now emit camelCase. The deserializer matches case-insensitively, but
            // pin the contract.
            BridgeCallbackHandler.Initialize();
            yield return null;

            PaywallSkippedReason captured = PaywallSkippedReason.NoAudienceMatch;
            const string handlerId = "h-skip";
            BridgeCallbackHandler.Instance.RegisterPresentationHandler(handlerId,
                new PaywallPresentationHandler
                {
                    OnSkip = (reason) => captured = reason
                });

            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", "onSkip" },
                { "data", new Dictionary<string, object>
                    {
                        { "handlerId", handlerId },
                        { "reason", "holdout" }
                    }
                }
            }));
            yield return null;

            Assert.AreEqual(PaywallSkippedReason.Holdout, captured);
        }

        [UnityTest]
        public IEnumerator PresentationHandler_OnPresent_DeserializesFullPaywallInfo()
        {
            // Regression: Android previously emitted only {identifier}; now both platforms emit
            // the full PaywallInfo including products, presentedBy*, load times,
            // featureGatingBehavior, closeReason, etc.
            BridgeCallbackHandler.Initialize();
            yield return null;

            PaywallInfo captured = null;
            const string handlerId = "h-present";
            BridgeCallbackHandler.Instance.RegisterPresentationHandler(handlerId,
                new PaywallPresentationHandler
                {
                    OnPresent = (info) => captured = info
                });

            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", "onPresent" },
                { "data", new Dictionary<string, object>
                    {
                        { "handlerId", handlerId },
                        { "paywallInfo", new Dictionary<string, object>
                            {
                                { "identifier", "pw_abc" },
                                { "name", "Welcome" },
                                { "url", "https://example.com/p/pw_abc" },
                                { "productIds", new List<object> { "monthly", "yearly" } },
                                { "presentedBy", "register" },
                                { "presentedByPlacementWithName", "campaign_open" },
                                { "isFreeTrialAvailable", true },
                                { "featureGatingBehavior", "gated" },
                                { "closeReason", "manualClose" },
                                { "responseLoadDuration", 0.123 }
                            }
                        }
                    }
                }
            }));
            yield return null;

            Assert.IsNotNull(captured);
            Assert.AreEqual("pw_abc", captured.Identifier);
            Assert.AreEqual("Welcome", captured.Name);
            Assert.AreEqual(2, captured.ProductIds.Count);
            Assert.AreEqual("register", captured.PresentedBy);
            Assert.AreEqual("campaign_open", captured.PresentedByPlacementWithName);
            Assert.IsTrue(captured.IsFreeTrialAvailable.Value);
            Assert.AreEqual(FeatureGatingBehavior.Gated, captured.FeatureGatingBehavior);
            Assert.AreEqual(PaywallCloseReason.ManualClose, captured.CloseReason);
            Assert.AreEqual(0.123, captured.ResponseLoadDuration.Value, 0.0001);
        }
        // --- Purchase controller contract ---

        private static string PurchaseRequest(string method, string callbackId) =>
            Json.Serialize(new Dictionary<string, object>
            {
                { "method", method },
                { "data", new Dictionary<string, object>
                    {
                        { "productId", "com.app.monthly" },
                        { "basePlanId", "" },
                        { "offerId", "" },
                        { "callbackId", callbackId }
                    }
                }
            });

        private static string ReplacementPurchaseRequest(string callbackId) =>
            Json.Serialize(new Dictionary<string, object>
            {
                { "method", "purchaseFromGooglePlay" },
                { "data", new Dictionary<string, object>
                    {
                        { "productId", "com.app.annual" },
                        { "basePlanId", "annual" },
                        { "offerId", "" },
                        { "callbackId", callbackId },
                        { "replacement", new Dictionary<string, object>
                            {
                                { "mode", "charge_prorated" },
                                { "oldProductId", "com.app.monthly" },
                                { "oldPurchaseToken", "token-123" }
                            }
                        }
                    }
                }
            });

        [UnityTest]
        public IEnumerator PurchaseController_ReplacementOverload_ReceivesReplacement()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;
            var controller = new MockReplacementPurchaseController();
            BridgeCallbackHandler.Instance.PurchaseController = controller;

            BridgeCallbackHandler.Instance.OnCallback(ReplacementPurchaseRequest("cb-replace"));

            Assert.AreEqual("com.app.annual", controller.ProductId);
            Assert.AreEqual("annual", controller.BasePlanId);
            Assert.IsNotNull(controller.Replacement);
            Assert.AreEqual(ProductReplacement.ReplacementMode.ChargeProrated, controller.Replacement.Mode);
            Assert.AreEqual("com.app.monthly", controller.Replacement.OldProductId);
            Assert.AreEqual("token-123", controller.Replacement.OldPurchaseToken);

            // A normal purchase carries no replacement.
            BridgeCallbackHandler.Instance.OnCallback(PurchaseRequest("purchaseFromGooglePlay", "cb-plain"));
            Assert.AreEqual("com.app.monthly", controller.ProductId);
            Assert.IsNull(controller.Replacement);

            controller.PendingPurchase(PurchaseResult.Purchased());
            yield return null;
            BridgeCallbackHandler.Instance.PurchaseController = null;
        }

        private static string CustomProductPurchaseRequest(string callbackId, bool withReplacement)
        {
            var data = new Dictionary<string, object>
            {
                { "productId", "web_annual" },
                { "customTransactionId", "txn-abc" },
                { "callbackId", callbackId }
            };
            if (withReplacement)
            {
                // Android only. Custom subscriptions have no Play purchase token.
                data["replacement"] = new Dictionary<string, object>
                {
                    { "mode", "default" },
                    { "oldProductId", "web_monthly" },
                    { "oldPurchaseToken", null }
                };
            }
            return Json.Serialize(new Dictionary<string, object>
            {
                { "method", "purchaseCustomProduct" },
                { "data", data }
            });
        }

        [UnityTest]
        public IEnumerator PurchaseController_CustomProduct_ReachesPurchaseCustomProduct()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;
            var controller = new MockCustomProductPurchaseController();
            BridgeCallbackHandler.Instance.PurchaseController = controller;

            BridgeCallbackHandler.Instance.OnCallback(CustomProductPurchaseRequest("cb-custom", withReplacement: false));

            Assert.IsNotNull(controller.Purchase, "Custom product purchase must reach PurchaseCustomProduct");
            Assert.AreEqual("web_annual", controller.Purchase.ProductId);
            Assert.AreEqual("txn-abc", controller.Purchase.CustomTransactionId);
            Assert.IsNull(controller.Purchase.Replacement);

            LogAssert.Expect(LogType.Log,
                new Regex(@"RespondToPurchaseController\(callbackId=cb-custom,.*""type"":""purchased"""));
            controller.PendingPurchase(PurchaseResult.Purchased());
            yield return null;

            BridgeCallbackHandler.Instance.PurchaseController = null;
        }

        [UnityTest]
        public IEnumerator PurchaseController_CustomProduct_ReceivesReplacement()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;
            var controller = new MockCustomProductPurchaseController();
            BridgeCallbackHandler.Instance.PurchaseController = controller;

            BridgeCallbackHandler.Instance.OnCallback(CustomProductPurchaseRequest("cb-custom-replace", withReplacement: true));

            Assert.IsNotNull(controller.Purchase.Replacement);
            Assert.AreEqual(ProductReplacement.ReplacementMode.Default, controller.Purchase.Replacement.Mode);
            Assert.AreEqual("web_monthly", controller.Purchase.Replacement.OldProductId);
            Assert.IsNull(controller.Purchase.Replacement.OldPurchaseToken);

            controller.PendingPurchase(PurchaseResult.Cancelled());
            yield return null;
            BridgeCallbackHandler.Instance.PurchaseController = null;
        }

        [UnityTest]
        public IEnumerator PurchaseController_WithoutCustomProductSupport_FailsCustomProduct()
        {
            // Controllers that don't implement PurchaseCustomProduct must answer, not hang the paywall,
            // and must not be sent the custom product as a store purchase.
            BridgeCallbackHandler.Initialize();
            yield return null;
            var controller = new MockPurchaseControllerContract();
            BridgeCallbackHandler.Instance.PurchaseController = controller;

            LogAssert.Expect(LogType.Log,
                new Regex(@"RespondToPurchaseController\(callbackId=cb-custom-legacy,.*""type"":""failed"".*PurchaseCustomProduct"));
            BridgeCallbackHandler.Instance.OnCallback(CustomProductPurchaseRequest("cb-custom-legacy", withReplacement: false));
            yield return null;

            Assert.IsNull(controller.PendingPurchase, "A custom product must not reach the store purchase methods");
            BridgeCallbackHandler.Instance.PurchaseController = null;
        }

        [UnityTest]
        public IEnumerator PurchaseController_CustomProductThatThrows_RepliesFailed()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;
            var controller = new MockCustomProductPurchaseController { Throw = true };
            BridgeCallbackHandler.Instance.PurchaseController = controller;

            LogAssert.Expect(LogType.Error, new Regex("PurchaseCustomProduct threw: boom"));
            LogAssert.Expect(LogType.Log,
                new Regex(@"RespondToPurchaseController\(callbackId=cb-custom-throw,.*""type"":""failed"""));
            BridgeCallbackHandler.Instance.OnCallback(CustomProductPurchaseRequest("cb-custom-throw", withReplacement: false));
            yield return null;

            BridgeCallbackHandler.Instance.PurchaseController = null;
        }

        [UnityTest]
        public IEnumerator PurchaseController_WithoutReplacementOverload_StillReceivesPurchase()
        {
            // Controllers written before 2.9.0 implement only the 4-argument method. The
            // replacement overload's default implementation must route to it.
            BridgeCallbackHandler.Initialize();
            yield return null;
            var controller = new MockPurchaseControllerContract();
            BridgeCallbackHandler.Instance.PurchaseController = controller;

            BridgeCallbackHandler.Instance.OnCallback(ReplacementPurchaseRequest("cb-legacy"));
            Assert.IsNotNull(controller.PendingPurchase, "Legacy controller must receive the request");

            LogAssert.Expect(LogType.Log,
                new Regex(@"RespondToPurchaseController\(callbackId=cb-legacy,.*""type"":""purchased"""));
            controller.PendingPurchase(PurchaseResult.Purchased());
            yield return null;

            BridgeCallbackHandler.Instance.PurchaseController = null;
        }

        [UnityTest]
        public IEnumerator PurchaseController_GooglePlayThrows_RepliesFailed()
        {
            // Regression: a throwing controller used to be logged without a reply, leaving the
            // native purchase controller awaiting forever (endless spinner on the paywall).
            BridgeCallbackHandler.Initialize();
            yield return null;
            BridgeCallbackHandler.Instance.PurchaseController = new MockPurchaseControllerContract
            {
                Throw = true
            };

            LogAssert.Expect(LogType.Error, new Regex("PurchaseFromGooglePlay threw"));
            LogAssert.Expect(LogType.Log,
                new Regex(@"RespondToPurchaseController\(callbackId=cb-throw,.*""type"":""failed"""));
            BridgeCallbackHandler.Instance.OnCallback(PurchaseRequest("purchaseFromGooglePlay", "cb-throw"));
            yield return null;

            BridgeCallbackHandler.Instance.PurchaseController = null;
        }

        [UnityTest]
        public IEnumerator PurchaseController_RestoreThrows_RepliesFailed()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;
            BridgeCallbackHandler.Instance.PurchaseController = new MockPurchaseControllerContract
            {
                Throw = true
            };

            LogAssert.Expect(LogType.Error, new Regex("RestorePurchases threw"));
            LogAssert.Expect(LogType.Log,
                new Regex(@"RespondToRestorePurchases\(callbackId=cb-restore-throw,.*""type"":""failed"""));
            BridgeCallbackHandler.Instance.OnCallback(PurchaseRequest("restorePurchases", "cb-restore-throw"));
            yield return null;

            BridgeCallbackHandler.Instance.PurchaseController = null;
        }

        [UnityTest]
        public IEnumerator PurchaseController_LateResultAfterCancellation_IsNotSentToNative()
        {
            // Native abandons a request when its awaiting coroutine is cancelled and tells C#
            // via purchaseCancelled. A completion arriving afterwards must not be reported.
            BridgeCallbackHandler.Initialize();
            yield return null;
            var controller = new MockPurchaseControllerContract();
            BridgeCallbackHandler.Instance.PurchaseController = controller;

            BridgeCallbackHandler.Instance.OnCallback(PurchaseRequest("purchaseFromGooglePlay", "cb-late"));
            Assert.IsNotNull(controller.PendingPurchase, "Controller must receive the request");

            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", "purchaseCancelled" },
                { "data", new Dictionary<string, object> { { "callbackId", "cb-late" } } }
            }));

            var replies = new List<string>();
            Application.LogCallback capture = (message, _, __) =>
            {
                if (message.Contains("callbackId=cb-late")) replies.Add(message);
            };
            Application.logMessageReceived += capture;
            LogAssert.Expect(LogType.Warning, new Regex("Ignoring late purchase result.*cb-late"));
            controller.PendingPurchase(PurchaseResult.Purchased());
            Application.logMessageReceived -= capture;
            yield return null;

            Assert.IsEmpty(replies, "A cancelled request's result must not be sent to native");
            BridgeCallbackHandler.Instance.PurchaseController = null;
        }

        [UnityTest]
        public IEnumerator PurchaseController_RequestAfterCancellation_IsNotStarted()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;
            var controller = new MockPurchaseControllerContract();
            BridgeCallbackHandler.Instance.PurchaseController = controller;

            BridgeCallbackHandler.Instance.OnCallback(Json.Serialize(new Dictionary<string, object>
            {
                { "method", "restoreCancelled" },
                { "data", new Dictionary<string, object> { { "callbackId", "cb-stale" } } }
            }));
            LogAssert.Expect(LogType.Warning, new Regex("Skipping purchase controller request cb-stale"));
            BridgeCallbackHandler.Instance.OnCallback(PurchaseRequest("restorePurchases", "cb-stale"));
            yield return null;

            Assert.IsNull(controller.PendingRestore, "A cancelled request must not reach the controller");
            BridgeCallbackHandler.Instance.PurchaseController = null;
        }
    }

    /// <summary>
    /// Purchase controller that captures completions so tests can complete them later, or throws.
    /// </summary>
    class MockPurchaseControllerContract : IPurchaseController
    {
        public bool Throw;
        public System.Action<PurchaseResult> PendingPurchase;
        public System.Action<RestorationResult> PendingRestore;

        public void PurchaseFromAppStore(string productId, System.Action<PurchaseResult> completion)
        {
            if (Throw) throw new System.InvalidOperationException("boom");
            PendingPurchase = completion;
        }

        public void PurchaseFromGooglePlay(string productId, string basePlanId, string offerId, System.Action<PurchaseResult> completion)
        {
            if (Throw) throw new System.InvalidOperationException("boom");
            PendingPurchase = completion;
        }

        public void RestorePurchases(System.Action<RestorationResult> completion)
        {
            if (Throw) throw new System.InvalidOperationException("boom");
            PendingRestore = completion;
        }
    }

    /// <summary>
    /// Purchase controller that implements the replacement overload and records what it received.
    /// </summary>
    class MockReplacementPurchaseController : MockPurchaseControllerContract, IPurchaseController
    {
        public string ProductId;
        public string BasePlanId;
        public ProductReplacement Replacement;

        public void PurchaseFromGooglePlay(string productId, string basePlanId, string offerId,
            ProductReplacement replacement, System.Action<PurchaseResult> completion)
        {
            ProductId = productId;
            BasePlanId = basePlanId;
            Replacement = replacement;
            PendingPurchase = completion;
        }
    }

    /// <summary>
    /// Purchase controller that sells custom products and records what it received.
    /// </summary>
    class MockCustomProductPurchaseController : MockPurchaseControllerContract, IPurchaseController
    {
        public CustomProductPurchase Purchase;

        public void PurchaseCustomProduct(CustomProductPurchase purchase, System.Action<PurchaseResult> completion)
        {
            if (Throw) throw new System.InvalidOperationException("boom");
            Purchase = purchase;
            PendingPurchase = completion;
        }
    }

    /// <summary>
    /// Local mock that exposes the few hooks our contract tests need without bringing in the
    /// full surface MockSuperwallDelegate uses elsewhere.
    /// </summary>
    class MockSuperwallDelegateContract : ISuperwallDelegate
    {
        public System.Action<Dictionary<string, object>> OnUserAttributesDidChange;
        public System.Action<RedemptionResult> OnDidRedeemLink;
        public System.Action<CustomerInfo, CustomerInfo> OnCustomerInfoDidChange;

        public void SubscriptionStatusDidChange(SubscriptionStatus from, SubscriptionStatus to) { }
        public void HandleSuperwallEvent(SuperwallEventInfo eventInfo) { }
        public void HandleCustomPaywallAction(string name) { }
        public void WillDismissPaywall(PaywallInfo paywallInfo) { }
        public void WillPresentPaywall(PaywallInfo paywallInfo) { }
        public void DidDismissPaywall(PaywallInfo paywallInfo) { }
        public void DidPresentPaywall(PaywallInfo paywallInfo) { }
        public void PaywallWillOpenURL(string url) { }
        public void PaywallWillOpenDeepLink(string url) { }
        public void HandleLog(string level, string scope, string message, Dictionary<string, object> info, string error) { }
        public void WillRedeemLink() { }
        public void DidRedeemLink(RedemptionResult result) => OnDidRedeemLink?.Invoke(result);
        public void HandleSuperwallDeepLink(string fullURL, List<string> pathComponents, Dictionary<string, string> queryParameters) { }
        public void CustomerInfoDidChange(CustomerInfo from, CustomerInfo to) => OnCustomerInfoDidChange?.Invoke(from, to);
        public void UserAttributesDidChange(Dictionary<string, object> newAttributes) => OnUserAttributesDidChange?.Invoke(newAttributes);
    }
}
