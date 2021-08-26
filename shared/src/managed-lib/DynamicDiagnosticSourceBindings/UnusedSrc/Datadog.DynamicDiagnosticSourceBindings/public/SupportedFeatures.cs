using System;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    public class SupportedFeatures
    {
        public bool ActivityIdFormatOptions { get; }

        public bool FeatureSet_4020 { get; }

        public bool FeatureSet_5000 { get; }

        public string FormatFeatureSetSupportList()
        {
            return $"[{nameof(FeatureSet_5000)}={FeatureSet_5000}, {nameof(FeatureSet_4020)}={FeatureSet_4020}]";
        }
    }
}
