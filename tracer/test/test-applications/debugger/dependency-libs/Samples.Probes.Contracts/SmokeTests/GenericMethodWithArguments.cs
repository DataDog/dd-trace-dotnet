// <copyright file="GenericMethodWithArguments.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
using Samples.Probes.Contracts.Shared;

namespace Samples.Probes.Contracts.SmokeTests
{
    [LineProbeTestData(lineNumber: 27)]
    internal class GenericMethodWithArguments : IRun
    {
        public string Prop { get; } = nameof(GenericMethodWithArguments);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            var p = new Person("Alfred Hitchcock", 30, new Address { HomeType = BuildingType.Duplex, Number = 5, Street = "Elsewhere" }, System.Guid.NewGuid(), null);
            Method(p);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.String", new[] { "!!0" })]
        public string Method<T>(T genericParam)
        {
            return genericParam.ToString();
        }
    }
}
