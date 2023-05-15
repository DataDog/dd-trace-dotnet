using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    public class TemplateWithDurationAtEntry : IRun
    {
        private const string Json = @"{
        ""ref"": ""@duration""
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(3);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData(
            templateJson: Json,
            templateStr: "Result is: ",
            captureSnapshot: true,
            evaluateAt: "Entry",
            returnTypeName: "System.Int32",
            parametersTypeName: new[] { "System.Int32" })]
        public int Method(int seed)
        {
            int i = 5;
            Console.Write(seed + i);
            i++;
            Console.Write(seed + i);
            return seed + i;
        }
    }
}
