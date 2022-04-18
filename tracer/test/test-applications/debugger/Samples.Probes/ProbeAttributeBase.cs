using System;

namespace Samples.Probes;

public class ProbeAttributeBase : Attribute
{
    public ProbeAttributeBase(bool skip, int phase, bool unlisted, string[] skipOnFrameworks)
    {
        Skip = skip;
        Phase = phase;
        SkipOnFrameworks = skipOnFrameworks;
        Unlisted = unlisted;
    }

    public bool Skip { get; }
    public int Phase { get; }
    public string[] SkipOnFrameworks { get; }
    public bool Unlisted { get; }
}
