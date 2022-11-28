using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests;

[LineProbeTestData(lineNumber: 21, skip: true /* Line probes are broken in some cases, will fix ASAP*/)]
[LineProbeTestData(lineNumber: 22, skip: true /* Line probes are broken in some cases, will fix ASAP*/)]
[LineProbeTestData(lineNumber: 23, skip: true /* Line probes are broken in some cases, will fix ASAP*/)]
[LineProbeTestData(lineNumber: 24, skip: true /* Line probes are broken in some cases, will fix ASAP*/)]
[LineProbeTestData(lineNumber: 25, skip: true /* Line probes are broken in some cases, will fix ASAP*/)]
public class MultipleLineProbes : IRun
{
    public void Run()
    {
        MethodToInstrument(nameof(Run));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [MethodProbeTestData("System.Void", new[] { "System.String" }, skip: true /* Will be returned in the next PR - fix an issue when putting method probe and line probe one same method */)]
    public void MethodToInstrument(string callerName)
    {
        int a = callerName.Length;
        a++;
        a++;
        a++;
    }
}
