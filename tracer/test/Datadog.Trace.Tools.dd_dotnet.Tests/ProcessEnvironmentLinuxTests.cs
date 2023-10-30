// <copyright file="ProcessEnvironmentLinuxTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Tools.Shared.Linux;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.dd_dotnet.Tests
{
    public class ProcessEnvironmentLinuxTests
    {
        [Fact]
        public void FindDatadogTraceModule()
        {
            var modules = ProcessEnvironmentLinux.ReadModulesFromFile("maps.txt");

            modules.Should().Contain("/project/tracer/bin/tracer-home/netcoreapp3.1/Datadog.Trace.dll");
        }
    }
}
