using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.SmokeTests
{
    internal class AsyncGenericMethod : IAsyncRun
    {
        private const string ClassName = "AsyncWithGenericMethod";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            await Method(ClassName, $".{nameof(RunAsync)}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MethodProbeTestData (expectedNumberOfSnapshots:0 /* in optimize code this will create a generic struct state machine*/)]
        public async Task<string> Method<T>(T obj, string input)
        {
            var output = obj + input + ".";
            await Task.Delay(20);
            return output + nameof(Method);
        }
    }
}
