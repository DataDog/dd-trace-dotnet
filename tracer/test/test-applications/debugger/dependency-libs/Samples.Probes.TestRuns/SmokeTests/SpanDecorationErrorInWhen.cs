using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class SpanDecorationErrorInWhen : IRun
    {
        private const string When = @"{
    ""gt"": [
      {""ref"": ""error""},
      {""ref"": ""intArg""}
    ]
}";

        private const string TagName = "SpanDecorationErrorInWhen";

        private const string Decoration = @"{
      ""ref"": ""arg""
}";


        [MethodImpl(MethodImplOptions.NoInlining)]
        [SpanOnMethodProbeTestData]
        public void Run()
        {
            Console.WriteLine(Annotate(nameof(Run), nameof(Run).Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        string Annotate(string arg, int intArg)
        {
            return Method(arg, intArg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [SpanDecorationMethodProbeTestData(whenJson: When, decorationJson: new[] { Decoration }, decorationTagName: new[] { TagName })]
        string Method(string arg, int intArg)
        {
            var intLocal = nameof(Method).Length * 2;
            if (intLocal > intArg)
            {
                Console.WriteLine(intLocal);
            }

            return $"{arg} : {intLocal.ToString()}";
        }
    }
}
