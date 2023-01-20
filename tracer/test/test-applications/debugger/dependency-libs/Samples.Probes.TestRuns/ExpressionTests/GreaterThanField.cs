using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    public class GreaterThanField : IRun
    {
        private int _field;
        private const string Dsl = @"{
  ""dsl"": ""ref _field > 6""
}";

        private const string Json = @"{
    ""gt"": [
      {""ref"": ""_field""},
      6
    ]
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            _field = 5;
            Method(3);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MethodProbeTestData(
            conditionDsl: Dsl,
            conditionJson: Json,
            captureSnapshot: true,
            evaluateAt: 1,
            returnTypeName: "System.String",
            parametersTypeName: new[] { "System.Int32" })]
        public string Method(int intArg)
        {
            var result = intArg + _field;
            Console.WriteLine(result);
            _field += result;
            return $"Field is: {_field}";
        }
    }
}
