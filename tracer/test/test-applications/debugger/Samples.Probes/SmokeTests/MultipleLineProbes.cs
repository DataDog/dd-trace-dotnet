using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests;

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

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    [MethodProbeTestData("System.Void", new[] { "System.String" }, skip: true /* Will be returned in the next PR - fix an issue when putting method probe and line probe one same method */)]
    public void MethodToInstrument(string callerName)
    {
        int a = callerName.Length;
        a++;
        a++;
        a++;
    }
}
