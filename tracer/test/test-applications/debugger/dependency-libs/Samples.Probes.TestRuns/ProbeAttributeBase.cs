using System;

namespace Samples.Probes.TestRuns;

public class ProbeAttributeBase : Attribute
{
    public ProbeAttributeBase(bool skip, int phase, bool unlisted, int expectedNumberOfSnapshots, string[] skipOnFrameworks, bool captureSnapshot = true, int evaluateAt = 1, string conditionDsl = null, string conditionJson = null, string templateDsl = null, string templateJson = null, string templateStr = null, string probeId = null)
    {
        Skip = skip;
        Phase = phase;
        SkipOnFrameworks = skipOnFrameworks;
        Unlisted = unlisted;
        ExpectedNumberOfSnapshots = expectedNumberOfSnapshots;
        CaptureSnapshot = captureSnapshot;
        EvaluateAt = evaluateAt;
        ConditionDsl = conditionDsl;
        ConditionJson = conditionJson;
        TemplateDsl = templateDsl;
        TemplateJson = templateJson;
        TemplateStr = templateStr;
        ProbeId = probeId;
    }

    public bool Skip { get; }
    public int Phase { get; }
    public string[] SkipOnFrameworks { get; }
    public bool Unlisted { get; }
    public int ExpectedNumberOfSnapshots { get; }
    public bool CaptureSnapshot { get; set; }
    public int EvaluateAt { get; }
    public string ConditionDsl { get; }
    public string ConditionJson { get; set; }
    public string TemplateDsl { get; set; }
    public string TemplateJson { get; set; }
    public string TemplateStr { get; set; }
    public string ProbeId { get; set; }
}
