// <copyright file="ManagedTraceSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling;

/// <summary>
/// An implementation of <see cref="ITraceSampler"/> that handles rebuilding the trace sampler on changes.
///
/// </summary>
internal sealed class ManagedTraceSampler : ITraceSampler
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ManagedTraceSampler>();
    private readonly object _lock = new();
    private IReadOnlyDictionary<string, float>? _defaultSampleRates;
    private TraceSampler _current;

    public ManagedTraceSampler(TracerSettings settings)
    {
        _current = CreateSampler(settings.Manager.InitialMutableSettings, settings.CustomSamplingRulesFormat);
    }

    public bool HasResourceBasedSamplingRule => Volatile.Read(ref _current).HasResourceBasedSamplingRule;

    public void SetDefaultSampleRates(IReadOnlyDictionary<string, float> sampleRates)
    {
        // lock to avoid missed sample rate updates when updating inner sample
        lock (_lock)
        {
            // save the rates for if/when we rebuild on setting changes
            _defaultSampleRates = sampleRates;
            _current.SetDefaultSampleRates(sampleRates);
        }
    }

    public SamplingDecision MakeSamplingDecision(Span span) => Volatile.Read(ref _current).MakeSamplingDecision(span);

    // used for testing
    internal IReadOnlyList<ISamplingRule> GetRules() => Volatile.Read(ref _current).GetRules();

    private static TraceSampler CreateSampler(MutableSettings settings, string customSamplingRulesFormat)
    {
        // ISamplingRule is used to implement, in order of precedence:
        // - custom sampling rules
        //   - remote custom rules (provenance: "customer")
        //   - remote dynamic rules (provenance: "dynamic")
        //   - local custom rules (provenance: "local"/none) = DD_TRACE_SAMPLING_RULES
        // - global sampling rate
        //   - remote
        //   - local = DD_TRACE_SAMPLE_RATE
        // - agent sampling rates, if using a Datadog encoding (as a single rule)

        // Note: the order that rules are registered is important, as they are evaluated in order.
        // The first rule that matches will be used to determine the sampling rate.

        var sampler = new TraceSampler.Builder(new TracerRateLimiter(maxTracesPerInterval: settings.MaxTracesSubmittedPerSecond, intervalMilliseconds: null));

        // sampling rules (remote value overrides local value)
        var samplingRulesJson = settings.CustomSamplingRules;

        // check if the rules are remote or local because they have different JSON schemas
        if (settings.CustomSamplingRulesIsRemote)
        {
            // remote sampling rules
            if (!StringUtil.IsNullOrWhiteSpace(samplingRulesJson))
            {
                var remoteSamplingRules =
                    RemoteCustomSamplingRule.BuildFromConfigurationString(
                        samplingRulesJson,
                        RegexBuilder.DefaultTimeout);

                sampler.RegisterRules(remoteSamplingRules);
            }
        }
        else
        {
            // local sampling rules
            var patternFormatIsValid = SamplingRulesFormat.IsValid(customSamplingRulesFormat, out var samplingRulesFormat);

            if (patternFormatIsValid && !StringUtil.IsNullOrWhiteSpace(samplingRulesJson))
            {
                var localSamplingRules =
                    LocalCustomSamplingRule.BuildFromConfigurationString(
                        samplingRulesJson,
                        samplingRulesFormat,
                        RegexBuilder.DefaultTimeout);

                sampler.RegisterRules(localSamplingRules);
            }
        }

        // global sampling rate (remote value overrides local value)
        if (settings.GlobalSamplingRate is { } globalSamplingRate)
        {
            if (globalSamplingRate is < 0f or > 1f)
            {
                Log.Warning(
                    "{ConfigurationKey} configuration of {ConfigurationValue} is out of range",
                    ConfigurationKeys.GlobalSamplingRate,
                    settings.GlobalSamplingRate);
            }
            else
            {
                sampler.RegisterRule(new GlobalSamplingRateRule((float)globalSamplingRate));
            }
        }

        return sampler.Build();
    }
}
