using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests;

[LogLineProbeTestData(lineNumber: 13, unlisted: true)]
public class Emit100LineProbeSnapshotsTest : IRun
{
    public void Run()
    {
        var accu = 0;
        for (int i = 0; i < 100; i++)
        {
            accu += i;
        }

        if (accu > 0)
        {
            throw new IntentionalDebuggerException($"accu is {accu}");
        }
    }
}
