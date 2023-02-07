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
            string conditionDsl = null,
            string conditionJson = null,
            string templateDsl = null,
            string templateJson = null,
            string templateStr = null,
            string probeId = null,
            bool captureSnapshot = true,
            int evaluateAt = 1,
            params string[] skipOnFramework)
            : base(skip, phase, unlisted, expectedNumberOfSnapshots, skipOnFramework, conditionJson: conditionJson, conditionDsl: conditionDsl, templateDsl: templateDsl, templateJson: templateJson, templateStr: templateStr, probeId: probeId, evaluateAt: evaluateAt, captureSnapshot: captureSnapshot)
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
