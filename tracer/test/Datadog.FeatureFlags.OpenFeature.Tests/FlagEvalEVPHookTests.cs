// <copyright file="FlagEvalEVPHookTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using FluentAssertions;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

public class FlagEvalEVPHookTests
{
    [Theory]
    [InlineData("GENERAL")]
    [InlineData("PARSE_ERROR")]
    public async Task EvaluatorErrorMetadataReachesEventsWithoutChangingProviderResult(string code)
    {
        var fixture = new Fixture();
        // Evaluator failures carry a descriptive message separately from their stable code.
        var resolution = FeatureFlagsSdk.GetResolutionDetails("flag", false, new FailedEvaluation(code), 1234);
        var details = new FlagEvaluationDetails<bool>(
            resolution.FlagKey,
            resolution.Value,
            resolution.ErrorType,
            resolution.Reason,
            resolution.Variant,
            resolution.ErrorMessage,
            resolution.FlagMetadata);

        await fixture.Hook.FinallyAsync(Context(), details);

        resolution.Value.Should().BeFalse();
        resolution.ErrorType.Should().Be(ErrorType.None, "the EVP fix must not change existing provider results");
        resolution.Reason.Should().Be("error");
        resolution.ErrorMessage.Should().Be("private-evaluator-error");
        fixture.Enqueues.Should().Be(1);
        fixture.ErrorCode.Should().Be(code);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("FLAG_NOT_FOUND", "FLAG_NOT_FOUND")]
    [InlineData("INVALID_CONTEXT", "INVALID_CONTEXT")]
    [InlineData("PROVIDER_FATAL", "PROVIDER_FATAL")]
    [InlineData("PROVIDER_NOT_READY", "PROVIDER_NOT_READY")]
    [InlineData("TARGETING_KEY_MISSING", "TARGETING_KEY_MISSING")]
    [InlineData("TYPE_MISMATCH", "TYPE_MISMATCH")]
    [InlineData("private-evaluator-error", "GENERAL")]
    [InlineData(123, null)]
    public async Task MetadataErrorFallbackEmitsOnlyStableCodes(object? metadataError, string? expected)
    {
        var fixture = new Fixture();

        await fixture.Hook.FinallyAsync(Context(), Details(false, metadataError: metadataError));

        fixture.Enqueues.Should().Be(1);
        fixture.ErrorCode.Should().Be(expected);
    }

    [Fact]
    public async Task ExplicitErrorTypeTakesPrecedenceOverMetadata()
    {
        var fixture = new Fixture();

        await fixture.Hook.FinallyAsync(Context(), Details(false, error: ErrorType.TypeMismatch, metadataError: "GENERAL"));

        fixture.ErrorCode.Should().Be("TYPE_MISMATCH");
    }

    [Fact]
    public async Task CapacityRejectionPrecedesSnapshot()
    {
        var fixture = new Fixture { Available = false, ThrowOnSnapshot = true };

        await fixture.Hook.FinallyAsync(Context(), Details(true));

        fixture.Captures.Should().Be(0);
        fixture.Enqueues.Should().Be(0);
        fixture.Errors.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData("true")]
    [InlineData(1)]
    public async Task OnlyBooleanTrueCapturesContext(object? consent)
    {
        var fixture = new Fixture { ThrowOnSnapshot = true };

        await fixture.Hook.FinallyAsync(Context(), Details(consent));

        fixture.Captures.Should().Be(0);
        fixture.Enqueues.Should().Be(1);
        fixture.Consent.Should().BeFalse();
        fixture.Attributes.Should().BeNull();
        fixture.Errors.Should().Be(0);
    }

    [Fact]
    public async Task FullConsentCapturesContextAndPreservesEvaluationMetadata()
    {
        var fixture = new Fixture();

        await fixture.Hook.FinallyAsync(Context(), Details(true, timestamp: 1234d));

        fixture.Captures.Should().Be(1);
        fixture.Enqueues.Should().Be(1);
        fixture.Consent.Should().BeTrue();
        fixture.Attributes.Should().BeEquivalentTo(new Dictionary<string, object?> { ["country"] = "US" });
        fixture.TargetingKey.Should().Be("subject");
        fixture.FlagKey.Should().Be("flag");
        fixture.AllocationKey.Should().Be("allocation");
        fixture.EvalTimeMs.Should().Be(1234);
        fixture.OmissionReasons.Should().Be(0);
    }

