using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogOnLineProbeTestData(23)]
    [LogOnLineProbeTestData(24)]
    [LogOnLineProbeTestData(25)]
    [LogOnLineProbeTestData(26)]
    [LogOnLineProbeTestData(27)]
    [LogOnLineProbeTestData(35, expectedNumberOfSnapshots: 0)]
    [LogOnLineProbeTestData(44, expectedNumberOfSnapshots: 0)]
    [LogOnLineProbeTestData(50)]
    [LogOnLineProbeTestData(57, expectedNumberOfSnapshots: 0)]
    [LogOnLineProbeTestData(63)]
    public class NotSupportedFailureTest : IRun
    {
        public void Run()
        {
            var ret = new ByRefLikeType().IAmNotSupported();
            var ret2 = new NormalStruct().RetByRefLikeNotSupported(ret);
            var ret3 = new NormalStruct().IAmFine(ret);
            var ret4 = RetByRefLikeNotSupported(ret);
            var ret5 = IAmFine(ret);
        }

        public ref struct ByRefLikeType
        {
            [LogOnMethodProbeTestData(expectedNumberOfSnapshots: 0)]
            public string IAmNotSupported()
            {
                return nameof(IAmNotSupported);
            }
        }

        public struct NormalStruct
        {
            [LogOnMethodProbeTestData(expectedNumberOfSnapshots: 0)]
            public ByRefLikeType RetByRefLikeNotSupported(string str)
            {
                return default;
            }

            [LogOnMethodProbeTestData]
            public string IAmFine(string str)
            {
                return default;
            }
        }

        [LogOnMethodProbeTestData(expectedNumberOfSnapshots: 0)]
        public ByRefLikeType RetByRefLikeNotSupported(string str)
        {
            return default;
        }

        [LogOnMethodProbeTestData]
        public NormalStruct IAmFine(string str)
        {
            return default;
        }
    }
}
