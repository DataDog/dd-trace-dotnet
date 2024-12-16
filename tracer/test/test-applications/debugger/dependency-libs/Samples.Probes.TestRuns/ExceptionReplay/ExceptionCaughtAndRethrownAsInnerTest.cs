using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.ExceptionReplay
{
    [ExceptionReplayTestData(expectedNumberOfSnapshotsDefault: 4, expectedNumberOfSnaphotsFull: 4)]
    internal class ExceptionCaughtAndRethrownAsInnerTest : IRun
    {
        public void Run()
        {
            try
            {
                throw new Exception("My future is unknown");
            }
            catch (Exception e)
            {
                throw new ExceptionReplayIntentionalException(e.Message, innerException: e);
            }
        }
    }
}
