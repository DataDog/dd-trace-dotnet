using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests;

[LineProbeTestData(lineNumber: 21)]
[LineProbeTestData(lineNumber: 22)]
[LineProbeTestData(lineNumber: 23)]
[LineProbeTestData(lineNumber: 24)]
[LineProbeTestData(lineNumber: 25)]
public class MultipleLineProbes : IRun
{
    public void Run()
    {
        MethodToInstrument(nameof(Run));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [MethodProbeTestData("System.Void", new[] { "System.String" })]
    public void MethodToInstrument(string callerName)
    {
        int a = callerName.Length;
        a++;
        a++;
        a++;
    }
}
