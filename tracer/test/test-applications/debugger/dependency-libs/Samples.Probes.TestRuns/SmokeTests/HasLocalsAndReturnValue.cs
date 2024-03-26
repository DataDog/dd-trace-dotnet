using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(16)]
    [LogLineProbeTestData(17)]
    [LogLineProbeTestData(25)]
    public class HasLocalsAndReturnValue : IRun
    {
        public int Number { get; set; } = 7;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            var result = Method(Number);
            Console.WriteLine(result);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData("System.String", new[] { "System.Int32" })]
        public string Method(int num)
        {
            var timeSpan = TimeSpan.FromSeconds(num);
            return timeSpan.ToString();
        }
    }
}
