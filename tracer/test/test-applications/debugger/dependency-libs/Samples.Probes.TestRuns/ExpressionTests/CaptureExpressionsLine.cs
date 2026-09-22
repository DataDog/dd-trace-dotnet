using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    [LogLineProbeTestData(lineNumber: 19, captureSnapshot: false, CaptureExpressionsJson = CaptureExpressionTestData.BasicExpressionsJson)]
    public class CaptureExpressionsLine : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method("testValue");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public string Method(string inputValue)
        {
            var localValue = inputValue.Length;
            var testStruct = new CaptureExpressionsMethod.CaptureExpressionTestStruct(inputValue);
            return $"{localValue}:{testStruct.StringValue}";
        }
    }
}
