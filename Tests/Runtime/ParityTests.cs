using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Superwall.Internal;
using UnityEngine.TestTools;

namespace Superwall.Tests
{
    /// <summary>
    /// The API added to close the gaps with the native SDKs: what C# makes of the payloads both bridges send
    /// (ci~/android-harness/bridge-tests/ParityPayloadTest.kt has the Android half), what it sends them, and what
    /// happens where there is no native SDK.
    /// </summary>
    class ParityTests
    {
        // Implements one method: the rest of ISuperwallDelegate has default implementations.
        private class EventsOnlyDelegate : ISuperwallDelegate
        {
            public SuperwallEventInfo Last;
            public void HandleSuperwallEvent(SuperwallEventInfo eventInfo) { Last = eventInfo; }
        }

        private class RecordingPurchaseController : IPurchaseController
        {
            public bool Called;
            public string BasePlanId = "unset";
            public string OfferId = "unset";
            public void PurchaseFromAppStore(string productId, Action<PurchaseResult> completion) { }
            public void PurchaseFromGooglePlay(string productId, string basePlanId, string offerId, Action<PurchaseResult> completion)
            {
                Called = true;
                BasePlanId = basePlanId;
                OfferId = offerId;
            }
            public void RestorePurchases(Action<RestorationResult> completion) { }
        }

        private EventsOnlyDelegate _delegate;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BridgeCallbackHandler.Initialize();
            yield return null;
            _delegate = new EventsOnlyDelegate();
            BridgeCallbackHandler.Instance.Delegate = _delegate;
        }

        [TearDown]
        public void TearDown()
        {
            BridgeCallbackHandler.Instance.Delegate = null;
            BridgeCallbackHandler.Instance.PurchaseController = null;
            BridgeCallbackHandler.Instance.OnBackPressed = null;
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

        private SuperwallEventInfo SendEvent(string eventType, Dictionary<string, object> extra)
        {
            var data = new Dictionary<string, object> { { "eventType", eventType }, { "params", new Dictionary<string, object>() } };
            foreach (var pair in extra) data[pair.Key] = pair.Value;
            _delegate.Last = null;
            Send("handleSuperwallEvent", data);
            return _delegate.Last;
        }

        private static Dictionary<string, object> Experiment() => new Dictionary<string, object>
        {
            { "id", "exp" },
            { "groupId", "group" },
            { "variant", new Dictionary<string, object> { { "id", "var" }, { "type", "holdout" } } },
        };

        // --- Paywall info ---

        [Test]
        public void PaywallInfo_CarriesPresentationEligibilityAndProductDetails()
        {
            var info = BridgeCallbackHandler.DeserializePaywallInfo(new Dictionary<string, object>
            {
                { "identifier", "pw" },
                { "presentation", new Dictionary<string, object> { { "style", "drawer" }, { "delay", 300 }, { "height", 70.0 }, { "cornerRadius", 15.0 } } },
                { "introOfferEligibility", "ineligible" },
                { "databaseId", "db-1" },
                { "customerInfo", new Dictionary<string, object> { { "userId", "user-1" } } },
                { "products", new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            { "id", "pro:monthly:intro" }, { "productIdentifier", "pro" }, { "store", "playStore" },
                            { "playStoreProduct", new Dictionary<string, object>
                                {
                                    { "id", "pro" }, { "basePlanIdentifier", "monthly" }, { "offerType", "specified" },
                                    { "offerIdentifier", "intro" }, { "fullIdentifier", "pro:monthly:intro" },
                                }
                            },
                        },
                        new Dictionary<string, object>
                        {
                            { "id", "pro_yearly" }, { "productIdentifier", "pro_yearly" }, { "store", "appStore" },
                            { "appStoreProduct", new Dictionary<string, object> { { "id", "pro_yearly" }, { "billingPlanType", "monthly" } } },
                        },
                        new Dictionary<string, object>
                        {
                            { "id", "price_1" }, { "store", "stripe" },
                            { "stripeProduct", new Dictionary<string, object> { { "id", "price_1" }, { "trialDays", 7 }, { "environment", "live" } } },
                        },
                    }
                },
            });

