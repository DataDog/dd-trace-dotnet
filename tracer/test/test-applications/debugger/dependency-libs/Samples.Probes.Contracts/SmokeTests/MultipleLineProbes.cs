// <copyright file="MultipleLineProbes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;

namespace Samples.Probes.Contracts.SmokeTests
{
    [LineProbeTestData(lineNumber: 26)]
    [LineProbeTestData(lineNumber: 27)]
    [LineProbeTestData(lineNumber: 28)]
    [LineProbeTestData(lineNumber: 29)]
    [LineProbeTestData(lineNumber: 30)]
    public class MultipleLineProbes : IRun
    {
        public void Run()
        {
            MethodToInstrument(nameof(Run));
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.Void", new[] { "System.String" })]
        public void MethodToInstrument(string callerName)
        {
            int a = callerName.Length;
            a++;
            a++;
            a++;
        }
    }
}
