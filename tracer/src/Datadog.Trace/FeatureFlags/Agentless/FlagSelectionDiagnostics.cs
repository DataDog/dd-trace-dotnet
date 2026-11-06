// <copyright file="FlagSelectionDiagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.FeatureFlags.Agentless;

/// <summary>
/// Reports what happened to a flag selection, by reason and limit only: the selected values are never
/// logged or counted. Each count, and each log the client SDKs write once per process, is written once
/// per process through <see cref="Shared"/>.
/// </summary>
internal sealed class FlagSelectionDiagnostics
{
    internal const string IgnoredHeaderName = "DD-Flag-Selection-Ignored";

    internal const string RemoteConfigWarning = "FlagSelection is not supported when flags are delivered through Remote Config. Loading the full flag configuration.";
    internal const string DifferentSelectionWarning = "FlagSelection differs from the flag selection already in use in this process. Using the first selection.";

    internal const string MalformedSelectionNotSent = NotSentPrefix + "malformed_selection: DatadogProviderOptions.FlagSelection must be an object with optional Tags and KeyPatterns string arrays" + NotSentSuffix;
    internal const string TooManyValuesNotSent = NotSentPrefix + "too_many_values: each list accepts at most 31 values" + NotSentSuffix;
    internal const string InvalidKeyPatternNotSent = NotSentPrefix + "invalid_key_pattern: each key pattern is at most 200 UTF-8 bytes" + NotSentSuffix;
    internal const string InvalidTagNotSent = NotSentPrefix + "invalid_tag: each tag is at most 200 UTF-8 bytes" + NotSentSuffix;
    internal const string UrlLimitNotSent = NotSentPrefix + "too_many_values: the request URL is at most 8192 bytes" + NotSentSuffix;
    internal const string MalformedSelectionIgnored = IgnoredPrefix + "malformed_selection" + IgnoredSuffix;
    internal const string TooManyValuesIgnored = IgnoredPrefix + "too_many_values" + IgnoredSuffix;
    internal const string InvalidKeyPatternIgnored = IgnoredPrefix + "invalid_key_pattern" + IgnoredSuffix;
    internal const string InvalidTagIgnored = IgnoredPrefix + "invalid_tag" + IgnoredSuffix;
    internal const string TagLoadFailedIgnored = IgnoredPrefix + "tag_load_failed" + IgnoredSuffix;
    internal const string UnknownIgnored = IgnoredPrefix + "unknown" + IgnoredSuffix;

    private const string NotSentPrefix = "FlagSelection was not sent (";
    private const string NotSentSuffix = "). Loading the full flag configuration.";
    private const string IgnoredPrefix = "FlagSelection was ignored by the server (";
    private const string IgnoredSuffix = "). Loaded the full flag configuration.";

    private readonly IMetricsTelemetryCollector? _metrics;

    // Bit sets indexed by reason.
    private int _droppedCounted;
    private int _ignoredReported;
    private int _remoteConfigWarned;
    private int _differentSelectionWarned;

    internal FlagSelectionDiagnostics(IMetricsTelemetryCollector? metrics = null)
    {
        _metrics = metrics;
    }

    internal static FlagSelectionDiagnostics Shared { get; } = new();

    private IMetricsTelemetryCollector Metrics => _metrics ?? TelemetryFactory.Metrics;

    /// <summary>
    /// Keeps the reason the server reported only when it matches a known one byte for byte. An empty
    /// value is absent, and anything else, including surrounding white space or repeated headers
    /// joined with commas, is unknown.
    /// </summary>
    internal static MetricTags.FlagSelectionIgnoredReason? ParseIgnoredReason(string? headerValue)
        => headerValue switch
        {
            null or "" => null,
            "malformed_selection" => MetricTags.FlagSelectionIgnoredReason.MalformedSelection,
            "too_many_values" => MetricTags.FlagSelectionIgnoredReason.TooManyValues,
            "invalid_key_pattern" => MetricTags.FlagSelectionIgnoredReason.InvalidKeyPattern,
            "invalid_tag" => MetricTags.FlagSelectionIgnoredReason.InvalidTag,
            "tag_load_failed" => MetricTags.FlagSelectionIgnoredReason.TagLoadFailed,
            _ => MetricTags.FlagSelectionIgnoredReason.Unknown,
        };

