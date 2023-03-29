using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    // Phase 1
    [LogLineProbeTestData(70, 
                       templateJson: Phase1_TemplateJson, 
                       templateStr: "Result is: ", 
                       conditionJson: Condition_EvaluatesToTrue_Json, 
                       captureSnapshot: true,
                       phase: 1,
                       probeId: "99998286d046-9740-a3e4-95cf-ff46699c73c4")]

    // Phase 2
    [LogLineProbeTestData(70,
                       templateJson: TemplateJson,
                       templateStr: "This is a new Template, the local is: ",
                       conditionJson: Condition_EvaluatesToFalse_Json,
                       captureSnapshot: true,
                       phase: 2,
                       probeId: "99998286d046-9740-a3e4-95cf-ff46699c73c4",
                       expectedNumberOfSnapshots: 0 /* the condition turns out false */)]

    // Phase 3
    [LogLineProbeTestData(70,
                       templateJson: TemplateJson,
                       templateStr: "This is a new Template, the local is: ",
                       conditionJson: Condition_EvaluatesToTrue_Json,
                       captureSnapshot: true,
                       phase: 3,
                       probeId: "99998286d046-9740-a3e4-95cf-ff46699c73c4",
                       expectedNumberOfSnapshots: 1)]

    public class ConditionAndTemplateChangeTest : IRun
    {
        private const string Phase1_TemplateJson = @"{
        ""ref"": ""arg""
}";

        private const string Condition_EvaluatesToTrue_Json = @"{
    ""gt"": [
      {""ref"": ""local""},
      {""ref"": ""arg""}
    ]
}";

        private const string TemplateJson = @"{
        ""ref"": ""local""
}";

        private const string Condition_EvaluatesToFalse_Json = @"{
    ""lt"": [
      {""ref"": ""local""},
      {""ref"": ""arg""}
    ]
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            var result = Method(1);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData(phase: 1)]
        string Method(double arg)
        {
            var local = arg + GetInt(arg);
            Console.WriteLine(local);
            return $"Result is: {arg} + {local}";
        }

        [LogMethodProbeTestData(phase: 2)]
        int GetInt(double d)
        {
            return (int)(d + 1);
        }
    }
}
