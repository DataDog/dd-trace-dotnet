using System;

namespace Samples.Probes.TestRuns;

public class ProbeAttributeBase : Attribute
{
    public ProbeAttributeBase(bool skip, int phase, bool unlisted, int expectedNumberOfSnapshots, string[] skipOnFrameworks, bool captureSnapshot = true, string evaluateAt = null, string conditionJson = null, string templateJson = null, string templateStr = null, string probeId = null, bool expectProbeStatusFailure = false)
    {
        Skip = skip;
        Phase = phase;
        SkipOnFrameworks = skipOnFrameworks;
        Unlisted = unlisted;
        ExpectedNumberOfSnapshots = expectedNumberOfSnapshots;
        CaptureSnapshot = captureSnapshot;
        EvaluateAt = evaluateAt;
        ConditionJson = conditionJson;
        TemplateJson = templateJson;
        TemplateStr = templateStr;
		ProbeId = probeId;
        ExpectProbeStatusFailure = expectProbeStatusFailure;
    }

    public bool Skip { get; }
    public int Phase { get; }
    public string[] SkipOnFrameworks { get; }
    public bool Unlisted { get; }
    public int ExpectedNumberOfSnapshots { get; }
    public bool CaptureSnapshot { get; set; }
    public string EvaluateAt { get; }
    public string ConditionJson { get; set; }
	public string ProbeId { get; set; }
    public string TemplateJson { get; set; }
    public string TemplateStr { get; set; }
    public bool ExpectProbeStatusFailure { get; set; }
}
