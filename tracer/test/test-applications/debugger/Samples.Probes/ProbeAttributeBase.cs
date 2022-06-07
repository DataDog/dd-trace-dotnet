using System;

namespace Samples.Probes;

public class ProbeAttributeBase : Attribute
{
    public ProbeAttributeBase(bool skip, int phase, bool unlisted, int expectedNumberOfSnapshots, string[] skipOnFrameworks)
    {
        Skip = skip;
        Phase = phase;
        SkipOnFrameworks = skipOnFrameworks;
        Unlisted = unlisted;
        ExpectedNumberOfSnapshots = expectedNumberOfSnapshots;
    }

    public bool Skip { get; }
    public int Phase { get; }
    public string[] SkipOnFrameworks { get; }
    public bool Unlisted { get; }
    public int ExpectedNumberOfSnapshots { get; }
}
