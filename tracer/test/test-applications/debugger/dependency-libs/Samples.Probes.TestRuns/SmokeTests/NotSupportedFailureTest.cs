using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(23)]
    [LogLineProbeTestData(24)]
    [LogLineProbeTestData(25)]
    [LogLineProbeTestData(26)]
    [LogLineProbeTestData(27)]
    [LogLineProbeTestData(35, expectedNumberOfSnapshots: 0, expectProbeStatusFailure: true)]
    [LogLineProbeTestData(44, expectedNumberOfSnapshots: 0, expectProbeStatusFailure: true)]
    [LogLineProbeTestData(50)]
    [LogLineProbeTestData(57, expectedNumberOfSnapshots: 0, expectProbeStatusFailure: true)]
    [LogLineProbeTestData(63)]
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
            [LogMethodProbeTestData(expectedNumberOfSnapshots: 0, expectProbeStatusFailure: true)]
            public string IAmNotSupported()
            {
                return nameof(IAmNotSupported);
            }
        }

        public struct NormalStruct
        {
            [LogMethodProbeTestData(expectedNumberOfSnapshots: 0, expectProbeStatusFailure: true)]
            public ByRefLikeType RetByRefLikeNotSupported(string str)
            {
                return default;
            }

            [LogMethodProbeTestData]
            public string IAmFine(string str)
            {
                return default;
            }
        }

        [LogMethodProbeTestData(expectedNumberOfSnapshots: 0, expectProbeStatusFailure: true)]
        public ByRefLikeType RetByRefLikeNotSupported(string str)
        {
            return default;
        }

        [LogMethodProbeTestData]
        public NormalStruct IAmFine(string str)
        {
            return default;
        }
    }
}
