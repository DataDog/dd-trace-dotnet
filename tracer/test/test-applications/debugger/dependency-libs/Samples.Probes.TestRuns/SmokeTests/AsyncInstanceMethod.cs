using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogOnLineProbeTestData(lineNumber: 21)]
    public class AsyncInstanceMethod : IAsyncRun
    {
        private const string ClassName = "AsyncInstanceMethod";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            await Method($"{ClassName}.{nameof(RunAsync)}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogOnMethodProbeTestData]
        public async Task<string> Method(string input)
        {
            var output = input + ".";
            await Task.Delay(20);
            return output + nameof(Method);
        }
    }
}
