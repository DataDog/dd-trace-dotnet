// <copyright file="SpanEnrichmentHookTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using FluentAssertions;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

public class SpanEnrichmentHookTests
{
    [Theory]
    [InlineData("1001", 1001L)]
    [InlineData("2147483648", 2147483648L)]
    [InlineData("4294967295", 4294967295L)]
    public async Task ReadsTheSerialIdAndDoLogFromTypedMetadata(string wireSerialId, long expectedSerialId)
    {
        var evaluation = new TestEvaluation(new Dictionary<string, string>
        {
            [FeatureFlagMetadataKeys.SplitSerialId] = wireSerialId,
            [FeatureFlagMetadataKeys.DoLog] = "true",
        });
        long? serialId = null;
        bool doLog = false;

        await Run(evaluation, (id, log, _, _, _, _) =>
        {
            serialId = id;
            doLog = log;
        });

        serialId.Should().Be(expectedSerialId);
        doLog.Should().BeTrue();
    }

    [Fact]
    public async Task MissingMetadataAccumulatesDefaults()
    {
        long? serialId = null;
        var doLog = true;
        var targetingKey = "not-set";

        await Run(new TestEvaluation(null), (id, log, key, _, _, _) =>
        {
            serialId = id;
            doLog = log;
            targetingKey = key;
        });

        serialId.Should().BeNull();
        doLog.Should().BeFalse();
        targetingKey.Should().Be("user-123");
    }

    [Fact]
    public async Task MetadataWithoutADoLogAccumulatesFalse()
    {
        var doLog = true;

        await Run(new TestEvaluation(new Dictionary<string, string> { [FeatureFlagMetadataKeys.SplitSerialId] = "7" }), (_, log, _, _, _, _) => doLog = log);

        doLog.Should().BeFalse();
    }

    private static async Task Run(IEvaluation evaluation, Action<long?, bool, string?, bool, string, object?> accumulate)
    {
        var hook = new SpanEnrichmentHook(accumulate);
        var resolution = FeatureFlagsSdk.GetResolutionDetails("flag", false, evaluation, null);
        var details = new FlagEvaluationDetails<bool>(resolution.FlagKey, resolution.Value, resolution.ErrorType, resolution.Reason, resolution.Variant, resolution.ErrorMessage, resolution.FlagMetadata);
        await hook.FinallyAsync(Context(), details);
    }

    private static HookContext<bool> Context()
        => new("flag", false, FlagValueType.Boolean, new ClientMetadata(null, null), new Metadata("test"), EvaluationContext.Builder().SetTargetingKey("user-123").Build());

    internal sealed class TestEvaluation(Dictionary<string, string>? metadata) : IEvaluation
    {
        public string FlagKey => "flag";

        public object Value => true;

        public EvaluationReason Reason => EvaluationReason.Static;

        public string? Variant => "on";

        public string? Error => null;

        public IDictionary<string, string>? FlagMetadata => metadata;
    }
}
