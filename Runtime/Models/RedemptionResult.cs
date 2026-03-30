using System;
using System.Collections.Generic;

namespace Superwall
{
    [Serializable]
    public class RedemptionResult
    {
        public enum ResultType
        {
            Success,
            Error,
            ExpiredCode,
            InvalidCode,
            ExpiredSubscription
        }

        public ResultType Type;

        protected RedemptionResult(ResultType type)
        {
            Type = type;
        }

        public static SuccessResult Success(string code, RedemptionInfo redemptionInfo)
        {
            return new SuccessResult(code, redemptionInfo);
        }

        public static ErrorResult Error(string code, ErrorInfo error)
        {
            return new ErrorResult(code, error);
        }

        public static ExpiredCodeResult ExpiredCode(string code, ExpiredCodeInfo info)
        {
            return new ExpiredCodeResult(code, info);
        }

        public static InvalidCodeResult InvalidCode(string code)
        {
            return new InvalidCodeResult(code);
        }

        public static ExpiredSubscriptionResult ExpiredSubscription(string code, RedemptionInfo redemptionInfo)
        {
            return new ExpiredSubscriptionResult(code, redemptionInfo);
        }

        [Serializable]
        public class SuccessResult : RedemptionResult
        {
            public string Code;
            public RedemptionInfo RedemptionInfo;

            public SuccessResult(string code, RedemptionInfo redemptionInfo) : base(ResultType.Success)
            {
                Code = code;
                RedemptionInfo = redemptionInfo;
            }
        }

        [Serializable]
        public class ErrorResult : RedemptionResult
        {
            public string Code;
            public new ErrorInfo Error;

            public ErrorResult(string code, ErrorInfo error) : base(ResultType.Error)
            {
                Code = code;
                Error = error;
            }
        }

        [Serializable]
        public class ExpiredCodeResult : RedemptionResult
        {
            public string Code;
            public ExpiredCodeInfo Info;

            public ExpiredCodeResult(string code, ExpiredCodeInfo info) : base(ResultType.ExpiredCode)
            {
                Code = code;
                Info = info;
            }
        }

        [Serializable]
        public class InvalidCodeResult : RedemptionResult
        {
            public string Code;

            public InvalidCodeResult(string code) : base(ResultType.InvalidCode)
            {
                Code = code;
            }
        }

        [Serializable]
        public class ExpiredSubscriptionResult : RedemptionResult
        {
            public string Code;
            public RedemptionInfo RedemptionInfo;

            public ExpiredSubscriptionResult(string code, RedemptionInfo redemptionInfo) : base(ResultType.ExpiredSubscription)
            {
                Code = code;
                RedemptionInfo = redemptionInfo;
            }
        }
    }

    [Serializable]
    public class ErrorInfo
    {
        public string Message;
    }

    [Serializable]
    public class ExpiredCodeInfo
    {
        public bool Resent;
        public string ObfuscatedEmail;
    }

    [Serializable]
    public class RedemptionInfo
    {
        public Ownership Ownership;
        public PurchaserInfo PurchaserInfo;
        public RedemptionPaywallInfo PaywallInfo;
        public List<Entitlement> Entitlements;
    }

    [Serializable]
    public class Ownership
    {
        public enum OwnershipType
        {
            AppUser,
            Device
        }

        public OwnershipType Type;

        protected Ownership(OwnershipType type)
        {
            Type = type;
        }

        public static AppUserOwnership AppUser(string appUserId)
        {
            return new AppUserOwnership(appUserId);
        }

        public static DeviceOwnership Device(string deviceId)
        {
            return new DeviceOwnership(deviceId);
        }

        [Serializable]
        public class AppUserOwnership : Ownership
        {
            public string AppUserId;

            public AppUserOwnership(string appUserId) : base(OwnershipType.AppUser)
            {
                AppUserId = appUserId;
            }
        }

        [Serializable]
        public class DeviceOwnership : Ownership
        {
            public string DeviceId;

            public DeviceOwnership(string deviceId) : base(OwnershipType.Device)
            {
                DeviceId = deviceId;
            }
        }
    }

    [Serializable]
    public class PurchaserInfo
    {
        public string AppUserId;
        public string Email;
        public StoreIdentifiers StoreIdentifiers;
    }

    [Serializable]
    public class StoreIdentifiers
    {
        public enum StoreType
        {
            Stripe,
            Paddle,
            Unknown
        }

        public StoreType Type;

        protected StoreIdentifiers(StoreType type)
        {
            Type = type;
        }

        public static StripeStoreIdentifiers Stripe(string customerId, List<string> subscriptionIds)
        {
            return new StripeStoreIdentifiers(customerId, subscriptionIds);
        }

        public static PaddleStoreIdentifiers Paddle(string customerId, List<string> subscriptionIds)
        {
            return new PaddleStoreIdentifiers(customerId, subscriptionIds);
        }

        public static UnknownStoreIdentifiers Unknown(string store, Dictionary<string, object> additionalInfo)
        {
            return new UnknownStoreIdentifiers(store, additionalInfo);
        }

        [Serializable]
        public class StripeStoreIdentifiers : StoreIdentifiers
        {
            public string CustomerId;
            public List<string> SubscriptionIds;

            public StripeStoreIdentifiers(string customerId, List<string> subscriptionIds) : base(StoreType.Stripe)
            {
                CustomerId = customerId;
                SubscriptionIds = subscriptionIds;
            }
        }

        [Serializable]
        public class PaddleStoreIdentifiers : StoreIdentifiers
        {
            public string CustomerId;
            public List<string> SubscriptionIds;

            public PaddleStoreIdentifiers(string customerId, List<string> subscriptionIds) : base(StoreType.Paddle)
            {
                CustomerId = customerId;
                SubscriptionIds = subscriptionIds;
            }
        }

        [Serializable]
        public class UnknownStoreIdentifiers : StoreIdentifiers
        {
            public string Store;
            public Dictionary<string, object> AdditionalInfo;

            public UnknownStoreIdentifiers(string store, Dictionary<string, object> additionalInfo) : base(StoreType.Unknown)
            {
                Store = store;
                AdditionalInfo = additionalInfo;
            }
        }
    }

    [Serializable]
    public class RedemptionPaywallInfo
    {
        public string Identifier;
        public string PlacementName;
        public Dictionary<string, object> PlacementParams;
        public string VariantId;
        public string ExperimentId;
    }

    [Serializable]
    public class CustomCallback
    {
        public string Name;
        public Dictionary<string, object> Variables;
    }

    [Serializable]
    public class CustomCallbackResult
    {
        public CustomCallbackResultStatus Status;
        public Dictionary<string, object> Data;
    }
}
