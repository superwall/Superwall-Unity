using System;

namespace Superwall
{
    [Serializable]
    public class ConfigurationResult
    {
        public enum ResultType
        {
            Success,
            Failed
        }

        public ResultType Type;

        protected ConfigurationResult(ResultType type)
        {
            Type = type;
        }

        public bool IsSuccess => Type == ResultType.Success;

        public static SuccessResult Success()
        {
            return new SuccessResult();
        }

        public static FailedResult Failed(string error)
        {
            return new FailedResult(error);
        }

        [Serializable]
        public class SuccessResult : ConfigurationResult
        {
            public SuccessResult() : base(ResultType.Success) { }
        }

        [Serializable]
        public class FailedResult : ConfigurationResult
        {
            public string Error;

            public FailedResult(string error) : base(ResultType.Failed)
            {
                Error = error;
            }
        }
    }
}
