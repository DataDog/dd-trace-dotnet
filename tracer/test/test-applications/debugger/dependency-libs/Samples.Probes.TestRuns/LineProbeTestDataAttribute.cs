using System;

namespace Samples.Probes.TestRuns;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class LineProbeTestDataAttribute : ProbeAttributeBase
{
    public LineProbeTestDataAttribute(int lineNumber,
                                      int columnNumber = 0,
                                      bool skip = false,
                                      int phase = 1,
                                      bool unlisted = false,
                                      int expectedNumberOfSnapshots = 1,
                                      string conditionDsl = null,
                                      string conditionJson = null,
                                      string templateDsl = null,
                                      string templateJson = null,
                                      string templateStr = null,
                                      bool captureSnapshot = true,
                                      params string[] skipOnFramework)
        : base(skip, phase, unlisted, expectedNumberOfSnapshots, skipOnFramework, conditionDsl: conditionDsl, evaluateAt: 1, conditionJson: conditionJson, templateDsl: templateDsl, templateJson: templateJson, templateStr: templateStr, captureSnapshot: captureSnapshot)
    {
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
    }

    public int LineNumber { get; }
    public int ColumnNumber { get; }
}
