// <copyright file="FlagEvaluationIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.FeatureFlags;

#if NETFRAMEWORK
[Collection(nameof(ManualInstrumentationTests))]
#endif
public class FlagEvaluationIntegrationTests : TestHelper
{
    private const string Subject = "evp-subject-canary@example.test";
    private const string SubjectHash = "sha256_0f0dd8be0ebb286af88e00cc7581e5afd2e3a0485ad97e8f7275dfa282c8b243";
    private const string AttributeCanary = "evp-private-attribute-canary";

    public FlagEvaluationIntegrationTests(ITestOutputHelper output)
        : base("OpenFeature", output)
    {
    }

    [SkippableTheory]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [Trait("RunOnWindows", "True")]
    public async Task InstrumentedEvaluationsPreserveResultsAndRespectPrivacy(bool consent, bool enabled, bool agentless)
    {
        using var agent = EnvironmentHelper.GetMockAgent();
        var configuration = CreateConfiguration(consent);
        using var cdn = agentless ? new FlagEvaluationConfigurationServer(Envelope(configuration)) : null;
        if (cdn is null)
        {
            agent.SetupRcm(Output, [((object)configuration, RcmProducts.FfeFlags, nameof(FlagEvaluationIntegrationTests))]);
        }
        else
        {
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessBaseUrl, cdn.Url + "configuration");
        }

