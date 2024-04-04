using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class AsyncNoHoistedLocal : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            await Method();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData]
        public async Task<string> Method()
        {
            var input = nameof(Method) + "f";
            var iii = M(input);
            var rrr = M(iii) + input;
            var sss = M(rrr);
            await Task.Delay(20);
            return nameof(Method);
        }

        private object M(object input)
        {
            return input;
        }
    }
}
