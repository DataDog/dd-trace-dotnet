using System;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class LogOnLineProbeTestDataAttribute : LineProbeTestDataAttribute
{
    public LogOnLineProbeTestDataAttribute(int lineNumber, int columnNumber = 0, bool skip = false, int phase = 1, bool unlisted = false, int expectedNumberOfSnapshots = 1, string conditionJson = null, string templateJson = null, string templateStr = null, string probeId = null, bool captureSnapshot = true, params string[] skipOnFramework) : base(lineNumber, columnNumber, skip, phase, unlisted, expectedNumberOfSnapshots, conditionJson, templateJson, templateStr, probeId, captureSnapshot, skipOnFramework)
    {
    }
}
