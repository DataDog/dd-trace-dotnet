// <copyright file="HasLocalsAndReturnValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.Contracts.SmokeTests
{
    [LineProbeTestData(21)]
    [LineProbeTestData(22)]
    [LineProbeTestData(30)]
    internal class HasLocalsAndReturnValue : IRun
    {
        public int Number { get; set; } = 7;

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            var result = Method(Number);
            Console.WriteLine(result);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.String", new[] { "System.Int32" })]
        public string Method(int num)
        {
            var timeSpan = TimeSpan.FromSeconds(num);
            return timeSpan.ToString();
        }
    }
}
