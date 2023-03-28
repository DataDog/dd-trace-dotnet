using System;
using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    internal class PartialSnapshotWithLocalAtEntry : IRun
    {
        private const string Json = @"{
        ""ref"": ""i""
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(3);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MethodProbeTestData(
            templateJson: Json,
            templateStr: "Result is: ",
            captureSnapshot: false,
            evaluateAt: Const.Entry,
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
