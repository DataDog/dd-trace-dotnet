using System.Runtime.CompilerServices;
using Samples.Probes.Shared;

namespace Samples.Probes.SmokeTests;

[LineProbeTestData(lineNumber: 14, unlisted: true, skipOnFramework: new []{ "net6.0" })]
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
