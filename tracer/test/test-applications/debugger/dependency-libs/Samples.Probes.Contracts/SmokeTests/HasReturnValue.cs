// <copyright file="HasReturnValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class HasReturnValue : IRun
    {
        public int Number { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            Method();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.String", new string[0])]
        public string Method()
        {
            Number = 7;
            return Number.ToString();
        }
    }
}
