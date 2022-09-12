// <copyright file="StaticMethodWithArguments.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;

namespace Samples.Probes.Contracts.SmokeTests
{
    [LineProbeTestData(18)]
    internal class StaticMethodWithArguments : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.String", new[] { "System.String" })]
        public static string Method(string lastName)
        {
            return lastName;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            Method("Last");
        }
    }
}
