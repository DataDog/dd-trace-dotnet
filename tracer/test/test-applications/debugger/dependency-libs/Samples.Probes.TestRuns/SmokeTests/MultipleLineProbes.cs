using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests;

[LogOnLineProbeTestData(lineNumber: 21)]
[LogOnLineProbeTestData(lineNumber: 22)]
[LogOnLineProbeTestData(lineNumber: 23)]
[LogOnLineProbeTestData(lineNumber: 24)]
[LogOnLineProbeTestData(lineNumber: 25)]
public class MultipleLineProbes : IRun
{
    public void Run()
    {
        MethodToInstrument(nameof(Run));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [LogOnMethodProbeTestData("System.Void", new[] { "System.String" })]
    public void MethodToInstrument(string callerName)
    {
        int a = callerName.Length;
        a++;
        a++;
        a++;
    }
}
