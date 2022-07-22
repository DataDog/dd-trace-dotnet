using System;

namespace Samples.Probes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class LineProbeTestDataAttribute : ProbeAttributeBase
{
    public LineProbeTestDataAttribute(int lineNumber, int columnNumber = 0, bool skip = false, int phase = 1, bool unlisted = false, int expectedNumberOfSnapshots = 1, params string[] skipOnFramework) 
        : base(skip, phase, unlisted, expectedNumberOfSnapshots, skipOnFramework)
    {
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
    }

    public int LineNumber { get; }
    public int ColumnNumber { get; }
}
