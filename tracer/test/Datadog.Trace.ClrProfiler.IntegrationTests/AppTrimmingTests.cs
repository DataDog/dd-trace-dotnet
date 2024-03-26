// <copyright file="AppTrimmingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

#if NET6_0_OR_GREATER

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

public class AppTrimmingTests : TestHelper
{
    public AppTrimmingTests(ITestOutputHelper output)
        : base("Trimming", output)
    {
        SetServiceVersion("1.0.0");
    }

    [Fact]
    public async Task TrimmerTest()
    {
        var httpPort = TcpPortProvider.GetOpenPort();
        Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

        using var agent = EnvironmentHelper.GetMockAgent();
        var aspnetPort = TcpPortProvider.GetOpenPort();
        using var processResult = await RunSampleAndWaitForExit(agent, aspNetCorePort: aspnetPort, usePublishWithRID: true);
        var spans = agent.WaitForSpans(30);

        // Target app does 10 request, so it generates 30 spans (Http Request + AspNetCore + AspNetCore.Mvc)
        spans.Where(s => s.Name == "http.request").Should().HaveCount(10);
        spans.Where(s => s.Name == "aspnet_core.request").Should().HaveCount(10);
        spans.Where(s => s.Name == "aspnet_core_mvc.request").Should().HaveCount(10);
    }
}

#endif
