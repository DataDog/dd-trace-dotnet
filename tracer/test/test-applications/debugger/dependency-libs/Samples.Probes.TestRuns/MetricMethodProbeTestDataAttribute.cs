using System;

namespace Samples.Probes.TestRuns
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = true)]
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
            string metricJson = null,
            string metricName = null,
            string metricKind = null,
            string probeId = null,
            bool captureSnapshot = true,
            string evaluateAt = null,
            bool expectProbeStatusFailure = false,
            params string[] skipOnFrameworks)
            : base(returnTypeName: returnTypeName, parametersTypeName: parametersTypeName, skip: skip, phase: phase, unlisted: unlisted, expectedNumberOfSnapshots: expectedNumberOfSnapshots, useFullTypeName: useFullTypeName, conditionJson: null, templateJson: null, templateStr: null, probeId: probeId, evaluateAt: evaluateAt, captureSnapshot: captureSnapshot, expectProbeStatusFailure: expectProbeStatusFailure, skipOnFrameworks: skipOnFrameworks)
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
