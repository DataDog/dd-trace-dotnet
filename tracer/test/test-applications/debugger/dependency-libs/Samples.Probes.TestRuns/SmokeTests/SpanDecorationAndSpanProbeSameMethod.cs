using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class SpanDecorationAndSpanProbeSameMethod : IRun
    {
        private const string When = @"{
    ""gt"": [
      {""ref"": ""intLocal""},
      {""ref"": ""intArg""}
    ]
}";

        private const string TagName = "SpanDecorationArgsAndLocals";

        private const string Decoration = @"{
      ""ref"": ""arg""
}";


        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Console.WriteLine(Method(nameof(Run), nameof(Run).GetHashCode()));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [SpanOnMethodProbeTestData]
        [SpanDecorationMethodProbeTestData(whenJson: When, decorationJson: new[] { Decoration }, decorationTagName: new[] { TagName })]
        string Method(string arg, int intArg)
        {
            var intLocal = nameof(Method).GetHashCode();
            if (intLocal > intArg)
            {
                Console.WriteLine(intLocal);
            }

            return $"{arg} : {intLocal.ToString()}";
        }
    }
}