            Assert.AreEqual(PaywallPresentationStyle.Drawer, info.Presentation.Style);
            Assert.AreEqual(300L, info.Presentation.Delay);
            Assert.AreEqual(70.0, info.Presentation.Height);
            Assert.AreEqual(15.0, info.Presentation.CornerRadius);
            Assert.IsNull(info.Presentation.Width);
            Assert.AreEqual(IntroOfferEligibility.Ineligible, info.IntroOfferEligibility);
            Assert.AreEqual("db-1", info.DatabaseId);
            Assert.AreEqual("user-1", info.CustomerInfo.UserId);

            var playStore = info.Products[0];
            Assert.AreEqual("pro:monthly:intro", playStore.Id);
            Assert.AreEqual("pro", playStore.ProductIdentifier);
            Assert.AreEqual("monthly", playStore.PlayStoreProduct.BasePlanIdentifier);
            Assert.AreEqual(PlayStoreOfferType.Specified, playStore.PlayStoreProduct.OfferType);
            Assert.AreEqual("intro", playStore.PlayStoreProduct.OfferIdentifier);
            Assert.IsNull(playStore.AppStoreProduct);

            Assert.AreEqual(BillingPlanType.Monthly, info.Products[1].AppStoreProduct.BillingPlanType);
            Assert.AreEqual(7, info.Products[2].StripeProduct.TrialDays);
            Assert.AreEqual("live", info.Products[2].StripeProduct.Environment);
            // A bridge that sends no bare identifier leaves the one C# always had.
            Assert.AreEqual("price_1", info.Products[2].ProductIdentifier);
        }

        [Test]
        public void PaywallInfo_WithoutTheNewValues_LeavesThemNull()
        {
            var info = BridgeCallbackHandler.DeserializePaywallInfo(new Dictionary<string, object> { { "identifier", "pw" } });
            Assert.IsNull(info.Presentation);
            Assert.IsNull(info.IntroOfferEligibility);
            Assert.IsNull(info.DatabaseId);
            Assert.IsNull(info.CustomerInfo);
        }

        // --- Store product ---

        [Test]
        public void StoreProduct_CarriesItsPeriodDiscountsAndBillingPlan()
        {
            var discount = new Dictionary<string, object>
            {
                { "offerIdentifier", "winback" }, { "currencyCode", "USD" }, { "price", 4.99 }, { "localizedPriceString", "$4.99" },
                { "paymentMode", "payUpFront" }, { "numberOfPeriods", 3 }, { "type", "promotional" },
                { "subscriptionPeriod", new Dictionary<string, object> { { "value", 1 }, { "unit", "month" } } },
            };
            var product = global::Superwall.Superwall.DeserializeStoreProduct(new Dictionary<string, object>
            {
                { "productIdentifier", "pro" },
                { "subscriptionPeriod", new Dictionary<string, object> { { "value", 1 }, { "unit", "year" } } },
                { "introductoryDiscount", new Dictionary<string, object>
                    {
                        { "price", 0 }, { "localizedPriceString", "Free" }, { "paymentMode", "freeTrial" }, { "numberOfPeriods", 1 }, { "type", "introductory" },
                        { "subscriptionPeriod", new Dictionary<string, object> { { "value", 7 }, { "unit", "day" } } },
                    }
                },
                { "discounts", new List<object> { discount } },
                { "billingPlanType", "upFront" },
                { "isBillingPlanAvailable", true },
            });

            Assert.AreEqual(1, product.SubscriptionPeriod.Value);
            Assert.AreEqual(SubscriptionPeriodUnit.Year, product.SubscriptionPeriod.Unit);
            Assert.AreEqual(DiscountPaymentMode.FreeTrial, product.IntroductoryDiscount.PaymentMode);
            Assert.AreEqual(7, product.IntroductoryDiscount.SubscriptionPeriod.Value);
            Assert.AreEqual(SubscriptionPeriodUnit.Day, product.IntroductoryDiscount.SubscriptionPeriod.Unit);
            Assert.IsNull(product.IntroductoryDiscount.OfferIdentifier);
            Assert.AreEqual(1, product.Discounts.Count);
            Assert.AreEqual("winback", product.Discounts[0].OfferIdentifier);
            Assert.AreEqual(4.99, product.Discounts[0].Price);
            Assert.AreEqual(DiscountPaymentMode.PayUpFront, product.Discounts[0].PaymentMode);
            Assert.AreEqual(DiscountType.Promotional, product.Discounts[0].Type);
            Assert.AreEqual(3, product.Discounts[0].NumberOfPeriods);
            Assert.AreEqual(BillingPlanType.UpFront, product.BillingPlanType);
            Assert.IsTrue(product.IsBillingPlanAvailable);
        }

        [Test]
        public void StoreProduct_FromAndroid_HasAPeriodAndNoDiscounts()
        {
            var product = global::Superwall.Superwall.DeserializeStoreProduct(new Dictionary<string, object>
            {
                { "productIdentifier", "pro" },
                { "subscriptionPeriod", new Dictionary<string, object> { { "value", 3 }, { "unit", "month" } } },
            });
            Assert.AreEqual(3, product.SubscriptionPeriod.Value);
            Assert.AreEqual(SubscriptionPeriodUnit.Month, product.SubscriptionPeriod.Unit);
            Assert.IsNull(product.IntroductoryDiscount);
            Assert.IsNull(product.Discounts);
            Assert.IsNull(product.BillingPlanType);
        }

        // --- Event payloads ---

        [Test]
        public void AttributionMatch_IsTyped_UnderBothProvidersNames()
        {
            var info = SendEvent("attribution_match", new Dictionary<string, object>
            {
                { "attributionMatchInfo", new Dictionary<string, object>
                    {
                        { "provider", "apple_search_ads" }, { "matched", true }, { "source", "search" },
                        { "confidence", "high" }, { "matchScore", 97.5 },
                    }
                },
            });
            Assert.AreEqual(EventType.AttributionMatch, info.EventType);
            Assert.AreEqual(AttributionMatchProvider.AppleSearchAds, info.AttributionMatchInfo.Provider);
            Assert.IsTrue(info.AttributionMatchInfo.Matched);
            Assert.AreEqual("search", info.AttributionMatchInfo.Source);
            Assert.AreEqual(AttributionMatchConfidence.High, info.AttributionMatchInfo.Confidence);
            Assert.AreEqual(97.5, info.AttributionMatchInfo.MatchScore);
            Assert.IsNull(info.AttributionMatchInfo.Reason);

            var noMatch = BridgeCallbackHandler.DeserializeAttributionMatchInfo(new Dictionary<string, object>
            {
                { "provider", "mmp" }, { "matched", false }, { "reason", "no_candidates" },
            });
            Assert.AreEqual(AttributionMatchProvider.Mmp, noMatch.Provider);
            Assert.IsFalse(noMatch.Matched);
            Assert.IsNull(noMatch.Confidence);
            Assert.IsNull(noMatch.MatchScore);
            Assert.AreEqual("no_candidates", noMatch.Reason);
        }

        [Test]
        public void PageView_CarriesThePageAndWhereTheUserCameFrom()
        {
            var info = SendEvent("paywall_page_view", new Dictionary<string, object>
            {
                { "pageViewData", new Dictionary<string, object>
                    {
                        { "pageNodeId", "node-2" }, { "flowPosition", 1 }, { "pageName", "Plans" }, { "navigationNodeId", "nav-1" },
                        { "previousPageNodeId", "node-1" }, { "previousFlowPosition", 0 }, { "navigationType", "forward" },
                        { "timeOnPreviousPageMs", 1200 },
                    }
                },
            });
            Assert.AreEqual("node-2", info.PageViewData.PageNodeId);
            Assert.AreEqual(1, info.PageViewData.FlowPosition);
            Assert.AreEqual("Plans", info.PageViewData.PageName);
            Assert.AreEqual("node-1", info.PageViewData.PreviousPageNodeId);
            Assert.AreEqual(0, info.PageViewData.PreviousFlowPosition);
            Assert.AreEqual("forward", info.PageViewData.NavigationType);
            Assert.AreEqual(1200, info.PageViewData.TimeOnPreviousPageMs);

            var entry = BridgeCallbackHandler.DeserializePageViewData(new Dictionary<string, object> { { "pageNodeId", "node-1" }, { "navigationType", "entry" } });
            Assert.IsNull(entry.PreviousFlowPosition);
            Assert.IsNull(entry.TimeOnPreviousPageMs);
        }

        [Test]
        public void TransactionProduct_ReadsThePriceFromANumberOrAnInvariantString()
        {
            // Android sends the raw price as a string, iOS as a number.
            foreach (var raw in new object[] { "199.99", 199.99 })
            {
                var product = BridgeCallbackHandler.DeserializeTransactionProduct(new Dictionary<string, object>
                {
                    { "id", "coins_100" },
                    { "price", new Dictionary<string, object> { { "raw", raw }, { "localized", "€199,99" }, { "yearly", "€199,99" } } },
                    { "trialPeriod", new Dictionary<string, object> { { "days", 7 }, { "weeks", 1 }, { "text", "7-day" }, { "endAt", 1793275200000L } } },
                    { "period", new Dictionary<string, object> { { "alt", "year" }, { "ly", "yearly" }, { "unit", "year" }, { "years", 1 } } },
                    { "locale", "de_DE" },
                    { "languageCode", "de" },
                    { "currency", new Dictionary<string, object> { { "code", "EUR" }, { "symbol", "€" } } },
                });
                Assert.AreEqual("coins_100", product.Id);
                Assert.AreEqual(199.99, product.Price.Raw, 0.0001);
                Assert.AreEqual("€199,99", product.Price.Localized);
                Assert.AreEqual(7, product.TrialPeriod.Days);
                Assert.AreEqual(1793275200000L, product.TrialPeriod.EndAt);
                Assert.AreEqual(SubscriptionPeriodUnit.Year, product.Period.Unit);
                Assert.AreEqual("yearly", product.Period.Ly);
                Assert.AreEqual("EUR", product.Currency.Code);
            }

            var oneOff = BridgeCallbackHandler.DeserializeTransactionProduct(new Dictionary<string, object> { { "id", "coins" } });
            Assert.IsNull(oneOff.TrialPeriod);
            Assert.IsNull(oneOff.Period);
        }

        [Test]
        public void NonRecurringPurchase_KeepsTheProductIdentifier_AndAddsTheTransactionProduct()
        {
            var info = SendEvent("nonRecurringProduct_purchase", new Dictionary<string, object>
            {
                { "product", new Dictionary<string, object> { { "productIdentifier", "coins_100" } } },
                { "transactionProduct", new Dictionary<string, object> { { "id", "coins_100" }, { "locale", "en_US" } } },
            });
            Assert.AreEqual("coins_100", info.Product.ProductIdentifier);
            Assert.AreEqual("coins_100", info.TransactionProduct.Id);
            Assert.AreEqual("en_US", info.TransactionProduct.Locale);
        }

        [Test]
        public void PresentationRequest_CarriesWhyNothingWasPresented()
        {
            var holdout = SendEvent("paywallPresentationRequest", new Dictionary<string, object>
            {
                { "status", "noPresentation" },
                { "statusReason", new Dictionary<string, object> { { "type", "holdout" }, { "experiment", Experiment() }, { "info", "In a holdout group." } } },
            });
            Assert.AreEqual(PaywallPresentationRequestStatusType.NoPresentation, holdout.Status);
            Assert.AreEqual(PaywallPresentationRequestStatusReasonType.Holdout, holdout.StatusReason.Type);
            Assert.AreEqual("exp", holdout.StatusReason.Experiment.Id);
            Assert.AreEqual("In a holdout group.", holdout.StatusReason.Info);

            var noConfig = SendEvent("paywallPresentationRequest", new Dictionary<string, object>
            {
                { "status", "noPresentation" },
                { "statusReason", new Dictionary<string, object> { { "type", "noPaywallViewController" } } },
            });
            Assert.AreEqual(PaywallPresentationRequestStatusReasonType.NoPaywallViewController, noConfig.StatusReason.Type);
            Assert.IsNull(noConfig.StatusReason.Experiment);

            var presented = SendEvent("paywallPresentationRequest", new Dictionary<string, object> { { "status", "presentation" } });
            Assert.IsNull(presented.StatusReason);
        }

        [Test]
        public void SmallEventValues_AreRead()
        {
            Assert.AreEqual(850.0, SendEvent("shimmerView_complete", new Dictionary<string, object> { { "shimmerDuration", 850.0 } }).ShimmerDuration);

            var resource = SendEvent("paywallResourceLoad_fail", new Dictionary<string, object> { { "resourceUrl", "https://cdn.example.com/font.woff" }, { "error", "404" } });
            Assert.AreEqual("https://cdn.example.com/font.woff", resource.ResourceUrl);
            Assert.AreEqual("404", resource.Error);

            Assert.AreEqual(2, SendEvent("review_granted", new Dictionary<string, object> { { "reviewCount", 2 } }).ReviewCount);
            Assert.AreEqual(3, SendEvent("review_denied", new Dictionary<string, object> { { "reviewCount", 3 } }).ReviewCount);
            // A bridge that only sends the older key still fills the new field.
            var requested = SendEvent("review_requested", new Dictionary<string, object> { { "reviewRequestedCount", 1 } });
            Assert.AreEqual(1, requested.ReviewCount);
            Assert.AreEqual(1, requested.ReviewRequestedCount);

            var placement = SendEvent("custom_placement", new Dictionary<string, object>
            {
                { "name", "tapped_banner" },
                { "placementParams", new Dictionary<string, object> { { "banner", "summer" } } },
            });
            Assert.AreEqual("summer", placement.PlacementParams["banner"]);

            var changed = SendEvent("customerInfo_didChange", new Dictionary<string, object>
            {
                { "previousCustomerInfo", new Dictionary<string, object> { { "userId", "before" } } },
                { "customerInfo", new Dictionary<string, object> { { "userId", "after" } } },
            });
            Assert.AreEqual("before", changed.PreviousCustomerInfo.UserId);
            Assert.AreEqual("after", changed.CustomerInfo.UserId);
        }

        [Test]
        public void Entitlement_ReadsTheAndroidOnlyStateAndOfferTypes()
        {
            var status = BridgeCallbackHandler.DeserializeSubscriptionStatus(new Dictionary<string, object>
            {
                { "type", "active" },
                { "entitlements", new List<object>
                    {
                        new Dictionary<string, object> { { "id", "a" }, { "state", "unknown" }, { "offerType", "subscription" } },
                        new Dictionary<string, object> { { "id", "b" }, { "offerType", "revoked" } },
                    }
                },
            });
            var entitlements = ((SubscriptionStatus.ActiveStatus)status).Entitlements;
            Assert.AreEqual(LatestSubscriptionState.Unknown, entitlements[0].State);
            Assert.AreEqual(LatestSubscriptionOfferType.Subscription, entitlements[0].OfferType);
            Assert.AreEqual(LatestSubscriptionOfferType.Revoked, entitlements[1].OfferType);
            Assert.IsNull(entitlements[1].State);
        }

        // --- Presentation handlers ---

        [Test]
        public void OnSkip_PassesTheHoldoutExperiment_ToBothHandlers()
        {
            PaywallSkippedReason? plain = null;
            PaywallSkippedReason? detailed = null;
            Experiment experiment = null;
            BridgeCallbackHandler.Instance.RegisterPresentationHandler("skip-1", new PaywallPresentationHandler
            {
                OnSkip = reason => plain = reason,
                OnSkipWithExperiment = (reason, exp) => { detailed = reason; experiment = exp; },
            });

            Send("onSkip", new Dictionary<string, object> { { "handlerId", "skip-1" }, { "reason", "holdout" }, { "experiment", Experiment() } });

            Assert.AreEqual(PaywallSkippedReason.Holdout, plain);
            Assert.AreEqual(PaywallSkippedReason.Holdout, detailed);
            Assert.AreEqual("exp", experiment.Id);
            Assert.AreEqual(VariantType.Holdout, experiment.Variant.Type);
        }

        [Test]
        public void OnSkip_WithoutAnExperiment_PassesNull()
        {
            var called = false;
            Experiment experiment = new Experiment();
            BridgeCallbackHandler.Instance.RegisterPresentationHandler("skip-2", new PaywallPresentationHandler
            {
                OnSkipWithExperiment = (reason, exp) => { called = true; experiment = exp; },
            });
            Send("onSkip", new Dictionary<string, object> { { "handlerId", "skip-2" }, { "reason", "noAudienceMatch" } });
            Assert.IsTrue(called);
            Assert.IsNull(experiment);
        }

        [Test]
        public void PresentationHandler_IsLetGo_OnceItsPaywallIsDone()
        {
            var handler = BridgeCallbackHandler.Instance;
            var before = handler.PresentationHandlerCount;
            var dismissals = 0;
            var paywallHandler = new PaywallPresentationHandler { OnDismiss = (info, result) => dismissals++, OnError = error => { }, OnSkip = reason => { } };

            handler.RegisterPresentationHandler("done-dismiss", paywallHandler);
            handler.RegisterPresentationHandler("done-skip", paywallHandler);
            handler.RegisterPresentationHandler("done-error", paywallHandler);
            handler.RegisterPresentationHandler("done-no-callback", new PaywallPresentationHandler());
            Assert.AreEqual(before + 4, handler.PresentationHandlerCount);

            var declined = new Dictionary<string, object> { { "type", "declined" } };
            Send("onPresent", new Dictionary<string, object> { { "handlerId", "done-dismiss" }, { "paywallInfo", new Dictionary<string, object>() } });
            Assert.AreEqual(before + 4, handler.PresentationHandlerCount, "a presented paywall still has callbacks to come");

            Send("onDismiss", new Dictionary<string, object>
            {
                { "handlerId", "done-dismiss" }, { "result", declined },
                { "paywallInfo", new Dictionary<string, object> { { "closeReason", "manualClose" } } },
            });
            Send("onSkip", new Dictionary<string, object> { { "handlerId", "done-skip" }, { "reason", "placementNotFound" } });
            Send("onError", new Dictionary<string, object> { { "handlerId", "done-error" }, { "error", "boom" } });
            // Let go even when the app set no callback for the event that ends it.
            Send("onDismiss", new Dictionary<string, object>
            {
                { "handlerId", "done-no-callback" }, { "result", declined },
                { "paywallInfo", new Dictionary<string, object> { { "closeReason", "systemLogic" } } },
            });

            Assert.AreEqual(1, dismissals);
            Assert.AreEqual(before, handler.PresentationHandlerCount);
        }

        [Test]
        public void PresentationHandler_IsKept_WhenThePaywallClosesForTheNextOne()
        {
            var handler = BridgeCallbackHandler.Instance;
            var before = handler.PresentationHandlerCount;
            var dismissals = 0;
            handler.RegisterPresentationHandler("next-1", new PaywallPresentationHandler { OnDismiss = (info, result) => dismissals++ });

            var dismissal = new Dictionary<string, object>
            {
                { "handlerId", "next-1" }, { "result", new Dictionary<string, object> { { "type", "declined" } } },
                { "paywallInfo", new Dictionary<string, object> { { "closeReason", "forNextPaywall" } } },
            };
            Send("onDismiss", dismissal);
            Assert.AreEqual(before + 1, handler.PresentationHandlerCount, "the next paywall reports to the same handler");

            ((Dictionary<string, object>)dismissal["paywallInfo"])["closeReason"] = "manualClose";
            Send("onDismiss", dismissal);
            Assert.AreEqual(2, dismissals);
            Assert.AreEqual(before, handler.PresentationHandlerCount);
        }

        // --- Purchase controller ---

        [Test]
        public void GooglePlayPurchase_WithNoOffer_PassesNull_NotAnEmptyString()
        {
            var controller = new RecordingPurchaseController();
            BridgeCallbackHandler.Instance.PurchaseController = controller;
            Send("purchaseFromGooglePlay", new Dictionary<string, object>
            {
                { "productId", "coins_100" }, { "basePlanId", null }, { "offerId", null }, { "callbackId", "cb-null-offer" },
            });
            Assert.IsTrue(controller.Called);
            Assert.IsNull(controller.BasePlanId);
            Assert.IsNull(controller.OfferId);
        }

        // --- Options ---

        [Test]
        public void Options_SendTheCustomEnvironmentDevServerAndAndroidPaywallOptions()
        {
            var options = new SuperwallOptions
            {
                NetworkEnvironment = NetworkEnvironment.Custom,
                CustomNetworkEnvironment = new CustomNetworkEnvironment { Domain = "http://10.0.2.2:3000", CollectorHost = "collector.example.com", Port = 3000 },
                DevServer = DevServer.At("http://192.168.1.10:6100"),
            };
            options.Paywalls.PreloadDeviceOverrides = new Dictionary<DeviceTier, bool> { { DeviceTier.UltraLow, false }, { DeviceTier.High, true } };
            options.Paywalls.LoadingColorResourceName = "brand_primary";
            options.Paywalls.OnBackPressed = info => { };

            var json = SerializeOptions(options);
            Assert.AreEqual("Custom", json["networkEnvironment"]);
            var custom = (Dictionary<string, object>)json["customNetworkEnvironment"];
            Assert.AreEqual("http://10.0.2.2:3000", custom["domain"]);
            Assert.AreEqual("collector.example.com", custom["collectorHost"]);
            Assert.AreEqual(3000L, Convert.ToInt64(custom["port"]));
            Assert.IsFalse(custom.ContainsKey("baseHost"));
            Assert.AreEqual("http://192.168.1.10:6100", ((Dictionary<string, object>)json["devServer"])["url"]);

            var paywalls = (Dictionary<string, object>)json["paywalls"];
            var overrides = (Dictionary<string, object>)paywalls["preloadDeviceOverrides"];
            Assert.AreEqual(false, overrides["ultraLow"]);
            Assert.AreEqual(true, overrides["high"]);
            Assert.AreEqual("brand_primary", paywalls["loadingColorResourceName"]);
            Assert.AreEqual(true, paywalls["hasOnBackPressed"]);
        }

        [Test]
        public void Options_LeaveTheNewValuesOut_WhenUnset()
        {
            var options = new SuperwallOptions { CustomNetworkEnvironment = new CustomNetworkEnvironment { Domain = "ignored.example.com" } };
            var json = SerializeOptions(options);
            // Only sent with NetworkEnvironment.Custom.
            Assert.AreEqual("Release", json["networkEnvironment"]);
            Assert.IsFalse(json.ContainsKey("customNetworkEnvironment"));
            Assert.IsFalse(json.ContainsKey("devServer"));
            var paywalls = (Dictionary<string, object>)json["paywalls"];
            Assert.IsFalse(paywalls.ContainsKey("preloadDeviceOverrides"));
            Assert.IsFalse(paywalls.ContainsKey("loadingColorResourceName"));
            Assert.IsFalse(paywalls.ContainsKey("hasOnBackPressed"));

            // The dev server on localhost: present, with no address.
            json = SerializeOptions(new SuperwallOptions { DevServer = DevServer.Default });
            Assert.AreEqual(0, ((Dictionary<string, object>)json["devServer"]).Count);
        }

        [Test]
        public void OnBackPressed_ReachesTheOptionsCallback_WithOrWithoutPaywallInfo()
        {
            var calls = new List<PaywallInfo>();
            BridgeCallbackHandler.Instance.OnBackPressed = info => calls.Add(info);

            Send("onBackPressed", new Dictionary<string, object> { { "paywallInfo", new Dictionary<string, object> { { "identifier", "pw" } } } });
            Send("onBackPressed", new Dictionary<string, object>());

            Assert.AreEqual(2, calls.Count);
            Assert.AreEqual("pw", calls[0].Identifier);
            Assert.IsNull(calls[1]);
        }

        // --- No native SDK (the Editor) ---

        [Test]
        public void WithoutANativeSdk_CompletionsRunAFrameLater_AndNothingStaysRegistered()
        {
            var superwall = UnconfiguredInstance();
            var handler = BridgeCallbackHandler.Instance;
            handler.RunSimulatedCallbacks();
            var pendingBefore = handler.PendingAsyncCallbackCount;
            var handlersBefore = handler.PresentationHandlerCount;

            var featureRan = false;
            Dictionary<string, StoreProduct> products = null;
            string productsError = null;
            PurchaseResult purchase = null;
            RestorationResult restore = null;
            PresentationResult presentation = null;
            List<ConfirmedAssignment> assignments = null;
            CustomerInfo customerInfo = null;
            var dismissed = false;
            var alertClosed = false;
            var customerCenterDismissed = false;

            superwall.RegisterPlacement("campaign", null, new PaywallPresentationHandler(), () => featureRan = true);
            superwall.GetProducts(new List<string> { "pro" }, (result, error) => { products = result; productsError = error; });
            superwall.Purchase("pro", result => purchase = result);
            superwall.RestorePurchases(result => restore = result);
            superwall.GetPresentationResult("campaign", null, result => presentation = result);
            superwall.GetAssignments(result => assignments = result);
            superwall.GetCustomerInfo(result => customerInfo = result);
            superwall.Dismiss(() => dismissed = true);
            superwall.ShowAlert("Title", onAction: () => Assert.Fail("no alert is shown, so no action is taken"), onClose: () => alertClosed = true);
            superwall.PresentCustomerCenter(onDismiss: () => customerCenterDismissed = true);

            Assert.IsFalse(featureRan, "a native reply never arrives inside the call that asked for it");
            Assert.IsNull(products);

            handler.RunSimulatedCallbacks();

            Assert.IsTrue(featureRan);
            Assert.AreEqual(0, products.Count);
            Assert.AreEqual(global::Superwall.Superwall.NoNativeSdkMessage, productsError);
            Assert.AreEqual(PurchaseResult.ResultType.Failed, purchase.Type);
            Assert.AreEqual(RestorationResult.ResultType.Failed, restore.Type);
            Assert.AreEqual(PresentationResult.PaywallNotAvailable().GetType(), presentation.GetType());
            Assert.AreEqual(0, assignments.Count);
            Assert.IsNotNull(customerInfo);
            Assert.IsTrue(dismissed);
            Assert.IsTrue(alertClosed);
            Assert.IsTrue(customerCenterDismissed);
            Assert.AreEqual(pendingBefore, handler.PendingAsyncCallbackCount);
            Assert.AreEqual(handlersBefore, handler.PresentationHandlerCount);
        }

        [Test]
        public void GetProducts_KeepsItsOldCompletion()
        {
            var superwall = UnconfiguredInstance();
            Dictionary<string, StoreProduct> products = null;
            superwall.GetProducts(new List<string> { "pro" }, result => products = result);
            BridgeCallbackHandler.Instance.RunSimulatedCallbacks();
            Assert.AreEqual(0, products.Count);
        }

        [Test]
        public void PlatformOnlyCalls_AreSafeElsewhere()
        {
            var superwall = UnconfiguredInstance();
            Assert.AreEqual(0, superwall.GrantedEntitlements.Count);
            superwall.GrantedEntitlements = new List<Entitlement> { new Entitlement { Id = "pro" } };
            Assert.IsNull(superwall.ExternalAccountId);
            Assert.AreEqual(0, superwall.GetEntitlementsByProductId("pro").Count);
            Assert.AreEqual(0, superwall.GetEntitlementsByProductId(null).Count);
            superwall.OpenRestoreOnWeb();
            superwall.ObservePurchaseStart("pro");
            superwall.ObservePurchaseError("pro", "declined");
            superwall.ObservePurchaseResult(0, new List<GooglePlayPurchase> { new GooglePlayPurchase("{}", "sig") });
            superwall.SetGameControllerForwardingEnabled(true);
            superwall.DispatchGameControllerKeyEvent(96, true);
            superwall.DispatchGameControllerMotionEvent(0.5f, -0.5f);

            var completed = false;
            superwall.DismissForNextPaywall(() => completed = true);
            Assert.IsTrue(completed, "there is nothing to wait for where the call doesn't exist");

            // Only Android can tear the SDK down.
            Assert.IsFalse(global::Superwall.Superwall.Teardown());
        }
    }
}
