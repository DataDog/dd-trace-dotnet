using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests
{
    [LineProbeTestData(16)]
    [LineProbeTestData(17)]
    [LineProbeTestData(25)]
    internal class HasLocalsAndReturnValue : IRun
    {
        public int Number { get; set; } = 7;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            var result = Method(Number);
            Console.WriteLine(result);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MethodProbeTestData("System.String", new[] { "System.Int32" })]
        public string Method(int num)
        {
            var timeSpan = TimeSpan.FromSeconds(num);
            return timeSpan.ToString();
        }
    }
}
