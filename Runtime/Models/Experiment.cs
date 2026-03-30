using System;

namespace Superwall
{
    [Serializable]
    public class Experiment
    {
        public string Id;
        public string GroupId;
        public Variant Variant;
    }

    [Serializable]
    public class Variant
    {
        public string Id;
        public VariantType Type;
        public string PaywallId;
    }

    [Serializable]
    public class ConfirmedAssignment
    {
        public string ExperimentId;
        public Variant Variant;
    }
}
