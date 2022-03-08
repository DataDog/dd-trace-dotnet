using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.SmokeTests
{
    internal class AsyncInstanceMethod : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task RunAsync()
        {
            await Method("Name");
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.Threading.Tasks.Task<System.String>", new[] { "System.String" })]
        public async Task<string> Method(string name)
        {
            char[] array = new char[name.Length];
            for (int i = 0; i < name.Length; i++)
            {
                array[i] = name[i];
            }

            await Task.Delay(250);
            return new string(array);
        }
    }
}
