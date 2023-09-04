// <copyright file="ExecutedTelemetryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Telemetry;
using static Datadog.Trace.Telemetry.Metrics.MetricTags;

#nullable enable
namespace Datadog.Trace.Iast.Telemetry;

internal class ExecutedTelemetryHelper
{
    private const string BasicExecutedTag = "_dd.iast.telemetry.";
    private const string SourceExecutedTag = "executed.source.";
    private const string SinkExecutedTag = "executed.sink.";
    private const string PropagationExecutedTag = "executed.propagation";
    private static bool? _enabled = null;
    private static bool? _enabledDebug = null;
    private static IastMetricsVerbosityLevel _verbosityLevel = Iast.Instance.Settings.IastTelemetryVerbosity;
    private int[] _executedSinks = new int[Enum.GetValues(typeof(IastInstrumentedSinks)).Length];
    private int[] _executedSources = new int[Enum.GetValues(typeof(IastInstrumentedSources)).Length];
    private int _executedPropagations = 0;

    public static bool Enabled()
    {
        if (_enabled is null)
        {
            var telemetryEnabled = Iast.Instance.Settings.TelemetryEnabled;
            // This class does not send any mandatory telemetry
            _enabled = telemetryEnabled && _verbosityLevel <= IastMetricsVerbosityLevel.Information;
        }

        return _enabled ?? false;
    }

    public static bool EnabledDebug()
    {
        return _enabledDebug ?? (Enabled() && _verbosityLevel == IastMetricsVerbosityLevel.Debug);
    }

    public void AddExecutedSink(IastInstrumentedSinks type)
    {
        if (_verbosityLevel <= IastMetricsVerbosityLevel.Information)
        {
            _executedSinks[(int)type]++;
            TelemetryFactory.Metrics.RecordCountIastExecutedSinks(type);
        }
    }

    public void AddExecutedInstrumentation()
    {
        if (_verbosityLevel <= IastMetricsVerbosityLevel.Debug)
        {
            _executedPropagations++;
            TelemetryFactory.Metrics.RecordCountIastExecutedPropagations();
        }
    }

    public void AddExecutedSource(IastInstrumentedSources type)
    {
        if (_verbosityLevel <= IastMetricsVerbosityLevel.Information)
        {
            _executedSources[(int)type]++;
            TelemetryFactory.Metrics.RecordCountIastExecutedSources(type);
        }
    }

    public List<Tuple<string, int>> GenerateMetricTags()
    {
        List<Tuple<string, int>> tags = new();

        if (_executedPropagations > 0)
        {
            tags.Add(Tuple.Create(PropagationExecutedTag, _executedPropagations));
        }

        for (int i = 0; i < _executedSources.Length; i++)
        {
            if (_executedSources[i] > 0)
            {
                tags.Add(Tuple.Create(GetExecutedSourceTag((IastInstrumentedSources)i), _executedSources[i]));
            }
        }

        for (int i = 0; i < _executedSinks.Length; i++)
        {
            if (_executedSinks[i] > 0)
            {
                tags.Add(Tuple.Create(GetExecutedSinkTag((IastInstrumentedSinks)i), _executedSinks[i]));
            }
        }

        ResetMetrics();

        return tags;
    }

    private void ResetMetrics()
    {
        _executedPropagations = 0;

        for (int i = 0; i < _executedSources.Length; i++)
        {
            _executedSources[i] = 0;
        }

        for (int i = 0; i < _executedSinks.Length; i++)
        {
            _executedSinks[i] = 0;
        }
    }

    private string GetExecutedSourceTag(IastInstrumentedSources source)
    {
        return BasicExecutedTag + SourceExecutedTag + GetSourceTag(source);
    }

    private string GetExecutedSinkTag(IastInstrumentedSinks vulnerability)
    {
        return BasicExecutedTag + SinkExecutedTag + GetVulnerabilityTag(vulnerability);
    }

    private string GetVulnerabilityTag(IastInstrumentedSinks vulnerability)
        => vulnerability switch
        {
            IastInstrumentedSinks.LdapInjection => "ldap_injection",
            IastInstrumentedSinks.SqlInjection => "sql_injection",
            IastInstrumentedSinks.CommandInjection => "command_injection",
            IastInstrumentedSinks.InsecureCookie => "insecure_cookie",
            IastInstrumentedSinks.NoHttpOnlyCookie=> "no_http_only_cookie",
            IastInstrumentedSinks.NoSameSiteCookie => "no_samesite_cookie",
            IastInstrumentedSinks.WeakCipher => "weak_cipher",
            IastInstrumentedSinks.WeakHash => "weak_hash",
            IastInstrumentedSinks.PathTraversal => "path_traversal",
            IastInstrumentedSinks.Ssrf => "ssrf",
            IastInstrumentedSinks.UnvalidatedRedirect => "unvalidated_redirect",
            IastInstrumentedSinks.None => throw new System.Exception($"Undefined vulnerability name for value {vulnerability}."),
            _ => throw new System.Exception($"Undefined vulnerability name for value {vulnerability}."),
        };

    private string? GetSourceTag(IastInstrumentedSources source)
    {
        return SourceType.GetString((SourceTypeName)source);
    }
}
