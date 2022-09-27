using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.SmokeTests
{
    [LineProbeTestData(lineNumber: 21, skip: true)]
    public class AsyncInstanceMethod : IAsyncRun
    {
        private const string ClassName = "AsyncInstanceMethod";

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task RunAsync()
        {
            await Method($"{ClassName}.{nameof(RunAsync)}");
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData(skip: true)]
        public async Task<string> Method(string input)
        {
            var output = input + ".";
            await Task.Delay(20);
            return output + nameof(Method);
        }
    }
}
