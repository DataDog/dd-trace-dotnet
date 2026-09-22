using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    public class CaptureExpressionsMethod : IRun
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
        public string Method(string inputValue)
        {
            var localValue = inputValue.Length;
            var testStruct = new CaptureExpressionTestStruct(inputValue);
            return $"{localValue}:{testStruct.StringValue}";
        }

        public readonly struct CaptureExpressionTestStruct
        {
            public CaptureExpressionTestStruct(string stringValue)
            {
                StringValue = stringValue;
                Collection = new List<string> { "one", "two", "three" };
                Dictionary = new Dictionary<string, string> { { "one", "first" }, { "two", "second" } };
            }

            public string StringValue { get; }

            public List<string> Collection { get; }

            public Dictionary<string, string> Dictionary { get; }
        }
    }
}
