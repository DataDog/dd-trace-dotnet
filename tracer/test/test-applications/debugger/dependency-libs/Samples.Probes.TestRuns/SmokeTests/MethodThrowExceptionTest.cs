using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(26)]
    public class MethodThrowExceptionTest : IRun
    {
        public int Number { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(int.MaxValue);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData("System.String", new []{ "System.Int32" })]
        public string Method(int toSet)
        {
            Number += 7;
            var numberSnapshot = Number;
            Number = toSet;
            if (Number > numberSnapshot)
            {
                throw new InvalidOperationException($"Number {Number }is above snapshot value {numberSnapshot}");
            }

            return toSet.ToString();
        }
    }
}
