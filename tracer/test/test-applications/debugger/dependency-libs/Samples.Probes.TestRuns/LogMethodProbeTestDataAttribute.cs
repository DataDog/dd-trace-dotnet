using System;

namespace Samples.Probes.TestRuns
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    public class LogMethodProbeTestDataAttribute : MethodProbeTestDataAttribute
    {
        public LogMethodProbeTestDataAttribute(
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
    params string[] skipOnFramework)
    : base(returnTypeName, parametersTypeName, skip, phase, unlisted, expectedNumberOfSnapshots, useFullTypeName, conditionJson: conditionJson, templateJson: templateJson, templateStr: templateStr, probeId: probeId, evaluateAt: evaluateAt, skipOnFramework: skipOnFramework, captureSnapshot: captureSnapshot)
        {
        }
    }
}
