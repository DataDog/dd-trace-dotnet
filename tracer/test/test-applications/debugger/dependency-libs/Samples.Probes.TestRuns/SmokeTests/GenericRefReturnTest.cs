using System;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class GenericRefReturnTest : IRun
    {
        public void Run()
        {
            CallMe();
        }

        [LogMethodProbeTestData]
        internal void CallMe()
        {
            ref var whatever = ref Enqueue<Address>();
            if (whatever.City.Name.Length > 1 && whatever.ToString().Length > 1)
            {
                Console.WriteLine(whatever.Number);
            }
            else
            {
                Console.WriteLine(whatever);
            }
        }

        [LogMethodProbeTestData(expectedNumberOfSnapshots: 0, expectProbeStatusFailure: true)]
        internal static ref T Enqueue<T>()
        {
            return ref new GenericRefReturnTest.DefaultValueContainer<T>().Value;
        }

        private class DefaultValueContainer<T>
        {
            public T Value;
        }
    }
}