    [Fact]
    public async Task SnapshotFailurePreservesObservationWithoutAttributes()
    {
        var fixture = new Fixture { ThrowOnSnapshot = true };

        await fixture.Hook.FinallyAsync(Context(), Details(true));

        fixture.Enqueues.Should().Be(1);
        fixture.Attributes.Should().BeNull();
        fixture.OmissionReasons.Should().Be((int)ContextOmissionReason.SnapshotError);
        fixture.Errors.Should().Be(0, "snapshot omissions are counted by enqueue, not twice as hook errors");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("on")]
    public async Task VariantPresenceIsPreserved(string? variant)
    {
        var fixture = new Fixture();

        await fixture.Hook.FinallyAsync(Context(), Details(false, variant: variant));

        fixture.Variant.Should().Be(variant);
        fixture.Enqueues.Should().Be(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task MissingAndEmptyTargetingKeysRemainDistinct(string? targetingKey)
    {
        var fixture = new Fixture();
        var builder = EvaluationContext.Builder();
        if (targetingKey is not null)
        {
            builder.Set("targetingKey", targetingKey);
        }

        var context = new HookContext<bool>("flag", false, FlagValueType.Boolean, new ClientMetadata("client", "1"), new Metadata("provider"), builder.Build());

        await fixture.Hook.FinallyAsync(context, Details(false));

        fixture.TargetingKey.Should().Be(targetingKey);
        fixture.Enqueues.Should().Be(1);
    }

    [Fact]
    public async Task SnapshotOmissionReasonsReachEnqueue()
    {
        var fixture = new Fixture();
        var context = new HookContext<bool>(
            "flag",
            false,
            FlagValueType.Boolean,
            new ClientMetadata("client", "1"),
            new Metadata("provider"),
            EvaluationContext.Builder().Set("oversized", new string('v', 257)).Build());

        await fixture.Hook.FinallyAsync(context, Details(true));

        fixture.Enqueues.Should().Be(1);
        fixture.Attributes.Should().BeEmpty();
        fixture.OmissionReasons.Should().Be((int)ContextOmissionReason.MaxValueLength);
        fixture.Errors.Should().Be(0);
    }

    [Theory]
    [InlineData(ErrorType.None, null)]
    [InlineData(ErrorType.FlagNotFound, "FLAG_NOT_FOUND")]
    [InlineData(ErrorType.InvalidContext, "INVALID_CONTEXT")]
    [InlineData(ErrorType.ParseError, "PARSE_ERROR")]
    [InlineData(ErrorType.ProviderFatal, "PROVIDER_FATAL")]
    [InlineData(ErrorType.ProviderNotReady, "PROVIDER_NOT_READY")]
    [InlineData(ErrorType.TargetingKeyMissing, "TARGETING_KEY_MISSING")]
    [InlineData(ErrorType.TypeMismatch, "TYPE_MISMATCH")]
    [InlineData(ErrorType.General, "GENERAL")]
    [InlineData((ErrorType)999, "GENERAL")]
    public async Task ErrorUsesStableCodeNotMessage(ErrorType error, string? expected)
    {
        var fixture = new Fixture();

        await fixture.Hook.FinallyAsync(Context(), Details(true, error: error));

        fixture.Enqueues.Should().Be(1);
        fixture.ErrorCode.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1234")]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1d)]
    [InlineData(1234.5d)]
    [InlineData(double.MaxValue)]
    public async Task InvalidTimestampFallsBackToHookTime(object? timestamp)
    {
        var fixture = new Fixture();
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await fixture.Hook.FinallyAsync(Context(), Details(false, timestamp: timestamp));

        fixture.EvalTimeMs.Should().BeInRange(before, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        fixture.Enqueues.Should().Be(1);
    }

    [Fact]
    public async Task MissingMetadataIsProtected()
    {
        var fixture = new Fixture { ThrowOnSnapshot = true };
        var details = new FlagEvaluationDetails<bool>("flag", false, ErrorType.ProviderNotReady, null, null);

        await fixture.Hook.FinallyAsync(Context(), details);

        fixture.Enqueues.Should().Be(1);
        fixture.Consent.Should().BeFalse();
        fixture.Captures.Should().Be(0);
        fixture.AllocationKey.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HookAndMetricSinkFailuresCannotEscape(bool failCapacity)
    {
        var fixture = new Fixture { ThrowOnCapacity = failCapacity, ThrowOnEnqueue = !failCapacity, ThrowOnError = true };

        await fixture.Hook.FinallyAsync(Context(), Details(true));

        fixture.Errors.Should().Be(1);
    }

    [Fact]
    public async Task UninstrumentedProviderRegistersInertHook()
    {
        using var provider = new DatadogProvider();
        var hook = provider.GetProviderHooks().OfType<FlagEvalEVPHook>().Single();

        FeatureFlagsSdk.CanEnqueueEVP().Should().BeFalse();
        await hook.FinallyAsync(Context(), Details(true));
        var result = await provider.ResolveBooleanValueAsync("flag", false);
        result.Value.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.ProviderNotReady);
    }

    private static HookContext<bool> Context() => new(
        "flag",
        false,
        FlagValueType.Boolean,
        new ClientMetadata("client", "1"),
        new Metadata("provider"),
        EvaluationContext.Builder().Set("targetingKey", "subject").Set("country", "US").Build());

    private static FlagEvaluationDetails<bool> Details(object? consent, string? variant = "on", ErrorType error = ErrorType.None, object? timestamp = null, object? metadataError = null)
    {
        var metadata = new Dictionary<string, object> { ["__dd_allocation_key"] = "allocation" };
        if (consent is not null)
        {
            metadata[FeatureFlagMetadataKeys.ObserveFullEvaluationData] = consent;
        }

        if (timestamp is not null)
        {
            metadata[FeatureFlagMetadataKeys.EvaluationTimestampMs] = timestamp;
        }

        if (metadataError is not null)
        {
            metadata["errorCode"] = metadataError;
        }

        return new FlagEvaluationDetails<bool>("flag", true, error, "static", variant, "pii-error-canary", new ImmutableMetadata(metadata));
    }

    private sealed class FailedEvaluation(string errorCode) : IEvaluation
    {
        public string FlagKey => "flag";

        public object Value => false;

        public EvaluationReason Reason => EvaluationReason.Error;

        public string? Variant => null;

        public string? Error => "private-evaluator-error";

        public IDictionary<string, string>? FlagMetadata => new Dictionary<string, string> { ["errorCode"] = errorCode };
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Hook = new FlagEvalEVPHook(
                () => ThrowOnCapacity ? throw new InvalidOperationException("pii-canary") : Available,
                (flag, variant, allocation, target, time, error, attrs, flags) =>
                {
                    if (ThrowOnEnqueue)
                    {
                        throw new InvalidOperationException("pii-canary");
                    }

                    Enqueues++;
                    FlagKey = flag;
                    Variant = variant;
                    AllocationKey = allocation;
                    TargetingKey = target;
                    EvalTimeMs = time;
                    Consent = (flags & FlagEvaluationBridge.ObserveFullEvaluationData) != 0;
                    ErrorCode = error;
                    Attributes = attrs;
                    OmissionReasons = flags & ~FlagEvaluationBridge.ObserveFullEvaluationData;
                },
                () =>
                {
                    Errors++;
                    if (ThrowOnError)
                    {
                        throw new InvalidOperationException("pii-canary");
                    }
                },
                Capture);
        }

        public FlagEvalEVPHook Hook { get; }

        public bool Available { get; set; } = true;

        public bool ThrowOnSnapshot { get; set; }

        public bool ThrowOnCapacity { get; set; }

        public bool ThrowOnEnqueue { get; set; }

        public bool ThrowOnError { get; set; }

        public int Captures { get; private set; }

        public int Enqueues { get; private set; }

        public int Errors { get; private set; }

        public string? FlagKey { get; private set; }

        public string? Variant { get; private set; }

        public string? AllocationKey { get; private set; }

        public string? TargetingKey { get; private set; }

        public long EvalTimeMs { get; private set; }

        public bool Consent { get; private set; }

        public string? ErrorCode { get; private set; }

        public IReadOnlyDictionary<string, object?>? Attributes { get; private set; }

        public int OmissionReasons { get; private set; }

        private IReadOnlyDictionary<string, object?> Capture(EvaluationContext? context, out int reasons)
        {
            Captures++;
            if (ThrowOnSnapshot)
            {
                throw new InvalidOperationException("pii-canary");
            }

            return FlagEvaluationContextSnapshot.Capture(context, out reasons);
        }
    }
}
