// <copyright file="AppTrimmingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

#if NET7_0

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

public class AppTrimmingTests : TestHelper
{
    public AppTrimmingTests(ITestOutputHelper output)
        : base("Trimming", output)
    {
        SetServiceVersion("1.0.0");
    }

    [Fact]
    public void TrimmerTest()
    {
        var httpPort = TcpPortProvider.GetOpenPort();
        Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

        using var agent = EnvironmentHelper.GetMockAgent();
        var aspnetPort = TcpPortProvider.GetOpenPort();
        using var processResult = RunSampleAndWaitForExit(agent, aspNetCorePort: aspnetPort, usePublishWithRID: true);
        var spans = agent.WaitForSpans(30);

        // Target app does 10 request, so it generates 30 spans (Http Request + AspNetCore + AspNetCore.Mvc)
        Assert.Equal(10, spans.Count(s => s.Name == "http.request"));
        Assert.Equal(10, spans.Count(s => s.Name == "aspnet_core.request"));
        Assert.Equal(10, spans.Count(s => s.Name == "aspnet_core_mvc.request"));
    }
}

#endif
