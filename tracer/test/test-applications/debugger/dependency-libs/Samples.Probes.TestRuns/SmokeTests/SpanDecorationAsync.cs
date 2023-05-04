using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class SpanDecorationAsync : IAsyncRun
    {
        private const string When = @"{
    ""gt"": [
      {""ref"": ""intLocal""},
      {""ref"": ""intArg""}
    ]
}";

        private const string TagName = "SpanDecorationAsync";

        private const string Decoration = @"{
      ""ref"": ""arg""
}";


        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            Console.WriteLine(await Annotate(nameof(RunAsync), nameof(RunAsync).GetHashCode()));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        async Task<string> Annotate(string arg, int intArg)
        {
            return await Method(arg, intArg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [SpanDecorationMethodProbeTestData(skip:true, whenJson: When, decorationJson: new[] { Decoration }, decorationTagName: new[] { TagName })]
        async Task<string> Method(string arg, int intArg)
        {
            var intLocal = nameof(Method).GetHashCode() + arg.GetHashCode();
            if (intLocal > intArg)
            {
                Console.WriteLine(intLocal);
                await Task.Delay(20);
            }

            return $"{arg} : {intLocal.ToString()}";
        }
    }
}