        var payloads = new ConcurrentQueue<MockTracerAgent.EvpProxyPayload>();
        agent.EventPlatformProxyPayloadReceived += (_, args) =>
        {
            if (args.Value.PathAndQuery.EndsWith("/flagevaluation"))
            {
                payloads.Enqueue(args.Value);
            }
        };
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "0.1");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, agentless ? "agentless" : "remote_config");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FlaggingEvaluationCountsEnabled, enabled ? "true" : "false");
        SetEnvironmentVariable(ConfigurationKeys.ApiKey, "not-an-event-credential");
        using var telemetry = this.ConfigureTelemetry();
        using var process = await RunSampleAndWaitForExit(agent, arguments: "evp");
        process.StandardOutput.Should().Contain("<EVP: VALUES AND DEFAULTS OK>");
        process.StandardOutput.Should().Contain($"<EVP: HOOK BEFORE INITIALIZE {enabled}>");
        process.StandardOutput.Should().Contain("<EVP: FLUSHED>");

        if (!enabled)
        {
            payloads.Should().BeEmpty("the kill switch disables events, not evaluations");
            return;
        }

        payloads.Should().NotBeEmpty("the real native profiler must connect all provider stubs");
        foreach (var payload in payloads)
        {
            payload.PathAndQuery.Should().Be("/evp_proxy/v2/api/v2/flagevaluation");
            payload.Headers["Content-Encoding"].Should().Be("gzip");
            payload.Headers["Content-Type"].Should().Be("application/json");
            payload.Headers["X-Datadog-EVP-Subdomain"].Should().Be("event-platform-intake");
            payload.Headers["DD-EVP-ORIGIN"].Should().Be("dd-trace-dotnet");
            payload.Headers["DD-EVP-ORIGIN-VERSION"].Should().Be(TracerConstants.ThreePartVersion);
            payload.Headers["DD-API-KEY"].Should().BeNull();
            payload.Headers["Authorization"].Should().BeNull();
            // BodyInJson is the decompressed UTF-8 request body, before parsing/reserializing JSON.
            if (!consent)
            {
                payload.BodyInJson.Should().NotContain(Subject).And.NotContain(AttributeCanary);
            }
        }

        var rows = payloads.SelectMany(p => (JArray)JObject.Parse(p.BodyInJson)["flagEvaluations"]).ToList();
        rows.Sum(r => (long)r["evaluation_count"]).Should().Be(6);
        rows.Where(r => (string)r["flag"]["key"] == "simple-string" && r["error"] is null).Sum(r => (long)r["evaluation_count"]).Should().Be(3);
        rows.Should().Contain(r => (string)r["flag"]["key"] == "exposure-flag", "DoLog must not gate this event track");
        rows.Select(r => (string)r["error"]?["message"]).Should().Contain("FLAG_NOT_FOUND").And.Contain("TYPE_MISMATCH");
        foreach (var row in rows)
        {
            ((string)row["targeting_key"]).Should().Be(consent ? Subject : SubjectHash);
            if (consent)
            {
                ((string)row["context"]["evaluation"]["privateAttribute"]).Should().Be(AttributeCanary);
            }
            else
            {
                row["context"]?["evaluation"].Should().BeNull();
            }

            if (row["error"] is not null)
            {
                ((bool)row["runtime_default_used"]).Should().BeTrue();
            }
            else
            {
                row["runtime_default_used"].Should().BeNull();
            }
        }
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("RunOnWindows", "True")]
    public async Task ConfigurationChangeBeforeFlushPreservesEachEvaluationsConsent(bool initialConsent)
    {
        using var agent = EnvironmentHelper.GetMockAgent();
        using var cdn = new FlagEvaluationConfigurationServer(Envelope(CreateConfiguration(initialConsent)));
        var payloads = new ConcurrentQueue<string>();
        agent.EventPlatformProxyPayloadReceived += (_, args) =>
        {
            if (args.Value.PathAndQuery.EndsWith("/flagevaluation"))
            {
                payloads.Enqueue(args.Value.BodyInJson);
            }
        };
        var flushedBeforeSwap = false;
        cdn.AdvanceConfiguration = () =>
        {
            flushedBeforeSwap = !payloads.IsEmpty;
            cdn.SetResponse(Envelope(CreateConfiguration(!initialConsent)));
        };
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "agentless");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessBaseUrl, cdn.Url + "configuration");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessPollIntervalSeconds, "1");
        SetEnvironmentVariable("FFE_TEST_ADVANCE_URL", cdn.Url + "advance");
        using var telemetry = this.ConfigureTelemetry();
        using var process = await RunSampleAndWaitForExit(agent, arguments: "evp");
        process.StandardOutput.Should().Contain("<EVP: CONFIGURATION CHANGED>");
        process.StandardOutput.Should().Contain("<EVP: FLUSHED>");
        flushedBeforeSwap.Should().BeFalse("this test must replace configuration before the pending events are flushed");
        var rows = payloads.SelectMany(p => (JArray)JObject.Parse(p)["flagEvaluations"]).ToList();
        rows.Sum(r => (long)r["evaluation_count"]).Should().Be(7);
        var successful = rows.Where(r => (string)r["flag"]["key"] == "simple-string" && r["error"] is null).ToList();
        successful.Where(r => (string)r["targeting_key"] == Subject).Sum(r => (long)r["evaluation_count"]).Should().Be(initialConsent ? 3 : 1);
        successful.Where(r => (string)r["targeting_key"] == SubjectHash).Sum(r => (long)r["evaluation_count"]).Should().Be(initialConsent ? 1 : 3);
        foreach (var row in rows.Where(r => (string)r["targeting_key"] == SubjectHash))
        {
            row["context"]?["evaluation"].Should().BeNull();
        }
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task UnavailableAgentDoesNotChangeCdnEvaluationsOrIntroduceFallback()
    {
        // Delivery failure intentionally logs an error. Keep this test's expected errors
        // out of CI's CheckLogsForErrors scan, as in Telemetry_SendsRedactedErrorLogs.
        SetEnvironmentVariable(ConfigurationKeys.LogDirectory, Path.GetTempPath());
        using var agent = EnvironmentHelper.GetMockAgent();
        var configuration = CreateConfiguration(false);
        // Exercise flagevaluation delivery failure without unrelated trace/exposure senders
        // producing unrelated connection errors.
        foreach (var flag in configuration.Flags.ValidFlags)
        {
            foreach (var allocation in flag.Value.Allocations)
            {
                allocation.DoLog = false;
            }
        }

        using var cdn = new FlagEvaluationConfigurationServer(Envelope(configuration));
        SetEnvironmentVariable(ConfigurationKeys.TraceEnabled, "false");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FlaggingEvaluationCountsEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "agentless");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessBaseUrl, cdn.Url + "configuration");
        SetEnvironmentVariable("DD_TRACE_AGENT_URL", $"http://127.0.0.1:{TcpPortProvider.GetOpenPort()}");
        SetEnvironmentVariable(ConfigurationKeys.ApiKey, "not-an-event-credential");
        using var telemetry = this.ConfigureTelemetry();
        var elapsed = Stopwatch.StartNew();
        using var process = await RunSampleAndWaitForExit(agent, arguments: "evp");
        process.StandardOutput.Should().Contain("<EVP: VALUES AND DEFAULTS OK>").And.Contain("<EVP: FLUSHED>");
        elapsed.Elapsed.TotalSeconds.Should().BeLessThan(15, "a refused Agent must not block shutdown indefinitely");
        cdn.Requests.Should().NotBeEmpty().And.OnlyContain(path => path == "/configuration", "the configuration endpoint is not an event fallback");
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task EnablingEventsAfterStartupRequiresProviderRecreation()
    {
        using var agent = EnvironmentHelper.GetMockAgent();
        using var cdn = new FlagEvaluationConfigurationServer(Envelope(CreateConfiguration(false)));
        var payloads = new ConcurrentQueue<string>();
        agent.EventPlatformProxyPayloadReceived += (_, args) =>
        {
            if (args.Value.PathAndQuery.EndsWith("/flagevaluation"))
            {
                payloads.Enqueue(args.Value.BodyInJson);
            }
        };
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "agentless");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessBaseUrl, cdn.Url + "configuration");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FlaggingEvaluationCountsEnabled, "false");
        using var telemetry = this.ConfigureTelemetry();
        using var process = await RunSampleAndWaitForExit(agent, arguments: "evp-startup-gate");

        process.StandardOutput.Should().Contain("<EVP: PROVIDER RECREATION OK>");
        var rows = payloads.SelectMany(body => (JArray)JObject.Parse(body)["flagEvaluations"]).ToList();
        rows.Should().ContainSingle("only the replacement provider registers the event hook");
        ((long)rows.Single()["evaluation_count"]).Should().Be(1);
    }

    private static ServerConfiguration CreateConfiguration(bool consent) => new()
    {
        ObserveFullEvaluationData = consent,
        CreatedAt = consent ? "2026-09-30T00:00:01Z" : "2026-09-30T00:00:00Z",
        Format = "SERVER",
        Environment = new() { Name = "test" },
        Flags = FeatureFlagsHelpers.CreateAllFlags(),
    };

    private static string Envelope(ServerConfiguration configuration)
        => JsonHelper.SerializeObject(new { data = new { type = "universal-flag-configuration", attributes = configuration } });
}
