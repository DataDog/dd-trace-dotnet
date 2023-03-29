using System;

namespace Samples.Probes.TestRuns
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    public class MetricMethodProbeTestDataAttribute : MethodProbeTestDataAttribute
    {
        public MetricMethodProbeTestDataAttribute
            (
            string returnTypeName = null,
            string[] parametersTypeName = null,
            bool skip = false,
            int phase = 1,
            bool unlisted = false,
            int expectedNumberOfSnapshots = 1,
            bool useFullTypeName = true,
            string conditionJson = null,
            string templateJson = null,
            string templateStr = null,
            string probeId = null,
            bool captureSnapshot = true,
            string evaluateAt = null,
            string metricJson = null,
            string metricName = null,
            string metricKind = null,
            params string[] skipOnFramework)
            : base(returnTypeName, parametersTypeName, skip, phase, unlisted, expectedNumberOfSnapshots, useFullTypeName, conditionJson: conditionJson, templateJson: templateJson, templateStr: templateStr, probeId: probeId, evaluateAt: evaluateAt, captureSnapshot: captureSnapshot, skipOnFramework: skipOnFramework)
        {

            MetricJson = metricJson;
            MetricName = metricName;
            MetricKind = metricKind;
        }

        public string MetricJson { get; set; }
        public string MetricName { get; set; }
        public string MetricKind { get; set; }
    }
}
