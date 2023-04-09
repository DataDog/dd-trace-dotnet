using System;

namespace Samples.Probes.TestRuns
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    public abstract class MethodProbeTestDataAttribute : ProbeAttributeBase
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
			string probeId = null,
            bool captureSnapshot = true,
            string evaluateAt = null,
            bool expectProbeStatusFailure = false,
            params string[] skipOnFrameworks)
            : base(skip : skip, phase : phase, unlisted : unlisted, expectedNumberOfSnapshots : expectedNumberOfSnapshots, skipOnFrameworks: skipOnFrameworks, conditionJson: conditionJson, templateJson: templateJson, templateStr: templateStr, probeId: probeId, evaluateAt: evaluateAt, captureSnapshot: captureSnapshot, expectProbeStatusFailure: expectProbeStatusFailure)
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
