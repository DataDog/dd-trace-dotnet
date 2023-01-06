using System;

namespace Samples.Probes.TestRuns;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
public class ExpressionProbeTestDataAttribute : MethodProbeTestDataAttribute
{
    internal ExpressionProbeTestDataAttribute(
        string conditionDsl = null,
        string conditionJson = null,
        string templateDsl = null,
        string templateJson = null,
        string templateStr = null,
        bool captureSnapshot = true,
        int evaluateAt = 1,
        string returnTypeName = null,
        string[] parametersTypeName = null,
        bool skip = false,
        int phase = 1,
        bool unlisted = false,
        int expectedNumberOfSnapshots = 1,
        bool useFullTypeName = true,
        params string[] skipOnFramework)
        : base(returnTypeName, parametersTypeName, skip, phase, unlisted, expectedNumberOfSnapshots, useFullTypeName, captureSnapshot, evaluateAt, skipOnFramework)
    {

        ConditionDsl = conditionDsl;
        ConditionJson = conditionJson;
        TemplateDsl = templateDsl;
        TemplateJson = templateJson;
        TemplateStr = templateStr;
    }

    public string ConditionDsl { get; }
    public string ConditionJson { get; set; }
    public string TemplateDsl { get; set; }
    public string TemplateJson { get; set; }
    public string TemplateStr { get; set; }
}
