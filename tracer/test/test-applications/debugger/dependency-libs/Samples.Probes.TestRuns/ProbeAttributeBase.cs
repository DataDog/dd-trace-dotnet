using System;

namespace Samples.Probes.TestRuns;

public class ProbeAttributeBase : Attribute
{
    public ProbeAttributeBase(bool skip, int phase, bool unlisted, int expectedNumberOfSnapshots, string[] skipOnFrameworks, bool captureSnapshot = true, string evaluateAt = null, string conditionJson = null, string templateJson = null, string templateStr = null, string metricJson = null, string metricName = null, string metricKind = null, string probeId = null)
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
        MetricJson = metricJson;
        MetricName = metricName;
        MetricKind = metricKind;
		ProbeId = probeId;
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
    public string MetricJson { get; set; }
    public string MetricName { get; set; }
    public string MetricKind { get; set; }
}
