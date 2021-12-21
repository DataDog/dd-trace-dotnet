// <copyright file="PublicApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class PublicApiTests : PublicApiTestsBase
    {
        public PublicApiTests()
            : base(typeof(Tracer).Assembly)
        {
            // When building Datadog.Trace for netcoreapp3.1, there are differences based on build platform.
            // Specifically, non-Windows builds add an additional netstandard2.0 assembly reference
            string frameworkName = EnvironmentTools.GetTracerTargetFrameworkDirectory();
            if (frameworkName == "netcoreapp3.1")
            {
                AssemblyReferenceSnapshotPlatform = EnvironmentTools.IsWindows() ? "Windows" : "NonWindows";
            }
        }

#if NETFRAMEWORK
        [Fact]
        public void ExposesTracingHttpModule()
        {
            var httpModuleType = Type.GetType("Datadog.Trace.AspNet.TracingHttpModule, Datadog.Trace.AspNet");
            Assert.NotNull(httpModuleType);
        }
#endif
    }
}
