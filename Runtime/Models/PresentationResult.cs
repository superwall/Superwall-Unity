using System;

namespace Superwall
{
    [Serializable]
    public class PresentationResult
    {
        public enum ResultType
        {
            PlacementNotFound,
            NoAudienceMatch,
            Paywall,
            Holdout,
            PaywallNotAvailable
        }

        public ResultType Type;

        protected PresentationResult(ResultType type)
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

        public static PaywallPresentationResult Paywall(Experiment experiment)
        {
            return new PaywallPresentationResult(experiment);
        }

        public static HoldoutResult Holdout(Experiment experiment)
        {
            return new HoldoutResult(experiment);
        }

        public static PaywallNotAvailableResult PaywallNotAvailable()
        {
            return new PaywallNotAvailableResult();
        }

        [Serializable]
        public class PlacementNotFoundResult : PresentationResult
        {
            public PlacementNotFoundResult() : base(ResultType.PlacementNotFound) { }
        }

        [Serializable]
        public class NoAudienceMatchResult : PresentationResult
        {
            public NoAudienceMatchResult() : base(ResultType.NoAudienceMatch) { }
        }

        [Serializable]
        public class PaywallPresentationResult : PresentationResult
        {
            public Experiment Experiment;

            public PaywallPresentationResult(Experiment experiment) : base(ResultType.Paywall)
            {
                Experiment = experiment;
            }
        }

        [Serializable]
        public class HoldoutResult : PresentationResult
        {
            public Experiment Experiment;

            public HoldoutResult(Experiment experiment) : base(ResultType.Holdout)
            {
                Experiment = experiment;
            }
        }

        [Serializable]
        public class PaywallNotAvailableResult : PresentationResult
        {
            public PaywallNotAvailableResult() : base(ResultType.PaywallNotAvailable) { }
        }
    }
}
