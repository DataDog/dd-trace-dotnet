using System;

namespace Samples.Probes.TestRuns;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
public class ExpressionProbeTestDataAttribute : MethodProbeTestDataAttribute
{
    internal ExpressionProbeTestDataAttribute(string dsl, string json, int evaluateAt = 1, string returnTypeName = null, string[] parametersTypeName = null, bool skip = false, int phase = 1, bool unlisted = false, int expectedNumberOfSnapshots = 1, bool useFullTypeName = true, params string[] skipOnFramework)
        : base(returnTypeName, parametersTypeName, skip, phase, unlisted, expectedNumberOfSnapshots, useFullTypeName, skipOnFramework)
    {

        Dsl = dsl;
        Json = json;
        EvaluateAt = evaluateAt;
    }

    public string Dsl { get; }
    public string Json { get; }
    public int EvaluateAt { get; set; }
}
