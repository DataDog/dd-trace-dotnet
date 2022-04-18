using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests;

[LineProbeTestData(lineNumber: 17, expectedNumberOfSnapshots: 2)]
[LineProbeTestData(lineNumber: 24, expectedNumberOfSnapshots: 2)]
public class MultiScopesWithSameLocalNameTest : IRun
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
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
