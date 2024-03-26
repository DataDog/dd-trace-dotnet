using System;
using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    internal class TemplateNoException : IRun
    {
        private const string Json = @"{
        ""ref"": ""@exception""
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            var result = ThrowExceptionMethod(this);
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData(
            templateJson: Json,
            captureSnapshot: false,
            evaluateAt: Const.Entry,
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