    // Logged for every provider whose selection is not sent, and counted once per process per reason.
    // Skips telemetry, like the rest of these logs: the customer's selection is outside the server's
    // limits, which is not a tracer bug.
    internal void ReportDropped(IDatadogLogger log, MetricTags.FlagSelectionDroppedReason reason)
    {
        Try(() =>
        {
            switch (reason)
            {
                case MetricTags.FlagSelectionDroppedReason.MalformedSelection:
                    log.ErrorSkipTelemetry(MalformedSelectionNotSent);
                    break;
                case MetricTags.FlagSelectionDroppedReason.TooManyValues:
                    log.ErrorSkipTelemetry(TooManyValuesNotSent);
                    break;
                case MetricTags.FlagSelectionDroppedReason.InvalidKeyPattern:
                    log.ErrorSkipTelemetry(InvalidKeyPatternNotSent);
                    break;
                default:
                    log.ErrorSkipTelemetry(InvalidTagNotSent);
                    break;
            }
        });
        CountDropped(reason);
    }

    internal void ReportDroppedForUrlLimit(IDatadogLogger log)
    {
        Try(() => log.ErrorSkipTelemetry(UrlLimitNotSent));
        CountDropped(MetricTags.FlagSelectionDroppedReason.TooManyValues);
    }

    internal void ReportIgnored(IDatadogLogger log, MetricTags.FlagSelectionIgnoredReason reason)
    {
        if (!TryMark(ref _ignoredReported, (int)reason))
        {
            return;
        }

        Try(() =>
        {
            switch (reason)
            {
                case MetricTags.FlagSelectionIgnoredReason.MalformedSelection:
                    log.ErrorSkipTelemetry(MalformedSelectionIgnored);
                    break;
                case MetricTags.FlagSelectionIgnoredReason.TooManyValues:
                    log.ErrorSkipTelemetry(TooManyValuesIgnored);
                    break;
                case MetricTags.FlagSelectionIgnoredReason.InvalidKeyPattern:
                    log.ErrorSkipTelemetry(InvalidKeyPatternIgnored);
                    break;
                case MetricTags.FlagSelectionIgnoredReason.InvalidTag:
                    log.ErrorSkipTelemetry(InvalidTagIgnored);
                    break;
                case MetricTags.FlagSelectionIgnoredReason.TagLoadFailed:
                    log.ErrorSkipTelemetry(TagLoadFailedIgnored);
                    break;
                default:
                    log.ErrorSkipTelemetry(UnknownIgnored);
                    break;
            }
        });
        Try(() => Metrics.RecordCountFlagSelectionIgnored(reason));
    }

    internal void WarnRemoteConfig(IDatadogLogger log)
    {
        if (TryMark(ref _remoteConfigWarned, 0))
        {
            Try(() => log.Warning(RemoteConfigWarning));
        }
    }

    internal void WarnDifferentSelection(IDatadogLogger log)
    {
        if (TryMark(ref _differentSelectionWarned, 0))
        {
            Try(() => log.Warning(DifferentSelectionWarning));
        }
    }

    private static bool TryMark(ref int bits, int bit)
    {
        var mask = 1 << bit;
        while (true)
        {
            var current = Volatile.Read(ref bits);
            if ((current & mask) != 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref bits, current | mask, current) == current)
            {
                return true;
            }
        }
    }

    // A diagnostic must never change what the provider does.
    private static void Try(Action report)
    {
        try
        {
            report();
        }
        catch (Exception)
        {
        }
    }

    private void CountDropped(MetricTags.FlagSelectionDroppedReason reason)
    {
        if (TryMark(ref _droppedCounted, (int)reason))
        {
            Try(() => Metrics.RecordCountFlagSelectionDropped(reason));
        }
    }
}
