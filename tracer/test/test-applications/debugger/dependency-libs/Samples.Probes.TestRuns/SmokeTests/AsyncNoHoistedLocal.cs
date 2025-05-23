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
        [LogMethodProbeTestData(skipOnFrameworks: ["net5.0", "net48", "net462", "netcoreapp3.1", "netcoreapp3.0", "netcoreapp2.1"])]
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
