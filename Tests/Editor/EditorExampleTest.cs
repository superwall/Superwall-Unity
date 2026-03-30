using NUnit.Framework;
using System.Collections.Generic;
using Superwall;
using Superwall.Internal;

namespace Superwall.Editor.Tests
{
    class MiniJsonTests
    {
        [Test]
        public void Deserialize_SimpleObject_ReturnsDictionary()
        {
            var result = Json.Deserialize("{\"key\":\"value\"}") as Dictionary<string, object>;

            Assert.IsNotNull(result);
            Assert.AreEqual("value", result["key"]);
        }

        [Test]
        public void Deserialize_NestedObject_ReturnsNestedDictionary()
        {
            var json = "{\"outer\":{\"inner\":\"deep\"}}";
            var result = Json.Deserialize(json) as Dictionary<string, object>;

            Assert.IsNotNull(result);
            var outer = result["outer"] as Dictionary<string, object>;
            Assert.IsNotNull(outer);
            Assert.AreEqual("deep", outer["inner"]);
        }

        [Test]
        public void Deserialize_Array_ReturnsList()
        {
            var result = Json.Deserialize("[1,2,3]") as List<object>;

            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(1, result[0]);
            Assert.AreEqual(2, result[1]);
            Assert.AreEqual(3, result[2]);
        }

        [Test]
        public void Deserialize_MixedTypes_ParsesCorrectly()
        {
            var json = "{\"str\":\"hello\",\"num\":42,\"float\":3.14,\"bool\":true,\"nil\":null}";
            var result = Json.Deserialize(json) as Dictionary<string, object>;

            Assert.IsNotNull(result);
            Assert.AreEqual("hello", result["str"]);
            Assert.AreEqual(42, result["num"]);
            Assert.AreEqual(3.14, (double)result["float"], 0.001);
            Assert.AreEqual(true, result["bool"]);
            Assert.IsNull(result["nil"]);
        }

        [Test]
        public void Deserialize_EscapedStrings_HandlesCorrectly()
        {
            var json = "{\"text\":\"hello\\nworld\\t!\"}";
            var result = Json.Deserialize(json) as Dictionary<string, object>;

            Assert.IsNotNull(result);
            Assert.AreEqual("hello\nworld\t!", result["text"]);
        }

        [Test]
        public void Deserialize_UnicodeEscape_HandlesCorrectly()
        {
            var json = "{\"emoji\":\"\\u0041\"}";
            var result = Json.Deserialize(json) as Dictionary<string, object>;

            Assert.IsNotNull(result);
            Assert.AreEqual("A", result["emoji"]);
        }

