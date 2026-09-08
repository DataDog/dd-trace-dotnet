// <copyright file="TestOptimizationShutdownTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET8_0_OR_GREATER || NETFRAMEWORK
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Trait("Category", "EndToEnd")]
[Trait("Category", "TestIntegrations")]
[Trait("RunOnWindows", "True")]
public class TestOptimizationShutdownTests(ITestOutputHelper output) : TestingFrameworkEvpTest("TestOptimizationShutdown", output)
{
    [Theory]
    [InlineData("explicit-close")]
    [InlineData("process-exit")]
    [InlineData("exception")]
    public async Task OpenSessionIsSentBeforeTheWriterCloses(string shutdownTrigger)
    {
        EnvironmentHelper.EnableDefaultTransport();
        SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", "0");
        SetEnvironmentVariable("COR_ENABLE_PROFILING", "0");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        // Exercise agent discovery during initialization instead of bypassing it with a forced EVP mode.
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy, null);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.IntelligentTestRunnerEnabled, "0");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.GitUploadEnabled, "0");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverage, "0");

        var sessions = new ConcurrentQueue<JObject>();
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.EventPlatformProxyPayloadReceived += (_, e) =>
        {
            if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                foreach (var testEvent in payload.Events.Where(testEvent => testEvent.Type == SpanTypes.TestSession))
                {
                    sessions.Enqueue(JObject.Parse(testEvent.Content.ToString()));
                }
            }
        };

        var tracerPath = Path.Combine(EnvironmentHelper.GetMonitoringHomePath(), EnvironmentHelper.IsCoreClr() ? "net6.0" : "net461", "Datadog.Trace.dll");
        using var result = await RunSampleAndWaitForExit(agent, $"\"{tracerPath}\" {shutdownTrigger}");

        sessions.Should().ContainSingle();
        var session = sessions.Single();
        session.Value<ulong>("duration").Should().BeGreaterThan(0);
        session.Value<int>("error").Should().Be(shutdownTrigger == "exception" ? 1 : 0);
        session["meta"].Value<string>("test.command").Should().Be("shutdown regression");
    }
}
#endif
