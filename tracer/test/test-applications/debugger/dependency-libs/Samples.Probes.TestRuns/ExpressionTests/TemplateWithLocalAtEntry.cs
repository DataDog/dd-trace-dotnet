using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    internal class TemplateWithLocalAtEntry : IRun
    {
        private const string Dsl = @"{
  ""dsl"": ""Result is {ref i}""
}";

        private const string Json = @"{
  ""json"": {
        ""ref"": ""i""
    }
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(3);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MethodProbeTestData(
            templateDsl: Dsl,
            templateJson: Json,
            templateStr: "Result is: ",
            captureSnapshot: true,
            evaluateAt: 0,
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
