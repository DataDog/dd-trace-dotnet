// <copyright file="ExecutedTelemetryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using static Datadog.Trace.Telemetry.Metrics.MetricTags;

#nullable enable
namespace Datadog.Trace.Iast.Telemetry;

internal class ExecutedTelemetryHelper
{
    private const string BasicExecutedTag = "_dd.iast.telemetry.";
    private const string SourceExecutedTag = "executed.source.";
    private const string SinkExecutedTag = "executed.sink.";
    private const string PropagationExecutedTag = BasicExecutedTag + "executed.propagation";
    private const string RequestTaintedTag = BasicExecutedTag + "request.tainted";
    private static IastMetricsVerbosityLevel _verbosityLevel = Iast.Instance.Settings.TelemetryVerbosity;
    private int[] _executedSinks = new int[Trace.Telemetry.Metrics.IastInstrumentedSinksExtensions.Length];
    private int[] _executedSources = new int[Trace.Telemetry.Metrics.IastInstrumentedSourcesExtensions.Length];
    private int _executedPropagations = 0;
    private object _metricsLock = new();

    public static bool Enabled()
        => _verbosityLevel >= IastMetricsVerbosityLevel.Information;

    public static bool EnabledDebug()
        => _verbosityLevel == IastMetricsVerbosityLevel.Debug;

    public void AddExecutedSink(IastInstrumentedSinks type)
    {
        if (_verbosityLevel >= IastMetricsVerbosityLevel.Information)
        {
            lock (_metricsLock)
            {
                _executedSinks[(int)type]++;
            }

            TelemetryFactory.Metrics.RecordCountIastExecutedSinks(type);
        }
    }

    public void AddExecutedPropagation()
    {
        if (_verbosityLevel == IastMetricsVerbosityLevel.Debug)
        {
            lock (_metricsLock)
            {
                _executedPropagations++;
            }

            TelemetryFactory.Metrics.RecordCountIastExecutedPropagations();
        }
    }

    public void AddExecutedSource(IastInstrumentedSources type)
    {
        if (_verbosityLevel >= IastMetricsVerbosityLevel.Information)
        {
            lock (_metricsLock)
            {
                _executedSources[(int)type]++;
            }

            TelemetryFactory.Metrics.RecordCountIastExecutedSources(type);
        }
    }

    public void GenerateMetricTags(ITags tags, int taintedSize)
    {
        lock (_metricsLock)
        {
            if (_executedPropagations > 0)
            {
                tags.SetMetric(PropagationExecutedTag, _executedPropagations);
            }

            for (int i = 0; i < _executedSources.Length; i++)
            {
                if (_executedSources[i] > 0)
                {
                    tags.SetMetric(GetExecutedSourceTag((IastInstrumentedSources)i), _executedSources[i]);
                }
            }

            for (int i = 0; i < _executedSinks.Length; i++)
            {
                if (_executedSinks[i] > 0)
                {
                    tags.SetMetric(GetExecutedSinkTag((IastInstrumentedSinks)i), _executedSinks[i]);
                }
            }

            ResetMetrics();
        }

        if (_verbosityLevel >= IastMetricsVerbosityLevel.Information)
        {
            TelemetryFactory.Metrics.RecordCountIastRequestTainted(taintedSize);
            tags.SetMetric(RequestTaintedTag, taintedSize);
        }
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
        => vulnerability switch
        {
            IastInstrumentedSinks.WeakCipher => BasicExecutedTag + SinkExecutedTag + "weak_cipher",
            IastInstrumentedSinks.WeakHash => BasicExecutedTag + SinkExecutedTag + "weak_hash",
            IastInstrumentedSinks.SqlInjection => BasicExecutedTag + SinkExecutedTag + "sql_injection",
            IastInstrumentedSinks.CommandInjection => BasicExecutedTag + SinkExecutedTag + "command_injection",
            IastInstrumentedSinks.PathTraversal => BasicExecutedTag + SinkExecutedTag + "path_traversal",
            IastInstrumentedSinks.LdapInjection => BasicExecutedTag + SinkExecutedTag + "ldap_injection",
            IastInstrumentedSinks.Ssrf => BasicExecutedTag + SinkExecutedTag + "ssrf",
            IastInstrumentedSinks.UnvalidatedRedirect => BasicExecutedTag + SinkExecutedTag + "unvalidated_redirect",
            IastInstrumentedSinks.InsecureCookie => BasicExecutedTag + SinkExecutedTag + "insecure_cookie",
            IastInstrumentedSinks.NoHttpOnlyCookie=> BasicExecutedTag + SinkExecutedTag + "no_http_only_cookie",
            IastInstrumentedSinks.NoSameSiteCookie => BasicExecutedTag + SinkExecutedTag + "no_samesite_cookie",
            IastInstrumentedSinks.WeakRandomness => BasicExecutedTag + SinkExecutedTag + "weak_randomness",
            IastInstrumentedSinks.HardcodedSecret => BasicExecutedTag + SinkExecutedTag + "hardcoded_secret",
            IastInstrumentedSinks.XContentTypeHeaderMissing => BasicExecutedTag + SinkExecutedTag + "xcontenttype_header_missing",
            IastInstrumentedSinks.TrustBoundaryViolation => BasicExecutedTag + SinkExecutedTag + "trust_boundary_violation",
            IastInstrumentedSinks.HstsHeaderMissing => BasicExecutedTag + SinkExecutedTag + "hsts_header_missing",
            IastInstrumentedSinks.HeaderInjection => BasicExecutedTag + SinkExecutedTag + "header_injection",
            IastInstrumentedSinks.StackTraceLeak => BasicExecutedTag + SinkExecutedTag + "stacktrace_leak",
            IastInstrumentedSinks.NoSqlMongoDbInjection => BasicExecutedTag + SinkExecutedTag + "nosql_mongodb_injection",
            IastInstrumentedSinks.XPathInjection => BasicExecutedTag + SinkExecutedTag + "xpath_injection",
            IastInstrumentedSinks.ReflectionInjection => BasicExecutedTag + SinkExecutedTag + "reflection_injection",
            IastInstrumentedSinks.InsecureAuthProtocol => BasicExecutedTag + SinkExecutedTag + "insecure_auth_protocol",
            IastInstrumentedSinks.Xss => BasicExecutedTag + SinkExecutedTag + "xss",
            IastInstrumentedSinks.DirectoryListingLeak => BasicExecutedTag + SinkExecutedTag + "directory_listing_leak",
            IastInstrumentedSinks.SessionTimeout => BasicExecutedTag + SinkExecutedTag + "session_timeout",
            IastInstrumentedSinks.EmailHtmlInjection => BasicExecutedTag + SinkExecutedTag + "email_html_injection",
            IastInstrumentedSinks.None => throw new System.Exception($"Undefined vulnerability name for value {vulnerability}."),
            _ => throw new System.Exception($"Undefined vulnerability name for value {vulnerability}."),
        };

    private string? GetSourceTag(IastInstrumentedSources source)
    {
        return SourceTypeUtils.GetAsTag((SourceType)source);
    }
}
