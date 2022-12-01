using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.SmokeTests
{
    internal class AsyncStaticMethod : IAsyncRun
    {
        public const string ClassName = "AsyncStaticMethod";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            await Method(nameof(RunAsync));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MethodProbeTestData]
        public static async Task<string> Method(string input)
        {
            var output = $"{ClassName}.{input}.";
            await Task.Delay(20);
            return output + nameof(Method);
        }
    }
}
