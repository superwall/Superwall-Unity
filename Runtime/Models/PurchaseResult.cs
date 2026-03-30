using System;

namespace Superwall
{
    [Serializable]
    public class PurchaseResult
    {
        public enum ResultType
        {
            Cancelled,
            Purchased,
            Pending,
            Failed
        }

        public ResultType Type;

        protected PurchaseResult(ResultType type)
        {
            Type = type;
        }

        public static CancelledResult Cancelled()
        {
            return new CancelledResult();
        }

        public static PurchasedResult Purchased()
        {
            return new PurchasedResult();
        }

        public static PendingResult Pending()
        {
            return new PendingResult();
        }

        public static FailedResult Failed(string error)
        {
            return new FailedResult(error);
        }

        [Serializable]
        public class CancelledResult : PurchaseResult
        {
            public CancelledResult() : base(ResultType.Cancelled) { }
        }

        [Serializable]
        public class PurchasedResult : PurchaseResult
        {
            public PurchasedResult() : base(ResultType.Purchased) { }
        }

        [Serializable]
        public class PendingResult : PurchaseResult
        {
            public PendingResult() : base(ResultType.Pending) { }
        }

        [Serializable]
        public class FailedResult : PurchaseResult
        {
            public string Error;

            public FailedResult(string error) : base(ResultType.Failed)
            {
                Error = error;
            }
        }
    }
}
