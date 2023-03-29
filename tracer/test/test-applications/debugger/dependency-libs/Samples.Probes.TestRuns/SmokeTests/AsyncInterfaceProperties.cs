using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(lineNumber: 34)]
    public class AsyncInterfaceProperties : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            var implementInterface = new Class { DoNotShowMe = "bla bla", ShowMe = "Show Me!" };
            await Method(implementInterface);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData]
        public async Task<string> Method(IInterface parameter)
        {
            Console.WriteLine($"{parameter.ShowMe}, {parameter.DoNotShowMe}");

            await Task.Delay(20);
            IInterface iInterface = new Class { ShowMe = string.Empty };
            Console.WriteLine(iInterface.ShowMe);
            await Task.Yield();

            if (Check(iInterface))
            {
                return iInterface.DoNotShowMe;
            }

            return parameter.ShowMe;
        }

        private bool Check(IInterface iInterface)
        {
            return iInterface.ShowMe.Length == ToString()!.Length;
        }
    }
}
