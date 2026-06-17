// <copyright file="SpanEnrichmentIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.FeatureFlags;

#if NETFRAMEWORK
[Collection(nameof(ManualInstrumentationTests))]
#endif
public class SpanEnrichmentIntegrationTests : TestHelper
{
    // Frozen cross-SDK contract tag names (dd-trace-js#8343) — bare names, never _dd.-prefixed.
    // Hard-coded here (rather than referencing the internal SpanEnrichmentState constants) so the
    // integration test asserts the exact wire names the backend/system-tests depend on.
    private const string TagFlagsEnc = "ffe_flags_enc";
    private const string TagSubjectsEnc = "ffe_subjects_enc";
    private const string TagRuntimeDefaults = "ffe_runtime_defaults";

    public SpanEnrichmentIntegrationTests(ITestOutputHelper output)
        : base("OpenFeature", output)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task SpanEnrichment_GateOn_WritesFfeTagsOnRootSpanOnly()
    {
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.SetupRcm(
            Output,
            [
                ((object)new ServerConfiguration
                {
                    Flags = FeatureFlagsHelpers.CreateAllFlags(),
                },
                RcmProducts.FfeFlags,
                nameof(SpanEnrichmentIntegrationTests))
            ]);

        var output = await RunTest(agent, spanEnrichmentEnabled: true);
        Assert.Contains("<INSTRUMENTED>", output);
        Assert.Contains("Exit. OK", output);

        // The sample wraps flag evaluation in "ffe.root" and a child "ffe.child" (including an eval
        // after an await). Wait for both spans.
        var spans = await agent.WaitForSpansAsync(2, operationName: "ffe.root", returnAllOperations: true);

        using var scope = new AssertionScope();

        var root = spans.SingleOrDefault(s => s.Name == "ffe.root");
        root.Should().NotBeNull("the root enrichment span must be flushed to the agent");

        var child = spans.SingleOrDefault(s => s.Name == "ffe.child");
        child.Should().NotBeNull("the child span must be flushed to the agent");

        // 1) Root span carries the encoded flag serial ids. The sample evaluates simple-string
        // (serial 100) and exposure-flag (serial 108) directly in the root, plus rule/numeric evals
        // inside the child + across the await that must aggregate onto THIS root.
        root!.Tags.Should().ContainKey(TagFlagsEnc, "the root span must carry the encoded flag serial ids");
        var decoded = DecodeDeltaVarint(root.Tags[TagFlagsEnc]);
        decoded.Should().Contain(FeatureFlagsHelpers.SimpleStringSerialId, "simple-string's serial id must aggregate onto the root");
        decoded.Should().Contain(FeatureFlagsHelpers.ExposureSerialId, "exposure-flag's serial id must aggregate onto the root");

        // 2) exposure-flag authorizes logging (DoLog = true), so the root must carry a subjects map
        // keyed by SHA256(targetingKey). Subject keys must be hashes, never raw targeting keys.
        root.Tags.Should().ContainKey(TagSubjectsEnc, "exposure-flag (DoLog=true) must produce a subjects map");
        var subjects = JsonConvert.DeserializeObject<Dictionary<string, string>>(root.Tags[TagSubjectsEnc])!;
        subjects.Should().NotBeEmpty();
        foreach (var key in subjects.Keys)
        {
            // 64-hex-char lowercase SHA256 digest; the raw targeting key ("exposure-flag") must not leak.
            key.Should().MatchRegex("^[0-9a-f]{64}$", "subject keys must be SHA256 hex hashes");
            key.Should().NotBe("exposure-flag", "the raw targeting key must never leak into tags");
        }

        // 3) Child spans must NOT receive the ffe_* tags — enrichment targets the LOCAL ROOT only.
        child!.Tags.Should().NotContainKey(TagFlagsEnc, "child spans must not be enriched");
        child.Tags.Should().NotContainKey(TagSubjectsEnc, "child spans must not be enriched");
        child.Tags.Should().NotContainKey(TagRuntimeDefaults, "child spans must not be enriched");
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task SpanEnrichment_GateOff_WritesNoFfeTags()
    {
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.SetupRcm(
            Output,
            [
                ((object)new ServerConfiguration
                {
                    Flags = FeatureFlagsHelpers.CreateAllFlags(),
                },
                RcmProducts.FfeFlags,
                nameof(SpanEnrichmentIntegrationTests))
            ]);

        // Flag provider on, but the span-enrichment gate OFF: spans must carry NO ffe_* tags.
        var output = await RunTest(agent, spanEnrichmentEnabled: false);
        Assert.Contains("<INSTRUMENTED>", output);
        Assert.Contains("Exit. OK", output);

        var spans = await agent.WaitForSpansAsync(2, operationName: "ffe.root", returnAllOperations: true);

        using var scope = new AssertionScope();
        foreach (var span in spans)
        {
            span.Tags.Should().NotContainKey(TagFlagsEnc, "no ffe_* tags when the gate is off");
            span.Tags.Should().NotContainKey(TagSubjectsEnc, "no ffe_* tags when the gate is off");
            span.Tags.Should().NotContainKey(TagRuntimeDefaults, "no ffe_* tags when the gate is off");
        }
    }

    private async Task<string> RunTest(MockTracerAgent agent, bool spanEnrichmentEnabled)
    {
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "0.5");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FlaggingProviderEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.SpanEnrichmentEnabled, spanEnrichmentEnabled ? "1" : "0");

        using var telemetry = this.ConfigureTelemetry();
        // "enrich" tells the shared sample Program.cs to wrap evaluation in a root + child span.
        using var process = await RunSampleAndWaitForExit(agent, arguments: "enrich");
        return process.StandardOutput.ToString();
    }

    // Decode side mirrors the cross-SDK codec (system-tests test_ffe/utils.py): base64 -> ULEB128
    // delta-varint -> sorted serial ids. The round-trip oracle for the encoded flags tag.
    private static List<long> DecodeDeltaVarint(string base64)
    {
        var result = new List<long>();
        if (string.IsNullOrEmpty(base64))
        {
            return result;
        }

        var bytes = Convert.FromBase64String(base64);
        long prev = 0;
        var i = 0;
        while (i < bytes.Length)
        {
            long value = 0;
            var shift = 0;
            while (true)
            {
                var b = bytes[i++];
                value |= (long)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    break;
                }

                shift += 7;
            }

            prev += value;
            result.Add(prev);
        }

        return result;
    }
}
