using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    [LineProbeTestData(30, conditionDsl: Dsl, conditionJson: Json, captureSnapshot: false)]
    internal class LineConditionPartialSnapshot : IRun
    {
        private const string Dsl = @"{
  ""dsl"": ""local > arg""
}";

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
