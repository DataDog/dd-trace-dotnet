using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class AsyncGenericStruct : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            await new NestedAsyncGenericStruct<Generic>().Method(new Generic { Message = "NestedAsyncGenericStruct" }, $".{nameof(RunAsync)}");
        }

        internal struct NestedAsyncGenericStruct<T> where T : IGeneric
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            [LogMethodProbeTestData(expectedNumberOfSnapshots: 0 /* in optimize code this will create a generic struct state machine*/, expectProbeStatusFailure: true)]
            public async Task<string> Method(T generic, string input)
            {
                var output = generic.Message + input + ".";
                await Task.Delay(20);
                return output + nameof(Method);
            }
        }
    }
}
