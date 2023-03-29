using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests;

// Phase 1
[LogOnLineProbeTestData(lineNumber: 41, phase: 1)]
[LogOnLineProbeTestData(lineNumber: 55, phase: 1)]
[LogOnLineProbeTestData(lineNumber: 56, phase: 1)]

// Phase 2
[LogOnLineProbeTestData(lineNumber: 41, phase: 2)]
[LogOnLineProbeTestData(lineNumber: 55, phase: 2)]
[LogOnLineProbeTestData(lineNumber: 56, phase: 2)]
[LogOnLineProbeTestData(lineNumber: 57, phase: 2)]
[LogOnLineProbeTestData(lineNumber: 58, phase: 2)]

// Phase 3
[LogOnLineProbeTestData(lineNumber: 46, phase: 3, expectedNumberOfSnapshots: 3)]
[LogOnLineProbeTestData(lineNumber: 59, phase: 3)]

// Phase 4
[LogOnLineProbeTestData(lineNumber: 55, phase: 4)]

// Phase 5
[LogOnLineProbeTestData(lineNumber: 46, phase: 5, expectedNumberOfSnapshots: 3)]

// Phase 6
// Probe in unreachable branch should not emit any snapshot
[LogOnLineProbeTestData(lineNumber: 52, phase: 6, expectedNumberOfSnapshots: 0)]
public class LineProbesWithRevertTest : IRun
{
    public void Run()
    {
        MethodToInstrument(nameof(Run));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [LogOnMethodProbeTestData("System.Void", new[] { "System.String" }, phase: 1)]
    public void MethodToInstrument(string callerName)
    {
        int a = callerName.Length;

        int sum = a;
        for (int i = 2; i < a + 2; i++)
        {
            sum *= i;
        }

        if (sum < 10)
        {
            // Unreachable branch
            sum = 0;
        }

        a++;
        a++;
        a++;
        a++;
        a++;
    }
}
