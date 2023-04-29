// <copyright file="IastModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Process;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Propagation;
using Datadog.Trace.Iast.SensitiveData;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Logging;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.Iast;

internal static class IastModule
{
    private const string OperationNameWeakHash = "weak_hashing";
    private const string OperationNameWeakCipher = "weak_cipher";
    private const string OperationNameSqlInjection = "sql_injection";
    private const string OperationNameCommandInjection = "command_injection";
    private const string OperationNamePathTraversal = "path_traversal";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(IastModule));
    private static readonly Lazy<EvidenceRedactor?> EvidenceRedactorLazy;
    private static IastSettings iastSettings = Iast.Instance.Settings;

    static IastModule()
    {
        EvidenceRedactorLazy = new(() => CreateRedactor(iastSettings));
    }

    public static Scope? OnPathTraversal(string evidence)
    {
        try
        {
            return GetScope(evidence, IntegrationId.PathTraversal, VulnerabilityTypeName.PathTraversal, OperationNamePathTraversal, true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for path traversal.");
            return null;
        }
    }

    public static Scope? OnSqlQuery(string query, IntegrationId integrationId)
    {
        try
        {
            return GetScope(query, integrationId, VulnerabilityTypeName.SqlInjection, OperationNameSqlInjection, true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for Sql injection.");
            return null;
        }
    }

    public static Scope? OnCommandInjection(string file, string argumentLine, Collection<string> argumentList, IntegrationId integrationId)
    {
        try
        {
            var evidence = BuildCommandInjectionEvidence(file, argumentLine, argumentList);
            return string.IsNullOrEmpty(evidence) ? null : GetScope(evidence, integrationId, VulnerabilityTypeName.CommandInjection, OperationNameCommandInjection, true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for command injection.");
            return null;
        }
    }

    internal static EvidenceRedactor? CreateRedactor(IastSettings settings)
    {
        return settings.RedactionEnabled ? new EvidenceRedactor(settings.RedactionKeysRegex, settings.RedactionValuesRegex, TimeSpan.FromMilliseconds(settings.RedactionRegexTimeout)) : null;
    }

    private static string BuildCommandInjectionEvidence(string file, string argumentLine, Collection<string>? argumentList)
    {
        if (string.IsNullOrEmpty(file))
        {
            return string.Empty;
        }

        if ((argumentList is not null) && (argumentList.Count > 0))
        {
            var joinList = StringModuleImpl.OnStringJoin(string.Join(" ", argumentList), argumentList);
            var fileWithSpace = file + " ";
            _ = PropagationModuleImpl.PropagateTaint(file, fileWithSpace);
            return StringModuleImpl.OnStringConcat(fileWithSpace, joinList, string.Concat(fileWithSpace, joinList));
        }

        if (!string.IsNullOrEmpty(argumentLine))
        {
            var fileWithSpace = file + " ";
            _ = PropagationModuleImpl.PropagateTaint(file, fileWithSpace);
            return StringModuleImpl.OnStringConcat(fileWithSpace, argumentLine, string.Concat(fileWithSpace, argumentLine));
        }
        else
        {
            return file;
        }
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
        if (!isRequest && taintedFromEvidenceRequired)
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
        var vulnerability = new Vulnerability(
            vulnerabilityType,
            new Location(
                stackFile: filename,
                methodName: string.IsNullOrEmpty(filename) ? frameInfo.StackFrame?.GetMethod()?.Name : null,
                line: !string.IsNullOrEmpty(filename) ? frameInfo.StackFrame?.GetFileLineNumber() : null,
                spanId: currentSpan?.SpanId,
                methodTypeName: string.IsNullOrEmpty(filename) ? GetMethodTypeName(frameInfo.StackFrame) : null),
            new Evidence(evidenceValue, tainted?.Ranges),
            integrationId);

        if (!iastSettings.DeduplicationEnabled || HashBasedDeduplication.Instance.Add(vulnerability))
        {
            if (isRequest)
            {
                traceContext?.IastRequestContext?.AddVulnerability(vulnerability);
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
        var batch = new VulnerabilityBatch(EvidenceRedactorLazy.Value);
        batch.Add(vulnerability);

        var tags = new IastTags()
        {
            IastJson = batch.ToJson(),
            IastEnabled = "1"
        };

        var scope = tracer.StartActiveInternal(operationName, tags: tags);
        scope.Span.Type = SpanTypes.IastVulnerability;
        tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(integrationId);
        return scope;
    }

    private static string? GetMethodTypeName(StackFrame? frame)
    {
        return frame?.GetMethod()?.DeclaringType?.FullName;
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
