using System;

namespace Samples.Probes;

public class ProbeAttributeBase : Attribute
{
    public ProbeAttributeBase(bool skip, string[] skipOnFrameworks)
    {
        Skip = skip;
        SkipOnFrameworks = skipOnFrameworks;
    }

    public bool Skip { get; }
    public string[] SkipOnFrameworks { get; }
}
