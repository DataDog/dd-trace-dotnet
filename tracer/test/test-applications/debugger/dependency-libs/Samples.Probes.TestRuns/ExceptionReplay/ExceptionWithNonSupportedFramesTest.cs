using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.ExceptionReplay
{
    [ExceptionReplayTestData(expectedNumberOfSnapshotsDefault: 4, expectedNumberOfSnaphotsFull: 5)]
    internal class ExceptionWithNonSupportedFramesTest : IRun
    {
        public void Run()
        {
            NotSupportedFrame(nameof(ExceptionWithNonSupportedFramesTest));
        }

        ref MeRefStruct NotSupportedFrame(string str)
        {
            SupportedFrame(str);
            throw new Exception();
        }

        void SupportedFrame(string str)
        {
            throw new InvalidOperationException(str);
        }

        ref struct MeRefStruct
        {
#pragma warning disable CS0649 // Field 'ExceptionWithNonSupportedFramesTest.MeRefStruct.Field' is never assigned to, and will always have its default value null
            public string Field;
#pragma warning restore CS0649
        }
    }
}
