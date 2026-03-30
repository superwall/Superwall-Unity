using System;

namespace Superwall
{
    [Serializable]
    public class RestorationResult
    {
        public enum ResultType
        {
            Restored,
            Failed
        }

        public ResultType Type;

        protected RestorationResult(ResultType type)
        {
            Type = type;
        }

        public static RestoredResult Restored()
        {
            return new RestoredResult();
        }

        public static FailedResult Failed(string error)
        {
            return new FailedResult(error);
        }

        [Serializable]
        public class RestoredResult : RestorationResult
        {
            public RestoredResult() : base(ResultType.Restored) { }
        }

        [Serializable]
        public class FailedResult : RestorationResult
        {
            public string Error;

            public FailedResult(string error) : base(ResultType.Failed)
            {
                Error = error;
            }
        }
    }
}
