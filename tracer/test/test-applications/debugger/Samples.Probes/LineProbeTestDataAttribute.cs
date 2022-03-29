using System;

namespace Samples.Probes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class LineProbeTestDataAttribute : ProbeAttributeBase
{
    public int LineNumber { get; }
    public int ColumnNumber { get; }

    public LineProbeTestDataAttribute(int lineNumber, int columnNumber = 0, bool skip = false, params string[] skipOnFramework) :base(skip, skipOnFramework)
    {
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
    }
}
