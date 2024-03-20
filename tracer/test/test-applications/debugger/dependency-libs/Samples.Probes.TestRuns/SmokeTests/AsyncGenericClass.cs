using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class AsyncGenericClass : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            await new NestedAsyncGenericClass<Generic>().Method(new Generic { Message = "NestedAsyncGenericClass" }, $".{nameof(RunAsync)}");
        }

        internal class NestedAsyncGenericClass<T> where T : IGeneric
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            [LogMethodProbeTestData(expectedNumberOfSnapshots: 0 /*in optimize code this will create a nested struct inside generic parent*/, expectProbeStatusFailure: true)]
            public async Task<string> Method(T generic, string input)
            {
                var output = generic.Message + input + ".";
                await Task.Delay(20);
                return output + nameof(Method);
            }
        }
    }
}
