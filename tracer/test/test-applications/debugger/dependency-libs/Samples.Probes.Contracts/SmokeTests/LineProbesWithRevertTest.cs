// <copyright file="LineProbesWithRevertTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;

namespace Samples.Probes.Contracts.SmokeTests
{
    // Phase 1
    [LineProbeTestData(lineNumber: 46, phase: 1)]
    [LineProbeTestData(lineNumber: 60, phase: 1)]
    [LineProbeTestData(lineNumber: 61, phase: 1)]

// Phase 2
    [LineProbeTestData(lineNumber: 46, phase: 2)]
    [LineProbeTestData(lineNumber: 60, phase: 2)]
    [LineProbeTestData(lineNumber: 61, phase: 2)]
    [LineProbeTestData(lineNumber: 62, phase: 2)]
    [LineProbeTestData(lineNumber: 63, phase: 2)]

// Phase 3
    [LineProbeTestData(lineNumber: 51, phase: 3, expectedNumberOfSnapshots: 3)]
    [LineProbeTestData(lineNumber: 64, phase: 3)]

// Phase 4
    [LineProbeTestData(lineNumber: 60, phase: 4)]

// Phase 5
    [LineProbeTestData(lineNumber: 51, phase: 5, expectedNumberOfSnapshots: 3)]

// Phase 6
// Probe in unreachable branch should not emit any snapshot
    [LineProbeTestData(lineNumber: 57, phase: 6, expectedNumberOfSnapshots: 0)]
    public class LineProbesWithRevertTest : IRun
    {
        public void Run()
        {
            MethodToInstrument(nameof(Run));
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.Void", new[] { "System.String" }, phase: 1)]
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
}
