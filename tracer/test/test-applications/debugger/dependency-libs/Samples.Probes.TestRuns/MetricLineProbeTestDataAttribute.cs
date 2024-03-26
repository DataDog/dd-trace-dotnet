using System;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class MetricLineProbeTestDataAttribute : LineProbeTestDataAttribute
{
    public MetricLineProbeTestDataAttribute(int lineNumber,
                                      int columnNumber = 0,
                                      bool skip = false,
                                      int phase = 1,
                                      bool unlisted = false,
                                      int expectedNumberOfSnapshots = 1,
                                      string conditionJson = null,
                                      string templateJson = null,
                                      string templateStr = null,
                                      string probeId = null,
                                      bool captureSnapshot = true,
                                      string metricJson = null,
                                      string metricKind = null,
                                      string metricName = null,
                                      bool expectProbeStatusFailure = false,
                                      params string[] skipOnFrameworks)
        : base(lineNumber : lineNumber, columnNumber : columnNumber, skip : skip, phase : phase, unlisted : unlisted, expectedNumberOfSnapshots : expectedNumberOfSnapshots, conditionJson: conditionJson, templateJson: templateJson, templateStr: templateStr, probeId: probeId, captureSnapshot: captureSnapshot, expectProbeStatusFailure: expectProbeStatusFailure, skipOnFrameworks: skipOnFrameworks)
    {
        MetricJson = metricJson;
        MetricName = metricName;
        MetricKind = metricKind;
    }

    public string MetricJson { get; set; }
    public string MetricName { get; set; }
    public string MetricKind { get; set; }
}
