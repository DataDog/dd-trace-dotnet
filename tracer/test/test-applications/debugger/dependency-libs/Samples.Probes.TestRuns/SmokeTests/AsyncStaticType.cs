using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(lineNumber: 28)]
    public class AsyncStaticType : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            await StaticTypeInner.Method("Last name");
        }

        public static class StaticTypeInner
        {
            public static string _staticField = "Static Field";
            public static string StaticProperty { get; } = "Static Property";

            [MethodImpl(MethodImplOptions.NoInlining)]
            [LogMethodProbeTestData("System.String", new[] { "System.String" })]
            public static async Task<string> Method(string lastName)
            {
                await Task.Yield();
                await Task.Yield();
                await Task.Yield();
                return lastName;
            }
        }
    }
}
