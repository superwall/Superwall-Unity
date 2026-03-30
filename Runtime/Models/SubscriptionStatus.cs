using System;
using System.Collections.Generic;

namespace Superwall
{
    [Serializable]
    public class SubscriptionStatus
    {
        public enum StatusType
        {
            Active,
            Inactive,
            Unknown
        }

        public StatusType Type;

        protected SubscriptionStatus(StatusType type)
        {
            Type = type;
        }

        public static ActiveStatus CreateActive(List<Entitlement> entitlements)
        {
            return new ActiveStatus(entitlements);
        }

        public static InactiveStatus CreateInactive()
        {
            return new InactiveStatus();
        }

        public static UnknownStatus CreateUnknown()
        {
            return new UnknownStatus();
        }

        [Serializable]
        public class ActiveStatus : SubscriptionStatus
        {
            public List<Entitlement> Entitlements;

            public ActiveStatus(List<Entitlement> entitlements) : base(StatusType.Active)
            {
                Entitlements = entitlements;
            }
        }

        [Serializable]
        public class InactiveStatus : SubscriptionStatus
        {
            public InactiveStatus() : base(StatusType.Inactive) { }
        }

        [Serializable]
        public class UnknownStatus : SubscriptionStatus
        {
            public UnknownStatus() : base(StatusType.Unknown) { }
        }
    }
}
