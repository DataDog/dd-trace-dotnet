using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests;

[LogLineProbeTestData(lineNumber: 21)]
[LogLineProbeTestData(lineNumber: 22)]
[LogLineProbeTestData(lineNumber: 23)]
[LogLineProbeTestData(lineNumber: 24)]
[LogLineProbeTestData(lineNumber: 25)]
public class MultipleLineProbes : IRun
{
    public void Run()
    {
        MethodToInstrument(nameof(Run));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [LogMethodProbeTestData("System.Void", new[] { "System.String" })]
    public void MethodToInstrument(string callerName)
    {
        int a = callerName.Length;
        a++;
        a++;
        a++;
    }
}
