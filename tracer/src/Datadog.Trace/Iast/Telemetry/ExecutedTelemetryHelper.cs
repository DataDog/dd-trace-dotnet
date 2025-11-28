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

internal sealed class ExecutedTelemetryHelper
{
    private const string BasicExecutedTag = "_dd.iast.telemetry";
    private const string PropagationExecutedTag = BasicExecutedTag + ".executed.propagation";
    private const string RequestTaintedTag = BasicExecutedTag + ".request.tainted";
    private static string[] _executedSinkTags = new string[Trace.Telemetry.Metrics.IastVulnerabilityTypeExtensions.Length];
    private static string[] _executedSourceTags = new string[Trace.Telemetry.Metrics.IastSourceTypeExtensions.Length];
    private static string[] _supressedVulnerabilityTags = new string[Trace.Telemetry.Metrics.IastVulnerabilityTypeExtensions.Length];
    private static IastMetricsVerbosityLevel _verbosityLevel = Iast.Instance.Settings.TelemetryVerbosity;
    private int[] _executedSinks = new int[Trace.Telemetry.Metrics.IastVulnerabilityTypeExtensions.Length];
    private int[] _executedSources = new int[Trace.Telemetry.Metrics.IastSourceTypeExtensions.Length];
    private int[] _supressedVulnerabilities = new int[Trace.Telemetry.Metrics.IastVulnerabilityTypeExtensions.Length];
    private int _executedPropagations = 0;
    private object _metricsLock = new();

    static ExecutedTelemetryHelper()
    {
        // Initialize the tags
        for (int i = 0; i < _executedSinkTags.Length; i++)
        {
            _executedSinkTags[i] = $"{BasicExecutedTag}.executed.sink.{GetTag((IastVulnerabilityType)i, false)}";
        }

        for (int i = 0; i < _executedSourceTags.Length; i++)
        {
            _executedSourceTags[i] = $"{BasicExecutedTag}.executed.source.{GetTag((IastSourceType)i)}";
        }

        for (int i = 0; i < _supressedVulnerabilityTags.Length; i++)
        {
            _supressedVulnerabilityTags[i] = $"{BasicExecutedTag}.suppressed.vulnerabilities.{GetTag((IastVulnerabilityType)i, false)}";
        }
    }

    private static string GetTag(IastVulnerabilityType vulnerability, bool raiseException = true)
        => vulnerability switch
        {
            IastVulnerabilityType.WeakCipher => "weak_cipher",
            IastVulnerabilityType.WeakHash => "weak_hash",
            IastVulnerabilityType.SqlInjection => "sql_injection",
            IastVulnerabilityType.CommandInjection => "command_injection",
            IastVulnerabilityType.PathTraversal => "path_traversal",
            IastVulnerabilityType.LdapInjection => "ldap_injection",
            IastVulnerabilityType.Ssrf => "ssrf",
            IastVulnerabilityType.UnvalidatedRedirect => "unvalidated_redirect",
            IastVulnerabilityType.InsecureCookie => "insecure_cookie",
            IastVulnerabilityType.NoHttpOnlyCookie => "no_http_only_cookie",
            IastVulnerabilityType.NoSameSiteCookie => "no_samesite_cookie",
            IastVulnerabilityType.WeakRandomness => "weak_randomness",
            IastVulnerabilityType.HardcodedSecret => "hardcoded_secret",
            IastVulnerabilityType.XContentTypeHeaderMissing => "xcontenttype_header_missing",
            IastVulnerabilityType.TrustBoundaryViolation => "trust_boundary_violation",
            IastVulnerabilityType.HstsHeaderMissing => "hsts_header_missing",
            IastVulnerabilityType.HeaderInjection => "header_injection",
            IastVulnerabilityType.StackTraceLeak => "stacktrace_leak",
            IastVulnerabilityType.NoSqlMongoDbInjection => "nosql_mongodb_injection",
            IastVulnerabilityType.XPathInjection => "xpath_injection",
            IastVulnerabilityType.ReflectionInjection => "reflection_injection",
            IastVulnerabilityType.InsecureAuthProtocol => "insecure_auth_protocol",
            IastVulnerabilityType.Xss => "xss",
            IastVulnerabilityType.DirectoryListingLeak => "directory_listing_leak",
            IastVulnerabilityType.SessionTimeout => "session_timeout",
            IastVulnerabilityType.EmailHtmlInjection => "email_html_injection",
            IastVulnerabilityType.None => raiseException ? throw new System.Exception($"Undefined vulnerability name for value {vulnerability}.") : "none",
            _ => throw new System.Exception($"Undefined vulnerability name for value {vulnerability}."),
        };

    private static string? GetTag(IastSourceType source)
    {
        return SourceTypeUtils.GetAsTag((SourceType)source);
    }

    public static bool Enabled()
        => _verbosityLevel >= IastMetricsVerbosityLevel.Information;

    public static bool EnabledDebug()
        => _verbosityLevel == IastMetricsVerbosityLevel.Debug;

    public void AddExecutedSink(IastVulnerabilityType type)
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

    public void AddExecutedSource(IastSourceType type)
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

    public void AddSupressedVulnerability(IastVulnerabilityType type)
    {
        if (_verbosityLevel >= IastMetricsVerbosityLevel.Information)
        {
            lock (_metricsLock)
            {
                _supressedVulnerabilities[(int)type]++;
            }

            TelemetryFactory.Metrics.RecordCountIastSuppressedVulnerabilities(type);
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
                    tags.SetMetric(_executedSourceTags[i], _executedSources[i]);
                }
            }

            for (int i = 0; i < _executedSinks.Length; i++)
            {
                if (_executedSinks[i] > 0)
                {
                    tags.SetMetric(_executedSinkTags[i], _executedSinks[i]);
                }
            }

            for (int i = 0; i < _supressedVulnerabilities.Length; i++)
            {
                if (_supressedVulnerabilities[i] > 0)
                {
                    tags.SetMetric(_supressedVulnerabilityTags[i], _executedSinks[i]);
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

#if NETCOREAPP3_0_OR_GREATER
        Array.Fill(_executedSources, 0);
        Array.Fill(_executedSinks, 0);
        Array.Fill(_supressedVulnerabilities, 0);
#else
        for (int i = 0; i < _executedSources.Length; i++)
        {
            _executedSources[i] = 0;
        }

        for (int i = 0; i < _executedSinks.Length; i++)
        {
            _executedSinks[i] = 0;
        }

        for (int i = 0; i < _supressedVulnerabilities.Length; i++)
        {
            _supressedVulnerabilities[i] = 0;
        }
#endif
    }
}
