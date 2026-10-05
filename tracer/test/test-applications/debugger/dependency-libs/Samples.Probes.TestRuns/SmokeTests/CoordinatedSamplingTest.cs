using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace;

namespace Samples.Probes.TestRuns.SmokeTests;

[LogLineProbeTestData(lineNumber: 45, unlisted: true)]
[LogLineProbeTestData(lineNumber: 49, unlisted: true)]
public class CoordinatedSamplingTest : IRun
{
    public void Run()
    {
        using var scope = Tracer.Instance.StartActive("coordinated-sampling");
        Correlation();
        CorrelationLoop();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [LogMethodProbeTestData(unlisted: true)]
    public void Correlation()
    {
        CorrelationMiddle();
        Thread.Sleep(200);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [LogMethodProbeTestData(unlisted: true)]
    public void CorrelationMiddle()
    {
        CorrelationLeaf();
        Thread.Sleep(200);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [LogMethodProbeTestData(unlisted: true)]
    public void CorrelationLeaf()
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void CorrelationLoop()
    {
        for (var i = 0; i < 3; i++)
        {
            var loopValue = i + 1;
            Thread.Sleep(loopValue);
        }

        var siblingValue = 42;
        Thread.Sleep(siblingValue == 42 ? 1 : 0);
    }
}
