using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LineProbeTestData(23)]
    [LineProbeTestData(24)]
    [LineProbeTestData(25)]
    [LineProbeTestData(26)]
    [LineProbeTestData(27)]
    [LineProbeTestData(35, expectedNumberOfSnapshots: 0)]
    [LineProbeTestData(44, expectedNumberOfSnapshots: 0)]
    [LineProbeTestData(50)]
    [LineProbeTestData(57, expectedNumberOfSnapshots: 0)]
    [LineProbeTestData(63)]
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
            [MethodProbeTestData(expectedNumberOfSnapshots: 0)]
            public string IAmNotSupported()
            {
                return nameof(IAmNotSupported);
            }
        }

        public struct NormalStruct
        {
            [MethodProbeTestData(expectedNumberOfSnapshots: 0)]
            public ByRefLikeType RetByRefLikeNotSupported(string str)
            {
                return default;
            }

            [MethodProbeTestData]
            public string IAmFine(string str)
            {
                return default;
            }
        }

        [MethodProbeTestData(expectedNumberOfSnapshots: 0)]
        public ByRefLikeType RetByRefLikeNotSupported(string str)
        {
            return default;
        }

        [MethodProbeTestData]
        public NormalStruct IAmFine(string str)
        {
            return default;
        }
    }
}
