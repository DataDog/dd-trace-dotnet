using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Samples.Probes.Shared;

namespace Samples.Probes.SmokeTests
{
    internal class GenericRefReturnTest : IRun
    {
        public void Run()
        {
            CallMe();
        }

        [MethodProbeTestData]
        internal void CallMe()
        {
            ref var whatever = ref Enqueue<Address>();
        }

        [MethodProbeTestData(expectedNumberOfSnapshots: 0)]
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
