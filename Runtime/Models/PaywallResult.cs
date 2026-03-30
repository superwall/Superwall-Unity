using System;

namespace Superwall
{
    [Serializable]
    public class PaywallResult
    {
        public enum ResultType
        {
            Purchased,
            Declined,
            Restored
        }

        public ResultType Type;

        protected PaywallResult(ResultType type)
        {
            Type = type;
        }

        public static PurchasedResult Purchased(string productId)
        {
            return new PurchasedResult(productId);
        }

        public static DeclinedResult Declined()
        {
            return new DeclinedResult();
        }

        public static RestoredResult Restored()
        {
            return new RestoredResult();
        }

        [Serializable]
        public class PurchasedResult : PaywallResult
        {
            public string ProductId;

            public PurchasedResult(string productId) : base(ResultType.Purchased)
            {
                ProductId = productId;
            }
        }

        [Serializable]
        public class DeclinedResult : PaywallResult
        {
            public DeclinedResult() : base(ResultType.Declined) { }
        }

        [Serializable]
        public class RestoredResult : PaywallResult
        {
            public RestoredResult() : base(ResultType.Restored) { }
        }
    }
}
