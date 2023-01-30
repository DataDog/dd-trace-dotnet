using System;

namespace Samples.Probes.TestRuns
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    public class MethodProbeTestDataAttribute : ProbeAttributeBase
    {
        public MethodProbeTestDataAttribute(
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
            string metricJson = null,
            string metricName = null,
            string metricKind = null,
			string probeId = null,
            bool captureSnapshot = true,
            string evaluateAt = null,
            params string[] skipOnFramework)
            : base(skip, phase, unlisted, expectedNumberOfSnapshots, skipOnFramework, conditionJson: conditionJson, templateJson: templateJson, templateStr: templateStr, metricJson: metricJson, metricName: metricName, metricKind: metricKind, probeId: probeId, evaluateAt: evaluateAt, captureSnapshot: captureSnapshot)
        {

            ReturnTypeName = returnTypeName;
            ParametersTypeName = parametersTypeName;
            UseFullTypeName = useFullTypeName;
        }

        public string ReturnTypeName { get; }
        public string[] ParametersTypeName { get; }
        public bool UseFullTypeName { get; }
    }
}
