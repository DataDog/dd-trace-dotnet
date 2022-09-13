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

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task RunAsync()
        {
            await Method(ClassName, $".{nameof(RunAsync)}");
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData(skip: true)]
        public async Task<string> Method<T>(T obj, string input)
        {
            var output = obj + input + ".";
            await Task.Delay(20);
            return output + nameof(Method);
        }
    }
}
