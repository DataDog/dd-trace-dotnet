// <copyright file="StaticVoidMethod.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class StaticVoidMethod : IRun
    {
        public static int Number { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.Void", new string[0])]
        public static void Method()
        {
            Number = 7;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            Method();
        }
    }
}
