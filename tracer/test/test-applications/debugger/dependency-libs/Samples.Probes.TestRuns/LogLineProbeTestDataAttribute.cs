using System;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class LogLineProbeTestDataAttribute : LineProbeTestDataAttribute
{
    public LogLineProbeTestDataAttribute(int lineNumber, int columnNumber = 0, bool skip = false, int phase = 1, bool unlisted = false, int expectedNumberOfSnapshots = 1, string conditionJson = null, string templateJson = null, string templateStr = null, string probeId = null, bool captureSnapshot = true, bool expectProbeStatusFailure = false, params string[] skipOnFrameworks) : base(lineNumber: lineNumber, columnNumber: columnNumber, skip: skip, phase: phase, unlisted: unlisted, expectedNumberOfSnapshots: expectedNumberOfSnapshots, conditionJson: conditionJson, templateJson: templateJson, templateStr: templateStr, probeId: probeId, captureSnapshot: captureSnapshot, expectProbeStatusFailure: expectProbeStatusFailure, skipOnFrameworks: skipOnFrameworks)
    {
    }
}
