using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class SpanDecorationSameTags : IAsyncRun
    {
        private const string When = @"{
    ""gt"": [
      {""ref"": ""intLocal""},
      {""ref"": ""intArg""}
    ]
}";

        private const string TagName = "SpanDecorationSameTags";

        private const string Decoration1 = @"{
      ""ref"": ""arg""
}";

        private const string Decoration2 = @"{
      ""ref"": ""intArg""
}";


        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            Console.WriteLine(await Method(nameof(RunAsync), nameof(RunAsync).GetHashCode()));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [SpanDecorationMethodProbeTestData(whenJson: When, decorationJson: new[] { Decoration1, Decoration2 }, decorationTagName: new[] { TagName, TagName })]
        async Task<string> Method(string arg, int intArg)
        {
            var intLocal = nameof(Method).GetHashCode();
            if (intLocal > intArg)
            {
                Console.WriteLine(intLocal);
                await Task.Delay(20);
            }

            return $"{arg} : {intLocal.ToString()}";
        }
    }
}
