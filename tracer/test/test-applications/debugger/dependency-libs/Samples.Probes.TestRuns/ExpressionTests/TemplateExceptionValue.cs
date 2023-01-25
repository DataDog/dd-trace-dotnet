using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    internal class TemplateExceptionValue : IRun
    {
        private const string Dsl = @"{
  ""dsl"": ""Result is: {ref arg}""
}";

        private const string Json = @"{
        ""ref"": ""@exceptions""
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            var result = ThrowExceptionMethod(this);
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        [MethodProbeTestData(
            templateDsl: Dsl,
            templateJson: Json,
            captureSnapshot: false,
            evaluateAt: 1,
            returnTypeName: "System.String",
            parametersTypeName: new[] { "System.Object" })]
        private string ThrowExceptionMethod(object arg)
        {
            var castTo = (string)arg;
            Console.WriteLine(castTo);
            return castTo;
        }
    }
}
