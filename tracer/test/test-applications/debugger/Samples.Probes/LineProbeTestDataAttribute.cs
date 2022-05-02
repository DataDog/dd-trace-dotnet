using System;

namespace Samples.Probes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class LineProbeTestDataAttribute : ProbeAttributeBase
{
    public LineProbeTestDataAttribute(int lineNumber, int columnNumber = 0, bool skip = false, int expectedNumberOfSnapshots = 1, int phase = 1, bool unlisted = false, params string[] skipOnFramework) 
        : base(skip, phase, unlisted, skipOnFramework)
    {
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
        ExpectedNumberOfSnapshots = expectedNumberOfSnapshots;
    }

    public int LineNumber { get; }
    public int ColumnNumber { get; }
    public int ExpectedNumberOfSnapshots { get; }
}
