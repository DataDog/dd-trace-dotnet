using System;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public abstract class LineProbeTestDataAttribute : ProbeAttributeBase
{
    public LineProbeTestDataAttribute(int lineNumber,
                                      int columnNumber = 0,
                                      bool skip = false,
                                      int phase = 1,
                                      bool unlisted = false,
                                      int expectedNumberOfSnapshots = 1,
                                      string conditionJson = null,
                                      string templateJson = null,
                                      string templateStr = null,
									  string probeId = null,
                                      bool captureSnapshot = true,
                                      bool expectProbeStatusFailure = false,
                                      params string[] skipOnFrameworks)
        : base(skip : skip, phase : phase, unlisted : unlisted, expectedNumberOfSnapshots : expectedNumberOfSnapshots, skipOnFrameworks : skipOnFrameworks, evaluateAt: Const.Exit, conditionJson: conditionJson, templateJson: templateJson, templateStr: templateStr, captureSnapshot: captureSnapshot, probeId: probeId, expectProbeStatusFailure: expectProbeStatusFailure)
    {
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
    }

    public int LineNumber { get; }
    public int ColumnNumber { get; }
}
