using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    [LogLineProbeTestData(26, conditionJson: Json, captureSnapshot: true)]
    internal class LineConditionFullSnapshot : IRun
    {
        private const string Json = @"{
    ""gt"": [
      {""ref"": ""local""},
      {""ref"": ""arg""}
    ]
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            var result = Method(TimeSpan.FromSeconds(1).TotalSeconds);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        string Method(double arg)
        {
            var local = arg + GetInt(arg);
            Console.WriteLine(local);
            return $"Result is: {arg} + {local}";
        }

        int GetInt(double d)
        {
            return (int)(d + 1);
        }
    }
}
