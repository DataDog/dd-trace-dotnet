using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class SpanDecorationArgsAndLocals : IRun
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
        [SpanOnMethodProbeTestData]
        public void Run()
        {
            Thread.Sleep(5000);
            Console.WriteLine(Method(nameof(Run), nameof(Run).GetHashCode()));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
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
