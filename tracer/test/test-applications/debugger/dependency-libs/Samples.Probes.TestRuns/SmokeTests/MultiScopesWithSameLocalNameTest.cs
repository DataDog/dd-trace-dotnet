using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests;

/// <summary>
/// This test yields wrongful snapshots. We have two locals, with same names but in different scopes (`i` and `localInFor`).
/// As we currently are not taking into account scoping when we resolve local index -> name, 
/// we might capture the wrong local at the wrong location.
/// In this example, we place a line probe on line 25 (first for loop), expected to see the value of the locals 
/// at this location but we accidently capture the `i` and `localInFor` that are in the scope of the second for loop (as they override the `i` and `localInFor` of the first loop, because of the same naming)
/// thus they will see the value '0' in the snapshots instead of having their real value.
/// </summary>
[LogLineProbeTestData(lineNumber: 25, expectedNumberOfSnapshots: 2)]
[LogLineProbeTestData(lineNumber: 32, expectedNumberOfSnapshots: 2)]
public class MultiScopesWithSameLocalNameTest : IRun
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Run()
    {
        for (int i = 0; i < 2; i++)
        {
            int localInFor = i + 2;
            Mutate(ref localInFor);
            Console.WriteLine(localInFor);
        }

        for (int i = 0; i < 2; i++)
        {
            int localInFor = i + 4;
            Mutate(ref localInFor);
            Console.WriteLine(localInFor);
        }
    }

    void Mutate(ref int @out)
    {
        @out += 5;
    }
}
