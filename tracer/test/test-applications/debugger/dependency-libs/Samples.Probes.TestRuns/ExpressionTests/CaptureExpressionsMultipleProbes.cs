using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    public class CaptureExpressionsMultipleProbes : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method("testValue");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData(
            captureSnapshot: false,
            evaluateAt: Const.Exit,
            returnTypeName: "System.String",
            parametersTypeName: new[] { "System.String" },
            CaptureExpressionsJson = CaptureExpressionTestData.BasicExpressionsJson)]
        [LogMethodProbeTestData(
            captureSnapshot: false,
            evaluateAt: Const.Exit,
            returnTypeName: "System.String",
            parametersTypeName: new[] { "System.String" },
            CaptureExpressionsJson = CaptureExpressionTestData.ComplexExpressionsJson)]
        public string Method(string inputValue)
        {
            var localValue = inputValue.Length;
            var testStruct = new CaptureExpressionsMethod.CaptureExpressionTestStruct(inputValue);
            return $"{localValue}:{testStruct.StringValue}";
        }
    }
}
