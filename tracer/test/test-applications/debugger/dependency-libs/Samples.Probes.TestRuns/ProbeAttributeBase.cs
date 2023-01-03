using System;

namespace Samples.Probes.TestRuns;

public class ProbeAttributeBase : Attribute
{
    public ProbeAttributeBase(bool skip, int phase, bool unlisted, int expectedNumberOfSnapshots, string[] skipOnFrameworks, bool isFullSnapshot = true, int evaluateAt = 1)
    {
        Skip = skip;
        Phase = phase;
        SkipOnFrameworks = skipOnFrameworks;
        Unlisted = unlisted;
        ExpectedNumberOfSnapshots = expectedNumberOfSnapshots;
        IsFullSnapshot = isFullSnapshot;
        EvaluateAt = evaluateAt;
    }

    public bool Skip { get; }
    public int Phase { get; }
    public string[] SkipOnFrameworks { get; }
    public bool Unlisted { get; }
    public int ExpectedNumberOfSnapshots { get; }
    public bool IsFullSnapshot { get; set; }
    public int EvaluateAt { get; }
}
