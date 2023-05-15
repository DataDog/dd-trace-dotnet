using System.Linq;

namespace Samples.Probes.TestRuns.SmokeTests;

[LogLineProbeTestData(lineNumber: 10, unlisted: true)]
public class LambdaSingleLine : IRun
{
    public void Run()
    {
        var q = Enumerable.Range(1, 10).Where(i => i % 2 == 0).Select(f => f * f).ToList();
    }
}
