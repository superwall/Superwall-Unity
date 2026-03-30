using System;

namespace Superwall
{
    [Serializable]
    public class TriggerResult
    {
        public enum ResultType
        {
            PlacementNotFound,
            NoAudienceMatch,
            Paywall,
            Holdout,
            Error
        }

        public ResultType Type;

        protected TriggerResult(ResultType type)
        {
            Type = type;
        }

        public static PlacementNotFoundResult PlacementNotFound()
        {
            return new PlacementNotFoundResult();
        }

        public static NoAudienceMatchResult NoAudienceMatch()
        {
            return new NoAudienceMatchResult();
        }

        public static PaywallTriggerResult Paywall(Experiment experiment)
        {
            return new PaywallTriggerResult(experiment);
        }

        public static HoldoutResult Holdout(Experiment experiment)
        {
            return new HoldoutResult(experiment);
        }

        public static ErrorResult Error(string errorMessage)
        {
            return new ErrorResult(errorMessage);
        }

        [Serializable]
        public class PlacementNotFoundResult : TriggerResult
        {
            public PlacementNotFoundResult() : base(ResultType.PlacementNotFound) { }
        }

        [Serializable]
        public class NoAudienceMatchResult : TriggerResult
        {
            public NoAudienceMatchResult() : base(ResultType.NoAudienceMatch) { }
        }

        [Serializable]
        public class PaywallTriggerResult : TriggerResult
        {
            public Experiment Experiment;

            public PaywallTriggerResult(Experiment experiment) : base(ResultType.Paywall)
            {
                Experiment = experiment;
            }
        }

        [Serializable]
        public class HoldoutResult : TriggerResult
        {
            public Experiment Experiment;

            public HoldoutResult(Experiment experiment) : base(ResultType.Holdout)
            {
                Experiment = experiment;
            }
        }

        [Serializable]
        public class ErrorResult : TriggerResult
        {
            public string ErrorMessage;

            public ErrorResult(string errorMessage) : base(ResultType.Error)
            {
                ErrorMessage = errorMessage;
            }
        }
    }
}
