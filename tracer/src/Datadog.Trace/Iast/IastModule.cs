// <copyright file="IastModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Aspects.System;
using Datadog.Trace.Iast.Propagation;
using Datadog.Trace.Iast.SensitiveData;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Logging;
using static Datadog.Trace.Telemetry.Metrics.MetricTags;

namespace Datadog.Trace.Iast;

internal static class IastModule
{
    public const string HeaderInjectionEvidenceSeparator = ": ";
    private const string OperationNameStackTraceLeak = "stacktrace_leak";
    private const string OperationNameWeakHash = "weak_hashing";
    private const string OperationNameWeakCipher = "weak_cipher";
    private const string OperationNameSqlInjection = "sql_injection";
    private const string OperationNameCommandInjection = "command_injection";
    private const string OperationNamePathTraversal = "path_traversal";
    private const string OperationNameLdapInjection = "ldap_injection";
    private const string OperationNameSsrf = "ssrf";
    private const string OperationNameWeakRandomness = "weak_randomness";
    private const string OperationNameHardcodedSecret = "hardcoded_secret";
    private const string OperationNameTrustBoundaryViolation = "trust_boundary_violation";
    private const string OperationNameUnvalidatedRedirect = "unvalidated_redirect";
    private const string OperationNameHeaderInjection = "header_injection";
    private const string ReferrerHeaderName = "Referrer";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(IastModule));
    private static readonly Lazy<EvidenceRedactor?> EvidenceRedactorLazy;
    private static readonly Func<TaintedObject, bool> Always = (x) => true;
    private static IastSettings iastSettings = Iast.Instance.Settings;

    static IastModule()
    {
        EvidenceRedactorLazy = new(() => CreateRedactor(iastSettings));
    }

    internal static string? OnUnvalidatedRedirect(string? evidence)
    {
        if (evidence != null && OnUnvalidatedRedirect(evidence, IntegrationId.UnvalidatedRedirect).VulnerabilityAdded)
        {
            return new string(evidence.ToCharArray());
        }

        return evidence;
    }

    internal static IastModuleResponse OnUnvalidatedRedirect(string evidence, IntegrationId integrationId)
    {
        bool HasInvalidOrigin(TaintedObject tainted)
        {
            try
            {
                foreach (var range in tainted.Ranges)
                {
                    if (range.Source is null) { continue; }
                    var origin = range.Source.Origin;
                    // TODO: reenable when SourceTypeName.Database gets defined -> if (origin == SourceType.Database) { continue; }
                    if (origin == SourceType.RequestPath) { continue; }
                    if (origin == SourceType.RequestHeaderValue && range.Source.Name == "Host") { continue; }

                    return true;
                }

                return IsRefererHeader(tainted);
            }
            catch (Exception err)
            {
                Log.Error(err, "Error while checking for UnvalidatedRedirect tainted origins.");
            }

            return true;
        }

        bool IsRefererHeader(TaintedObject tainted)
        {
            foreach (var range in tainted.Ranges)
            {
                if (range.Source is null) { return false; }
                var origin = range.Source.Origin;
                if (origin != SourceType.RequestHeaderValue || !ReferrerHeaderName.Equals(range.Source.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        try
        {
            OnExecutedSinkTelemetry(IastInstrumentedSinks.UnvalidatedRedirect);
            return GetScope(evidence, integrationId, VulnerabilityTypeName.UnvalidatedRedirect, OperationNameUnvalidatedRedirect, HasInvalidOrigin);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for Unvalidated Redirect.");
            return IastModuleResponse.Empty;
        }
    }

    internal static IastModuleResponse OnTrustBoundaryViolation(string name)
    {
        try
        {
            OnExecutedSinkTelemetry(IastInstrumentedSinks.TrustBoundaryViolation);
            return GetScope(name, IntegrationId.TrustBoundaryViolation, VulnerabilityTypeName.TrustBoundaryViolation, OperationNameTrustBoundaryViolation, Always);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for TrustBoundaryViolation.");
            return IastModuleResponse.Empty;
        }
    }

    internal static IastModuleResponse OnLdapInjection(string evidence)
    {
        try
        {
            OnExecutedSinkTelemetry(IastInstrumentedSinks.LdapInjection);
            return GetScope(evidence, IntegrationId.Ldap, VulnerabilityTypeName.LdapInjection, OperationNameLdapInjection, Always);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for ldap injection.");
            return IastModuleResponse.Empty;
        }
    }

    internal static IastModuleResponse OnSSRF(string evidence)
    {
        try
        {
            OnExecutedSinkTelemetry(IastInstrumentedSinks.Ssrf);
            return GetScope(evidence, IntegrationId.Ssrf, VulnerabilityTypeName.Ssrf, OperationNameSsrf, Always);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for SSRF.");
            return IastModuleResponse.Empty;
        }
    }

    internal static IastModuleResponse OnWeakRandomness(string evidence)
    {
        try
        {
            OnExecutedSinkTelemetry(IastInstrumentedSinks.WeakRandomness);
            return GetScope(evidence, IntegrationId.SystemRandom, VulnerabilityTypeName.WeakRandomness, OperationNameWeakRandomness);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for WeakRandomness.");
            return IastModuleResponse.Empty;
        }
    }

    public static IastModuleResponse OnPathTraversal(string evidence)
    {
        try
        {
            OnExecutedSinkTelemetry(IastInstrumentedSinks.PathTraversal);
            return GetScope(evidence, IntegrationId.PathTraversal, VulnerabilityTypeName.PathTraversal, OperationNamePathTraversal, Always);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for path traversal.");
            return IastModuleResponse.Empty;
        }
    }

    public static IastModuleResponse OnSqlQuery(string query, IntegrationId integrationId)
    {
        try
        {
            OnExecutedSinkTelemetry(IastInstrumentedSinks.SqlInjection);
            return GetScope(query, integrationId, VulnerabilityTypeName.SqlInjection, OperationNameSqlInjection, Always);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for Sql injection.");
            return IastModuleResponse.Empty;
        }
    }

    public static IastModuleResponse OnCommandInjection(string file, string argumentLine, Collection<string> argumentList, IntegrationId integrationId)
    {
        try
        {
            OnExecutedSinkTelemetry(IastInstrumentedSinks.CommandInjection);
            var evidence = BuildCommandInjectionEvidence(file, argumentLine, argumentList);
            return string.IsNullOrEmpty(evidence) ? IastModuleResponse.Empty : GetScope(evidence, integrationId, VulnerabilityTypeName.CommandInjection, OperationNameCommandInjection, Always);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for command injection.");
            return IastModuleResponse.Empty;
        }
    }

    internal static EvidenceRedactor? CreateRedactor(IastSettings settings)
    {
        return settings.RedactionEnabled ? new EvidenceRedactor(settings.RedactionKeysRegex, settings.RedactionValuesRegex, TimeSpan.FromMilliseconds(settings.RegexTimeout)) : null;
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

    public static IastModuleResponse OnInsecureCookie(IntegrationId integrationId, string cookieName)
    {
        OnExecutedSinkTelemetry(IastInstrumentedSinks.InsecureCookie);
        // We provide a hash value for the vulnerability instead of calculating one, following the agreed conventions
        return AddWebVulnerability(cookieName, integrationId, VulnerabilityTypeName.InsecureCookie, (VulnerabilityTypeName.InsecureCookie.ToString() + ":" + cookieName).GetStaticHashCode());
    }

    public static IastModuleResponse OnNoHttpOnlyCookie(IntegrationId integrationId, string cookieName)
    {
        OnExecutedSinkTelemetry(IastInstrumentedSinks.NoHttpOnlyCookie);
        // We provide a hash value for the vulnerability instead of calculating one, following the agreed conventions
        return AddWebVulnerability(cookieName, integrationId, VulnerabilityTypeName.NoHttpOnlyCookie, (VulnerabilityTypeName.NoHttpOnlyCookie.ToString() + ":" + cookieName).GetStaticHashCode());
    }

    public static IastModuleResponse OnNoSamesiteCookie(IntegrationId integrationId, string cookieName)
    {
        OnExecutedSinkTelemetry(IastInstrumentedSinks.NoSameSiteCookie);
        // We provide a hash value for the vulnerability instead of calculating one, following the agreed conventions
        return AddWebVulnerability(cookieName, integrationId, VulnerabilityTypeName.NoSameSiteCookie, (VulnerabilityTypeName.NoSameSiteCookie.ToString() + ":" + cookieName).GetStaticHashCode());
    }

    public static void OnHardcodedSecret(Vulnerability vulnerability)
    {
        // We provide a hash value for the vulnerability instead of calculating one, following the agreed conventions
        AddVulnerabilityAsSingleSpan(Tracer.Instance, IntegrationId.HardcodedSecret, OperationNameHardcodedSecret, vulnerability).SingleSpan?.Dispose();
    }

    public static void OnHardcodedSecret(List<Vulnerability> vulnerabilities)
    {
        // We provide a hash value for the vulnerability instead of calculating one, following the agreed conventions
        AddVulnerabilityAsSingleSpan(Tracer.Instance, IntegrationId.HardcodedSecret, OperationNameHardcodedSecret, vulnerabilities).SingleSpan?.Dispose();
    }

    public static IastModuleResponse OnCipherAlgorithm(Type type, IntegrationId integrationId)
    {
        OnExecutedSinkTelemetry(IastInstrumentedSinks.WeakCipher);
        var algorithm = type.BaseType?.Name;

        if (algorithm is null || !InvalidCipherAlgorithm(type, algorithm))
        {
            return IastModuleResponse.Empty;
        }

        return GetScope(algorithm, integrationId, VulnerabilityTypeName.WeakCipher, OperationNameWeakCipher);
    }

    public static IastModuleResponse OnStackTraceLeak(Exception ex, IntegrationId integrationId)
    {
        OnExecutedSinkTelemetry(IastInstrumentedSinks.StackTraceLeak);
        var evidence = $"{ex.Source},{ex.GetType().Name}";
        // We report the stack of the exception instead of the current stack
        var stack = new StackTrace(ex, true);
        return GetScope(evidence, integrationId, VulnerabilityTypeName.StackTraceLeak, OperationNameStackTraceLeak, externalStack: stack);
    }

    public static IastModuleResponse OnHashingAlgorithm(string? algorithm, IntegrationId integrationId)
    {
        OnExecutedSinkTelemetry(IastInstrumentedSinks.WeakHash);
        if (algorithm == null || !InvalidHashAlgorithm(algorithm))
        {
            return IastModuleResponse.Empty;
        }

        return GetScope(algorithm, integrationId, VulnerabilityTypeName.WeakHash, OperationNameWeakHash);
    }

    internal static void OnExecutedPropagationTelemetry()
    {
        if (ExecutedTelemetryHelper.EnabledDebug())
        {
            GetIastContext()?.OnExecutedPropagationTelemetry();
        }
    }

    internal static void OnExecutedSourceTelemetry(IastInstrumentedSources source)
    {
        if (ExecutedTelemetryHelper.Enabled())
        {
            GetIastContext()?.OnExecutedSourceTelemetry(source);
        }
    }

    internal static void OnExecutedSinkTelemetry(IastInstrumentedSinks sink)
    {
        if (ExecutedTelemetryHelper.Enabled())
        {
            GetIastContext()?.OnExecutedSinkTelemetry(sink);
        }
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

    internal static VulnerabilityBatch GetVulnerabilityBatch()
    {
        return new VulnerabilityBatch(EvidenceRedactorLazy.Value);
    }

    // This method adds web vulnerabilities, with no location, only on web environments
    private static IastModuleResponse AddWebVulnerability(string? evidenceValue, IntegrationId integrationId, string vulnerabilityType, int hashId)
    {
        var tracer = Tracer.Instance;
        if (!iastSettings.Enabled || !tracer.Settings.IsIntegrationEnabled(integrationId))
        {
            // integration disabled, don't create a scope, skip this span
            return IastModuleResponse.Empty;
        }

        var currentSpan = (tracer.ActiveScope as Scope)?.Span;
        var traceContext = currentSpan?.Context?.TraceContext;

        if (traceContext?.IastRequestContext?.AddVulnerabilitiesAllowed() != true)
        {
            // we are inside a request but we don't accept more vulnerabilities or IastRequestContext is null, which means that iast is
            // not activated for this particular request
            return IastModuleResponse.Empty;
        }

        var vulnerability = new Vulnerability(
            vulnerabilityType,
            hashId,
            string.IsNullOrEmpty(evidenceValue) ? null : new Evidence(evidenceValue!, null),
            integrationId);

        if (!iastSettings.DeduplicationEnabled || HashBasedDeduplication.Instance.Add(vulnerability))
        {
            traceContext?.IastRequestContext?.AddVulnerability(vulnerability);
            return IastModuleResponse.Vulnerable;
        }

        return IastModuleResponse.Empty;
    }

    public static bool AddRequestVulnerabilitiesAllowed()
    {
        var currentSpan = (Tracer.Instance.ActiveScope as Scope)?.Span;
        var traceContext = currentSpan?.Context?.TraceContext;
        var isRequest = traceContext?.RootSpan?.Type == SpanTypes.Web;
        return isRequest && traceContext?.IastRequestContext?.AddVulnerabilitiesAllowed() == true;
    }

    private static IastModuleResponse GetScope(string evidenceValue, IntegrationId integrationId, string vulnerabilityType, string operationName, Func<TaintedObject, bool>? taintValidator = null, bool addLocation = true, int? hash = null, StackTrace? externalStack = null)
    {
        var tracer = Tracer.Instance;
        if (!iastSettings.Enabled || !tracer.Settings.IsIntegrationEnabled(integrationId))
        {
            // integration disabled, don't create a scope, skip this span
            return IastModuleResponse.Empty;
        }

        var scope = tracer.ActiveScope as Scope;
        var currentSpan = scope?.Span;
        var traceContext = currentSpan?.Context?.TraceContext;
        var isRequest = traceContext?.RootSpan?.Type == SpanTypes.Web;

        // We do not have, for now, tainted objects in console apps, so further checking is not neccessary.
        if (!isRequest && taintValidator != null)
        {
            return IastModuleResponse.Empty;
        }

        if (isRequest && traceContext?.IastRequestContext?.AddVulnerabilitiesAllowed() != true)
        {
            // we are inside a request but we don't accept more vulnerabilities or IastRequestContext is null, which means that iast is
            // not activated for this particular request
            return IastModuleResponse.Empty;
        }

        TaintedObject? tainted = null;
        if (taintValidator != null)
        {
            tainted = traceContext?.IastRequestContext?.GetTainted(evidenceValue);
            if (tainted is null || !taintValidator(tainted))
            {
                return IastModuleResponse.Empty;
            }
        }

        Location? location = null;

        if (addLocation)
        {
            var frameInfo = StackWalker.GetFrame(externalStack);

            if (!frameInfo.IsValid)
            {
                return IastModuleResponse.Empty;
            }

            // Sometimes we do not have the file/line but we have the method/class.
            var stackFrame = frameInfo.StackFrame;
            var filename = stackFrame?.GetFileName();
            var line = string.IsNullOrEmpty(filename) ? 0 : (stackFrame?.GetFileLineNumber() ?? 0);

            location = new Location(
                    stackFile: filename,
                    methodName: string.IsNullOrEmpty(filename) ? stackFrame?.GetMethod()?.Name : null,
                    line: line > 0 ? line : null,
                    spanId: currentSpan?.SpanId,
                    methodTypeName: string.IsNullOrEmpty(filename) ? GetMethodTypeName(stackFrame) : null);
        }

        var vulnerability = (hash is null) ?
            new Vulnerability(vulnerabilityType, location, new Evidence(evidenceValue, tainted?.Ranges), integrationId) :
            new Vulnerability(vulnerabilityType, (int)hash, location, new Evidence(evidenceValue, tainted?.Ranges), integrationId);

        if (!iastSettings.DeduplicationEnabled || HashBasedDeduplication.Instance.Add(vulnerability))
        {
            if (isRequest)
            {
                traceContext?.IastRequestContext?.AddVulnerability(vulnerability);
                return IastModuleResponse.Vulnerable;
            }
            else
            {
                return AddVulnerabilityAsSingleSpan(tracer, integrationId, operationName, vulnerability);
            }
        }

        return IastModuleResponse.Empty;
    }

    private static IastModuleResponse AddVulnerabilityAsSingleSpan(Tracer tracer, IntegrationId integrationId, string operationName, List<Vulnerability> vulnerabilities)
    {
        // we either are not in a request or the distributed tracer returned a scope that cannot be casted to Scope and we cannot access the root span.
        var batch = GetVulnerabilityBatch();
        foreach (var vulnerability in vulnerabilities)
        {
            batch.Add(vulnerability);
        }

        return AddVulnerabilityAsSingleSpan(tracer, integrationId, operationName, batch.ToJson());
    }

    private static IastModuleResponse AddVulnerabilityAsSingleSpan(Tracer tracer, IntegrationId integrationId, string operationName, Vulnerability vulnerability)
    {
        // we either are not in a request or the distributed tracer returned a scope that cannot be casted to Scope and we cannot access the root span.
        var batch = GetVulnerabilityBatch();
        batch.Add(vulnerability);
        return AddVulnerabilityAsSingleSpan(tracer, integrationId, operationName, batch.ToJson());
    }

    private static IastModuleResponse AddVulnerabilityAsSingleSpan(Tracer tracer, IntegrationId integrationId, string operationName, string vulnsJson)
    {
        var tags = new IastTags()
        {
            IastJson = vulnsJson,
            IastEnabled = "1"
        };

        var scope = tracer.StartActiveInternal(operationName, tags: tags);
        scope.Span.Type = SpanTypes.IastVulnerability;
        tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(integrationId);
        return new IastModuleResponse(scope);
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

    // Evidence: If the customer application is setting the header with an invalid value, the evidence value should be the value that is set. If the header is missing, the evidence should not be sent.
    // hash('XCONTENTTYPE_HEADER_MISSING:<service-name>')
    internal static IastModuleResponse OnXContentTypeOptionsHeaderMissing(IntegrationId integrationId, string headerValue, string serviceName)
    {
        string? evidence = string.IsNullOrEmpty(headerValue) ? null : headerValue;
        return AddWebVulnerability(evidence, integrationId, VulnerabilityTypeName.XContentTypeHeaderMissing, (VulnerabilityTypeName.XContentTypeHeaderMissing + ":" + serviceName).GetStaticHashCode());
    }

    internal static IastModuleResponse OnStrictTransportSecurityHeaderMissing(IntegrationId integrationId, string serviceName)
    {
        return AddWebVulnerability(null, integrationId, VulnerabilityTypeName.HstsHeaderMissing, (VulnerabilityTypeName.HstsHeaderMissing + ":" + serviceName).GetStaticHashCode());
    }

    internal static void OnHeaderInjection(IntegrationId integrationId, string headerName, string headerValue)
    {
        var evidence = StringAspects.Concat(headerName, HeaderInjectionEvidenceSeparator, headerValue);
        var hash = ("HEADER_INJECTION:" + headerName).GetStaticHashCode();
        GetScope(evidence, integrationId, VulnerabilityTypeName.HeaderInjection, OperationNameHeaderInjection, Always, false, hash);
    }
}
