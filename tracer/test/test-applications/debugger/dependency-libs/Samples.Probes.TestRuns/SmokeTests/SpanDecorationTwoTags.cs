using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class SpanDecorationTwoTags : IRun
    {
        private const string When = @"{
    ""gt"": [
      {""ref"": ""intLocal""},
      {""ref"": ""intArg""}
    ]
}";

        private const string TagName1 = "SpanDecorationTwoTags";

        private const string TagName2 = "SpanDecorationTwoTags2";

        private const string Decoration1 = @"{
      ""ref"": ""arg""
}";

        private const string Decoration2 = @"{
      ""ref"": ""intArg""
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
        [SpanDecorationMethodProbeTestData(whenJson: When, decorationJson: new[] { Decoration1, Decoration2 }, decorationTagName: new[] { TagName1, TagName2 })]
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
