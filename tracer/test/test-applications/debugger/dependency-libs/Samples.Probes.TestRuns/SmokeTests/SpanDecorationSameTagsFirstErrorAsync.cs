using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class SpanDecorationSameTagsFirstErrorAsync : IAsyncRun
    {
        private const string When = @"{
    ""gt"": [
      {""ref"": ""intLocal""},
      {""ref"": ""intArg""}
    ]
}";

        private const string TagName = "SpanDecorationSameTagsFirstErrorAsync";

        private const string Decoration = @"{
      ""ref"": ""arg""
}";

        private const string ErrorDecoration = @"{
      ""ref"": ""error""
}";


        [MethodImpl(MethodImplOptions.NoInlining)]
        [SpanOnMethodProbeTestData(skip:true)]
        public async Task RunAsync()
        {
            Console.WriteLine(await Method(nameof(RunAsync), nameof(RunAsync).Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [SpanDecorationMethodProbeTestData(skip:true, whenJson: When, decorationJson: new[] { ErrorDecoration, Decoration }, decorationTagName: new[] { TagName, TagName })]
        async Task<string> Method(string arg, int intArg)
        {
            var intLocal = nameof(Method).Length * 2;
            if (intLocal > intArg)
            {
                Console.WriteLine(intLocal);
                await Task.Delay(20);
            }

            return $"{arg} : {intLocal.ToString()}";
        }
    }
}
