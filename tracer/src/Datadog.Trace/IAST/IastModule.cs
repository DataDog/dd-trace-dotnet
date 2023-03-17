// <copyright file="IastModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Settings;

namespace Datadog.Trace.Iast;

internal static class IastModule
{
    private const string OperationNameWeakHash = "weak_hashing";
    private const string OperationNameWeakCipher = "weak_cipher";
    private const string OperationNameSqlInjection = "sql_injection";
    private static IastSettings iastSettings = Iast.Instance.Settings;

    public static Scope? OnSqlQuery(string query, IntegrationId integrationId)
    {
        return GetScope(query, integrationId, VulnerabilityTypeName.SqlInjection, OperationNameSqlInjection, true);
    }

    public static Scope? OnCipherAlgorithm(Type type, IntegrationId integrationId)
    {
        var algorithm = type.BaseType?.Name;

        if (algorithm is null || !InvalidCipherAlgorithm(type, algorithm))
        {
            return null;
        }

        return GetScope(algorithm, integrationId, VulnerabilityTypeName.WeakCipher, OperationNameWeakCipher);
    }

    public static Scope? OnHashingAlgorithm(string? algorithm, IntegrationId integrationId)
    {
        if (algorithm == null || !InvalidHashAlgorithm(algorithm))
        {
            return null;
        }

        return GetScope(algorithm, integrationId, VulnerabilityTypeName.WeakHash, OperationNameWeakHash);
    }

    public static IastRequestContext? GetIastContext()
    {
        if (!iastSettings.Enabled)
        {
            // integration disabled, don't create a scope, skip this span
            return null;
        }

        var currentSpan = (Tracer.Instance.ActiveScope as Scope)?.Span;
        var traceContext = currentSpan?.Context?.TraceContext;
        return traceContext?.IastRequestContext;
    }

    private static Scope? GetScope(string evidenceValue, IntegrationId integrationId, string vulnerabilityType, string operationName, bool taintedFromEvidenceRequired = false)
    {
        var tracer = Tracer.Instance;
        if (!iastSettings.Enabled || !tracer.Settings.IsIntegrationEnabled(integrationId))
        {
            // integration disabled, don't create a scope, skip this span
            return null;
        }

        var currentSpan = (tracer.ActiveScope as Scope)?.Span;
        var traceContext = currentSpan?.Context?.TraceContext;
        var isRequest = traceContext?.RootSpan?.Type == SpanTypes.Web;

        // We do not have, for now, tainted objects in console apps, so further checking is not neccessary.
        if (!isRequest && vulnerabilityType == VulnerabilityTypeName.SqlInjection)
        {
            return null;
        }

        TaintedObject? tainted = null;
        if (taintedFromEvidenceRequired)
        {
            tainted = traceContext?.IastRequestContext?.GetTainted(evidenceValue);
            if (tainted is null)
            {
                return null;
            }
        }

        if (isRequest && traceContext?.IastRequestContext?.AddVulnerabilitiesAllowed() != true)
        {
            // we are inside a request but we don't accept more vulnerabilities or IastRequestContext is null, which means that iast is
            // not activated for this particular request
            return null;
        }

        var frameInfo = StackWalker.GetFrame();

        if (!frameInfo.IsValid)
        {
            return null;
        }

        // Sometimes we do not have the file/line but we have the method/class.
        var filename = frameInfo.StackFrame?.GetFileName();
        var vulnerability = new Vulnerability(vulnerabilityType, new Location(filename ?? GetMethodName(frameInfo.StackFrame), filename != null ? frameInfo.StackFrame?.GetFileLineNumber() : null, currentSpan?.SpanId), new Evidence(evidenceValue, tainted?.Ranges));

        if (!iastSettings.DeduplicationEnabled || HashBasedDeduplication.Instance.Add(vulnerability))
        {
            if (isRequest)
            {
                traceContext?.IastRequestContext.AddVulnerability(vulnerability);
                return null;
            }
            else
            {
                return AddVulnerabilityAsSingleSpan(tracer, integrationId, operationName, vulnerability);
            }
        }

        return null;
    }

    private static Scope? AddVulnerabilityAsSingleSpan(Tracer tracer, IntegrationId integrationId, string operationName, Vulnerability vulnerability)
    {
        // we either are not in a request or the distributed tracer returned a scope that cannot be casted to Scope and we cannot access the root span.
        var batch = new VulnerabilityBatch();
        batch.Add(vulnerability);

        var tags = new IastTags()
        {
            IastJson = batch.ToString(),
            IastEnabled = "1"
        };

        var scope = tracer.StartActiveInternal(operationName, tags: tags);
        scope.Span.Type = SpanTypes.IastVulnerability;
        tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(integrationId);
        return scope;
    }

    private static string? GetMethodName(StackFrame? frame)
    {
        var method = frame?.GetMethod();
        var declaringType = method?.DeclaringType;
        var namespaceName = declaringType?.Namespace;
        var typeName = declaringType?.Name;
        var methodName = method?.Name;

        if (methodName == null || typeName == null || namespaceName == null)
        {
            return null;
        }

        return $"{namespaceName}.{typeName}::{methodName}";
    }

    private static bool InvalidHashAlgorithm(string algorithm)
    {
        foreach (var weakHashAlgorithm in iastSettings.WeakHashAlgorithmsArray)
        {
            if (string.Equals(algorithm, weakHashAlgorithm, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool InvalidCipherAlgorithm(Type type, string algorithm)
    {
#if !NETFRAMEWORK
        if (ProviderValid(type.Name))
        {
            return false;
        }
#endif
        foreach (var weakCipherAlgorithm in iastSettings.WeakCipherAlgorithmsArray)
        {
            if (string.Equals(algorithm, weakCipherAlgorithm, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ProviderValid(string name)
        => name switch
        {
            // TripleDESCryptoServiceProvider is a SymetricAlgorithm that internally creates a TripleDES instance, which is also a weak SymmetricAlgorithm. In order to avoid launching two spans for a single vulnerability,
            // we skip the one that would be launched when instantiating the TripleDESCryptoServiceProvider class.
            "TripleDESCryptoServiceProvider" => true,
            _ => string.Equals(FrameworkDescription.Instance.OSPlatform, OSPlatformName.Linux, StringComparison.Ordinal) && name.EndsWith("provider", StringComparison.OrdinalIgnoreCase)
        };
}
