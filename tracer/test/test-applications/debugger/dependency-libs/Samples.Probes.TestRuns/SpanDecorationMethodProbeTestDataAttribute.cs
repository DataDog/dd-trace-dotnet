using System;

namespace Samples.Probes.TestRuns
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    public class SpanDecorationMethodProbeTestDataAttribute : MethodProbeTestDataAttribute
    {
        public SpanDecorationMethodProbeTestDataAttribute
            (
            string returnTypeName = null,
            string[] parametersTypeName = null,
            bool skip = false,
            int phase = 1,
            bool unlisted = false,
            int expectedNumberOfSnapshots = 1,
            bool useFullTypeName = true,
            string decorationsJson = null,
            string probeId = null,
            bool captureSnapshot = true,
            string evaluateAt = null,
            bool expectProbeStatusFailure = false,
            params string[] skipOnFrameworks)
            : base(returnTypeName: returnTypeName, parametersTypeName: parametersTypeName, skip: skip, phase: phase, unlisted: unlisted, expectedNumberOfSnapshots: expectedNumberOfSnapshots, useFullTypeName: useFullTypeName, conditionJson: null, templateJson: null, templateStr: null, probeId: probeId, evaluateAt: evaluateAt, captureSnapshot: captureSnapshot, expectProbeStatusFailure: expectProbeStatusFailure, skipOnFrameworks: skipOnFrameworks)
        {
            Decorations = decorationsJson;
        }

        public string Decorations { get; set; }
    }
}
