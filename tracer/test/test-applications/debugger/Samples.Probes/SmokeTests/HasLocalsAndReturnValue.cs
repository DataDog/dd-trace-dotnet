using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests
{
    [LineProbeTestData(16, skip: true /* Line probes are broken in some cases, will fix ASAP*/)]
    [LineProbeTestData(17, skip: true /* Line probes are broken in some cases, will fix ASAP*/)]
    [LineProbeTestData(25, skip: true /* Line probes are broken in some cases, will fix ASAP*/)]
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
        [MethodProbeTestData("System.String", new[] { "System.Int32" }, skip: true /* Will be returned in the next PR - fix an issue when putting method probe and line probe one same method */ )]
        public string Method(int num)
        {
            var timeSpan = TimeSpan.FromSeconds(num);
            return timeSpan.ToString();
        }
    }
}
