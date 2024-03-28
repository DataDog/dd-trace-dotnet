using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class AsyncMethodWithNotHoistedLocals : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            await Foo(nameof(RunAsync));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData]
        public async Task<int> Foo(string value)
        {
            var result = value.Length > 10 ? "some value" : "some other value";
            var otherValue = value + $" - {DateTime.Now.ToShortDateString()}";
            Bar(result);
            Bar(otherValue);
            await Task.Delay(20);
            if (value.Length > 7)
            {
                return value.Length;
            }

            return value.Length - 4;
        }

        public void Bar(string result)
        {
            Console.WriteLine(result);
        }
    }
}
