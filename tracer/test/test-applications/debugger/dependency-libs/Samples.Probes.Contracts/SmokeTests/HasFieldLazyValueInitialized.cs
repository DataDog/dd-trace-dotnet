// <copyright file="HasFieldLazyValueInitialized.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class HasFieldLazyValueInitialized : IRun
    {
#pragma warning disable SA1401 // Fields should be private
        public Lazy<string> FirstName = new Lazy<string>(new Func<string>(() => "First"));
#pragma warning restore SA1401 // Fields should be private

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            Method(FirstName.Value);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.String", new[] { "System.String" })]
        public string Method(string lastName)
        {
            return lastName;
        }
    }
}