        [Test]
        public void Deserialize_EmptyObject_ReturnsEmptyDictionary()
        {
            var result = Json.Deserialize("{}") as Dictionary<string, object>;

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Deserialize_EmptyArray_ReturnsEmptyList()
        {
            var result = Json.Deserialize("[]") as List<object>;

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Deserialize_NegativeNumber_ParsesCorrectly()
        {
            var json = "{\"value\":-99}";
            var result = Json.Deserialize(json) as Dictionary<string, object>;

            Assert.IsNotNull(result);
            Assert.AreEqual(-99, result["value"]);
        }

        [Test]
        public void Deserialize_LargeNumber_ReturnsLong()
        {
            var json = "{\"big\":9999999999}";
            var result = Json.Deserialize(json) as Dictionary<string, object>;

            Assert.IsNotNull(result);
            Assert.IsInstanceOf<long>(result["big"]);
            Assert.AreEqual(9999999999L, result["big"]);
        }

        [Test]
        public void Deserialize_Null_ReturnsNull()
        {
            Assert.IsNull(Json.Deserialize(null));
        }

        [Test]
        public void Serialize_Dictionary_ProducesValidJson()
        {
            var dict = new Dictionary<string, object>
            {
                { "name", "test" },
                { "count", 5 }
            };

            var json = Json.Serialize(dict);
            var parsed = Json.Deserialize(json) as Dictionary<string, object>;

            Assert.IsNotNull(parsed);
            Assert.AreEqual("test", parsed["name"]);
            Assert.AreEqual(5, parsed["count"]);
        }

        [Test]
        public void Serialize_List_ProducesValidJson()
        {
            var list = new List<object> { "a", "b", "c" };

            var json = Json.Serialize(list);
            var parsed = Json.Deserialize(json) as List<object>;

            Assert.IsNotNull(parsed);
            Assert.AreEqual(3, parsed.Count);
            Assert.AreEqual("a", parsed[0]);
        }

        [Test]
        public void Serialize_Null_ProducesNullString()
        {
            Assert.AreEqual("null", Json.Serialize(null));
        }

        [Test]
        public void Serialize_Bool_ProducesLowercase()
        {
            Assert.AreEqual("true", Json.Serialize(true));
            Assert.AreEqual("false", Json.Serialize(false));
        }

        [Test]
        public void Serialize_StringWithSpecialChars_EscapesCorrectly()
        {
            var json = Json.Serialize("hello\nworld");
            Assert.AreEqual("\"hello\\nworld\"", json);
        }

        [Test]
        public void Serialize_NestedStructure_Roundtrips()
        {
            var original = new Dictionary<string, object>
            {
                { "items", new List<object> { 1, 2, 3 } },
                { "nested", new Dictionary<string, object>
                    {
                        { "flag", true },
                        { "value", null }
                    }
                }
            };

            var json = Json.Serialize(original);
            var parsed = Json.Deserialize(json) as Dictionary<string, object>;

            Assert.IsNotNull(parsed);
            var items = parsed["items"] as List<object>;
            Assert.AreEqual(3, items.Count);

            var nested = parsed["nested"] as Dictionary<string, object>;
            Assert.AreEqual(true, nested["flag"]);
            Assert.IsNull(nested["value"]);
        }

        [Test]
        public void Deserialize_CallbackPayload_ParsesMethodAndData()
        {
            var json = "{\"method\":\"handleSuperwallEvent\",\"data\":{\"eventType\":\"paywallOpen\",\"placementName\":\"campaign_1\"}}";
            var result = Json.Deserialize(json) as Dictionary<string, object>;

            Assert.IsNotNull(result);
            Assert.AreEqual("handleSuperwallEvent", result["method"]);

            var data = result["data"] as Dictionary<string, object>;
            Assert.IsNotNull(data);
            Assert.AreEqual("paywallOpen", data["eventType"]);
            Assert.AreEqual("campaign_1", data["placementName"]);
        }
    }

    class ModelTests
    {
        // --- PurchaseResult ---

        [Test]
        public void PurchaseResult_Cancelled_HasCorrectType()
        {
            var result = PurchaseResult.Cancelled();
            Assert.AreEqual(PurchaseResult.ResultType.Cancelled, result.Type);
            Assert.IsInstanceOf<PurchaseResult.CancelledResult>(result);
        }

        [Test]
        public void PurchaseResult_Purchased_HasCorrectType()
        {
            var result = PurchaseResult.Purchased();
            Assert.AreEqual(PurchaseResult.ResultType.Purchased, result.Type);
        }

        [Test]
        public void PurchaseResult_Pending_HasCorrectType()
        {
            var result = PurchaseResult.Pending();
            Assert.AreEqual(PurchaseResult.ResultType.Pending, result.Type);
        }

        [Test]
        public void PurchaseResult_Failed_StoresError()
        {
            var result = PurchaseResult.Failed("network timeout");
            Assert.AreEqual(PurchaseResult.ResultType.Failed, result.Type);
            Assert.AreEqual("network timeout", result.Error);
        }

        // --- RestorationResult ---

        [Test]
        public void RestorationResult_Restored_HasCorrectType()
        {
            var result = RestorationResult.Restored();
            Assert.AreEqual(RestorationResult.ResultType.Restored, result.Type);
        }

        [Test]
        public void RestorationResult_Failed_StoresError()
        {
            var result = RestorationResult.Failed("no purchases found");
            Assert.AreEqual(RestorationResult.ResultType.Failed, result.Type);
            Assert.AreEqual("no purchases found", result.Error);
        }

        // --- PaywallResult ---

        [Test]
        public void PaywallResult_Purchased_StoresProductId()
        {
            var result = PaywallResult.Purchased("com.app.premium");
            Assert.AreEqual(PaywallResult.ResultType.Purchased, result.Type);
            Assert.AreEqual("com.app.premium", result.ProductId);
        }

        [Test]
        public void PaywallResult_Declined_HasCorrectType()
        {
            var result = PaywallResult.Declined();
            Assert.AreEqual(PaywallResult.ResultType.Declined, result.Type);
        }

        [Test]
        public void PaywallResult_Restored_HasCorrectType()
        {
            var result = PaywallResult.Restored();
            Assert.AreEqual(PaywallResult.ResultType.Restored, result.Type);
        }

        // --- SubscriptionStatus ---

        [Test]
        public void SubscriptionStatus_Active_StoresEntitlements()
        {
            var entitlements = new List<Entitlement>
            {
                new Entitlement { Id = "premium", Type = EntitlementType.ServiceLevel, IsActive = true }
            };

            var status = SubscriptionStatus.CreateActive(entitlements);
            Assert.AreEqual(SubscriptionStatus.StatusType.Active, status.Type);
            Assert.IsInstanceOf<SubscriptionStatus.ActiveStatus>(status);
            Assert.AreEqual(1, ((SubscriptionStatus.ActiveStatus)status).Entitlements.Count);
            Assert.AreEqual("premium", ((SubscriptionStatus.ActiveStatus)status).Entitlements[0].Id);
        }

        [Test]
        public void SubscriptionStatus_Inactive_HasCorrectType()
        {
            var status = SubscriptionStatus.CreateInactive();
            Assert.AreEqual(SubscriptionStatus.StatusType.Inactive, status.Type);
        }

        [Test]
        public void SubscriptionStatus_Unknown_HasCorrectType()
        {
            var status = SubscriptionStatus.CreateUnknown();
            Assert.AreEqual(SubscriptionStatus.StatusType.Unknown, status.Type);
        }

        // --- TriggerResult ---

        [Test]
        public void TriggerResult_PlacementNotFound_HasCorrectType()
        {
            var result = TriggerResult.PlacementNotFound();
            Assert.AreEqual(TriggerResult.ResultType.PlacementNotFound, result.Type);
        }

        [Test]
        public void TriggerResult_Paywall_StoresExperiment()
        {
            var experiment = new Experiment
            {
                Id = "exp_1",
                GroupId = "group_1",
                Variant = new Variant { Id = "var_1", Type = VariantType.Treatment, PaywallId = "pw_1" }
            };

            var result = TriggerResult.Paywall(experiment);
            Assert.AreEqual(TriggerResult.ResultType.Paywall, result.Type);
            Assert.IsInstanceOf<TriggerResult.PaywallTriggerResult>(result);
            Assert.AreEqual("exp_1", ((TriggerResult.PaywallTriggerResult)result).Experiment.Id);
        }

        [Test]
        public void TriggerResult_Error_StoresMessage()
        {
            var result = TriggerResult.Error("config not loaded");
            Assert.AreEqual(TriggerResult.ResultType.Error, result.Type);
            Assert.AreEqual("config not loaded", ((TriggerResult.ErrorResult)result).ErrorMessage);
        }

        // --- PresentationResult ---

        [Test]
        public void PresentationResult_AllTypes_HaveCorrectTypes()
        {
            Assert.AreEqual(PresentationResult.ResultType.PlacementNotFound,
                PresentationResult.PlacementNotFound().Type);
            Assert.AreEqual(PresentationResult.ResultType.NoAudienceMatch,
                PresentationResult.NoAudienceMatch().Type);
            Assert.AreEqual(PresentationResult.ResultType.PaywallNotAvailable,
                PresentationResult.PaywallNotAvailable().Type);
        }

        [Test]
        public void PresentationResult_Holdout_StoresExperiment()
        {
            var experiment = new Experiment
            {
                Id = "exp_2",
                GroupId = "group_2",
                Variant = new Variant { Id = "var_2", Type = VariantType.Holdout }
            };

            var result = PresentationResult.Holdout(experiment);
            Assert.AreEqual(PresentationResult.ResultType.Holdout, result.Type);
            Assert.AreEqual("exp_2", ((PresentationResult.HoldoutResult)result).Experiment.Id);
        }

        // --- RedemptionResult ---

        [Test]
        public void RedemptionResult_InvalidCode_StoresCode()
        {
            var result = RedemptionResult.InvalidCode("BADCODE");
            Assert.AreEqual(RedemptionResult.ResultType.InvalidCode, result.Type);
            Assert.AreEqual("BADCODE", ((RedemptionResult.InvalidCodeResult)result).Code);
        }

        [Test]
        public void RedemptionResult_Error_StoresCodeAndError()
        {
            var error = new ErrorInfo { Message = "Server error" };
            var result = RedemptionResult.Error("CODE123", error);
            Assert.AreEqual(RedemptionResult.ResultType.Error, result.Type);

            var errorResult = result as RedemptionResult.ErrorResult;
            Assert.AreEqual("CODE123", errorResult.Code);
            Assert.AreEqual("Server error", errorResult.Error.Message);
        }

        [Test]
        public void RedemptionResult_ExpiredCode_StoresInfo()
        {
            var info = new ExpiredCodeInfo { Resent = true, ObfuscatedEmail = "t***@example.com" };
            var result = RedemptionResult.ExpiredCode("EXPIRED1", info);

            var expiredResult = result as RedemptionResult.ExpiredCodeResult;
            Assert.AreEqual("EXPIRED1", expiredResult.Code);
            Assert.IsTrue(expiredResult.Info.Resent);
            Assert.AreEqual("t***@example.com", expiredResult.Info.ObfuscatedEmail);
        }

        // --- Ownership ---

        [Test]
        public void Ownership_AppUser_StoresUserId()
        {
            var ownership = Ownership.AppUser("user_123");
            Assert.AreEqual(Ownership.OwnershipType.AppUser, ownership.Type);
            Assert.AreEqual("user_123", ((Ownership.AppUserOwnership)ownership).AppUserId);
        }

        [Test]
        public void Ownership_Device_StoresDeviceId()
        {
            var ownership = Ownership.Device("device_abc");
            Assert.AreEqual(Ownership.OwnershipType.Device, ownership.Type);
            Assert.AreEqual("device_abc", ((Ownership.DeviceOwnership)ownership).DeviceId);
        }

        // --- StoreIdentifiers ---

        [Test]
        public void StoreIdentifiers_Stripe_StoresFields()
        {
            var ids = StoreIdentifiers.Stripe("cus_123", new List<string> { "sub_1", "sub_2" });
            Assert.AreEqual(StoreIdentifiers.StoreType.Stripe, ids.Type);

            var stripe = ids as StoreIdentifiers.StripeStoreIdentifiers;
            Assert.AreEqual("cus_123", stripe.CustomerId);
            Assert.AreEqual(2, stripe.SubscriptionIds.Count);
        }

        [Test]
        public void StoreIdentifiers_Unknown_StoresAdditionalInfo()
        {
            var info = new Dictionary<string, object> { { "region", "EU" } };
            var ids = StoreIdentifiers.Unknown("custom_store", info);

            var unknown = ids as StoreIdentifiers.UnknownStoreIdentifiers;
            Assert.AreEqual("custom_store", unknown.Store);
            Assert.AreEqual("EU", unknown.AdditionalInfo["region"]);
        }

        // --- Entitlement ---

        [Test]
        public void Entitlement_NullableFields_DefaultToNull()
        {
            var entitlement = new Entitlement
            {
                Id = "pro",
                Type = EntitlementType.ServiceLevel,
                IsActive = true,
                ProductIds = new List<string> { "prod_1" }
            };

            Assert.AreEqual("pro", entitlement.Id);
            Assert.IsTrue(entitlement.IsActive);
            Assert.IsNull(entitlement.Store);
            Assert.IsNull(entitlement.StartsAt);
            Assert.IsNull(entitlement.IsLifetime);
        }

        [Test]
        public void Entitlement_WithAllFields_StoresCorrectly()
        {
            var entitlement = new Entitlement
            {
                Id = "premium",
                Type = EntitlementType.ServiceLevel,
                IsActive = true,
                ProductIds = new List<string> { "monthly", "yearly" },
                LatestProductId = "yearly",
                Store = ProductStore.AppStore,
                StartsAt = 1700000000000L,
                ExpiresAt = 1710000000000L,
                IsLifetime = false,
                WillRenew = true,
                State = LatestSubscriptionState.Subscribed,
                OfferType = LatestSubscriptionOfferType.Trial
            };

            Assert.AreEqual(ProductStore.AppStore, entitlement.Store);
            Assert.AreEqual(LatestSubscriptionState.Subscribed, entitlement.State);
            Assert.AreEqual(LatestSubscriptionOfferType.Trial, entitlement.OfferType);
            Assert.IsTrue(entitlement.WillRenew.Value);
        }

        // --- SuperwallOptions ---

        [Test]
        public void SuperwallOptions_DefaultValues_AreCorrect()
        {
            var options = new SuperwallOptions();

            Assert.AreEqual(NetworkEnvironment.Release, options.NetworkEnvironment);
            Assert.AreEqual(TestModeBehavior.Automatic, options.TestModeBehavior);
            Assert.IsTrue(options.IsExternalDataCollectionEnabled);
            Assert.IsFalse(options.IsGameControllerEnabled);
            Assert.IsFalse(options.ShouldObservePurchases);
            Assert.AreEqual(6, options.MaxConfigRetryCount);
            Assert.IsNull(options.LocaleIdentifier);
        }

        [Test]
        public void PaywallOptions_DefaultValues_AreCorrect()
        {
            var options = new PaywallOptions();

            Assert.IsTrue(options.IsHapticFeedbackEnabled);
            Assert.IsTrue(options.ShouldPreload);
            Assert.IsTrue(options.AutomaticallyDismiss);
            Assert.AreEqual(TransactionBackgroundView.Spinner, options.TransactionBackgroundView);
        }

        [Test]
        public void RestoreFailed_DefaultValues_AreCorrect()
        {
            var restoreFailed = new RestoreFailed();

            Assert.AreEqual("No Subscription Found", restoreFailed.Title);
            Assert.IsNotNull(restoreFailed.Message);
            Assert.AreEqual("Okay", restoreFailed.CloseButtonTitle);
        }

        // --- Experiment / Variant ---

        [Test]
        public void Experiment_StoresAllFields()
        {
            var experiment = new Experiment
            {
                Id = "exp_abc",
                GroupId = "grp_1",
                Variant = new Variant
                {
                    Id = "var_1",
                    Type = VariantType.Treatment,
                    PaywallId = "pw_xyz"
                }
            };

            Assert.AreEqual("exp_abc", experiment.Id);
            Assert.AreEqual("grp_1", experiment.GroupId);
            Assert.AreEqual(VariantType.Treatment, experiment.Variant.Type);
            Assert.AreEqual("pw_xyz", experiment.Variant.PaywallId);
        }

        // --- ConfirmedAssignment ---

        [Test]
        public void ConfirmedAssignment_StoresFields()
        {
            var assignment = new ConfirmedAssignment
            {
                ExperimentId = "exp_1",
                Variant = new Variant { Id = "v1", Type = VariantType.Holdout }
            };

            Assert.AreEqual("exp_1", assignment.ExperimentId);
            Assert.AreEqual(VariantType.Holdout, assignment.Variant.Type);
        }

        // --- IdentityOptions ---

        [Test]
        public void IdentityOptions_DefaultValue_IsFalse()
        {
            var options = new IdentityOptions();
            Assert.IsFalse(options.RestorePaywallAssignments);
        }

        // --- CustomerInfo ---

        [Test]
        public void CustomerInfo_CanBeCreatedWithEmptyLists()
        {
            var info = new CustomerInfo
            {
                Subscriptions = new List<SubscriptionTransaction>(),
                NonSubscriptions = new List<NonSubscriptionTransaction>(),
                Entitlements = new List<Entitlement>(),
                UserId = "user_1"
            };

            Assert.AreEqual("user_1", info.UserId);
            Assert.AreEqual(0, info.Subscriptions.Count);
        }

        [Test]
        public void SubscriptionTransaction_StoresAllFields()
        {
            var tx = new SubscriptionTransaction
            {
                TransactionId = "txn_1",
                ProductId = "com.app.monthly",
                PurchaseDate = 1700000000000L,
                WillRenew = true,
                IsRevoked = false,
                IsInGracePeriod = false,
                IsInBillingRetryPeriod = false,
                IsActive = true,
                ExpirationDate = 1703000000000L,
                OfferType = LatestSubscriptionOfferType.Trial,
                Store = ProductStore.AppStore
            };

            Assert.AreEqual("txn_1", tx.TransactionId);
            Assert.IsTrue(tx.WillRenew);
            Assert.AreEqual(ProductStore.AppStore, tx.Store);
            Assert.AreEqual(LatestSubscriptionOfferType.Trial, tx.OfferType);
        }

        // --- CustomCallback ---

        [Test]
        public void CustomCallback_StoresNameAndVariables()
        {
            var cb = new CustomCallback
            {
                Name = "onPurchaseComplete",
                Variables = new Dictionary<string, object> { { "price", 9.99 } }
            };

            Assert.AreEqual("onPurchaseComplete", cb.Name);
            Assert.AreEqual(9.99, (double)cb.Variables["price"], 0.001);
        }

        [Test]
        public void CustomCallbackResult_StoresStatusAndData()
        {
            var result = new CustomCallbackResult
            {
                Status = CustomCallbackResultStatus.Success,
                Data = new Dictionary<string, object> { { "token", "abc123" } }
            };

            Assert.AreEqual(CustomCallbackResultStatus.Success, result.Status);
            Assert.AreEqual("abc123", result.Data["token"]);
        }

        // --- PaywallPresentationHandler ---

        [Test]
        public void PaywallPresentationHandler_CallbacksCanBeSet()
        {
            var handler = new PaywallPresentationHandler();
            bool presentCalled = false;
            bool errorCalled = false;
            string errorMessage = null;

            handler.OnPresent = (info) => { presentCalled = true; };
            handler.OnError = (err) => { errorCalled = true; errorMessage = err; };

            handler.OnPresent(new PaywallInfo());
            handler.OnError("timeout");

            Assert.IsTrue(presentCalled);
            Assert.IsTrue(errorCalled);
            Assert.AreEqual("timeout", errorMessage);
        }

        [Test]
        public void PaywallPresentationHandler_NullCallbacksAreAllowed()
        {
            var handler = new PaywallPresentationHandler();

            Assert.IsNull(handler.OnPresent);
            Assert.IsNull(handler.OnDismiss);
            Assert.IsNull(handler.OnError);
            Assert.IsNull(handler.OnSkip);
        }
    }
}
