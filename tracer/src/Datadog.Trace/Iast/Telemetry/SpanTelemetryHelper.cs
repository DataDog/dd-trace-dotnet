// <copyright file="SpanTelemetryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Web.Caching;
using Datadog.Trace.Telemetry;
using static Datadog.Trace.Telemetry.Metrics.MetricTags;

#nullable enable
namespace Datadog.Trace.Iast.Telemetry;

internal class SpanTelemetryHelper
{
    private const string BasicExecutedTag = "_dd.iast.telemetry.";
    private const string SourceExecutedTag = "executed.source.";
    private const string SinkExecutedTag = "executed.sink.";
    private const string PropagationExecutedTag = "executed.propagation";
    private static IastMetricsVerbosityLevel _verbosityLevel = Iast.Instance.Settings.IastTelemetryVerbosity;
    private int[] _executedSinks = new int[Enum.GetValues(typeof(IastInstrumentedSinks)).Length];
    private int[] _executedSources = new int[Enum.GetValues(typeof(IastInstrumentedSources)).Length];
    private int _executedInstrumentations = 0;

    public static bool Enabled()
    {
        return Iast.Instance.Settings.IastTelemetryVerbosity <= IastMetricsVerbosityLevel.Information;
    }

    public void AddExecutedSink(VulnerabilityType type)
    {
        if (_verbosityLevel <= IastMetricsVerbosityLevel.Information)
        {
            _executedSinks[(int)type]++;
        }
    }

    public void AddExecutedInstrumentation()
    {
        if (_verbosityLevel <= IastMetricsVerbosityLevel.Debug)
        {
            _executedInstrumentations++;
        }
    }

    public void AddExecutedSource(SourceTypeName type)
    {
        if (_verbosityLevel <= IastMetricsVerbosityLevel.Information)
        {
            _executedSources[(int)type]++;
        }
    }

    public List<Tuple<string, int>> GetMetricTags()
    {
        List<Tuple<string, int>> tags = new();

        if (_executedInstrumentations > 0)
        {
            tags.Add(Tuple.Create(PropagationExecutedTag, _executedInstrumentations));
        }

        for (int i = 0; i < _executedSources.Length; i++)
        {
            if (_executedSources[i] > 0)
            {
                tags.Add(Tuple.Create(GetExecutedSourceTag((SourceTypeName)i), _executedSources[i]));
            }
        }

        for (int i = 0; i < _executedSinks.Length; i++)
        {
            if (_executedSinks[i] > 0)
            {
                tags.Add(Tuple.Create(GetExecutedSinkTag((VulnerabilityType)i), _executedSinks[i]));
            }
        }

        return tags;
    }

    private string GetExecutedSourceTag(SourceTypeName source)
    {
        return BasicExecutedTag + SourceExecutedTag + GetSourceTag(source);
    }

    private string GetExecutedSinkTag(VulnerabilityType vulnerability)
    {
        return BasicExecutedTag + SinkExecutedTag + GetVulnerabilityTag(vulnerability);
    }

    // TODO: make test to make sure that all vulnerabilitis have tags

    private string GetVulnerabilityTag(VulnerabilityType vulnerability)
        => vulnerability switch
        {
            VulnerabilityType.LdapInjection => "ldap_injection",
            VulnerabilityType.SqlInjection => "sql_injection",
            VulnerabilityType.CommandInjection => "command_injection",
            VulnerabilityType.InsecureCookie => "insecure_cookie",
            VulnerabilityType.NoHttpOnlyCookie=> "no_http_only_cookie",
            VulnerabilityType.NoSameSiteCookie => "no_samesite_cookie",
            VulnerabilityType.WeakCipher => "weak_cipher",
            VulnerabilityType.WeakHash => "weak_hash",
            VulnerabilityType.PathTraversal => "path_traversal",
            VulnerabilityType.Ssrf => "ssrf",
            VulnerabilityType.UnvalidatedRedirect => "unvalidated_redirect",
            VulnerabilityType.None => throw new System.Exception($"Undefined vulnerability name for value {vulnerability}."),
            _ => throw new System.Exception($"Undefined vulnerability name for value {vulnerability}."),
        };

    // TODO: make test to make sure that all vulnerabilitis have tags

    private string? GetSourceTag(SourceTypeName source)
    {
        return SourceType.GetString(source).Replace(".", "_");
    }
}
