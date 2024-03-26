using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    public class GreaterThanDurationFalse : IRun
    {
        private const string Json = @"{
    ""gt"": [
      {""ref"": ""@duration""},
      1000
    ]
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(3);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData(
            expectedNumberOfSnapshots: 0,
            conditionJson: Json,
            captureSnapshot: true,
            evaluateAt: "Exit",
            returnTypeName: "System.String",
            parametersTypeName: new[] { "System.Int32" })]
        public string Method(int intArg)
        {
            Console.WriteLine(intArg);
            System.Threading.Thread.Sleep(250);
            return $"Arg is: {intArg}";
        }
    }
}

