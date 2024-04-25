// <copyright file="WithTracerAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Xunit
{
    // Make it a trait too so we can filter it on the command-line
    [TraitDiscoverer("Datadog.Profiler.IntegrationTests.Xunit.WithTracerTraitDiscoverer", "Datadog.Profiler.IntegrationTests")]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    internal class WithTracerAttribute : Attribute, ITraitAttribute
    {
        public WithTracerAttribute()
        {
        }
    }
}
