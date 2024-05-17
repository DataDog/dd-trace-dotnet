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
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Aspects.System;
using Datadog.Trace.Iast.Propagation;
using Datadog.Trace.Iast.SensitiveData;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Logging;
using static Datadog.Trace.Telemetry.Metrics.MetricTags;

namespace Datadog.Trace.Iast;

internal static partial class IastModule
{
    public const string HeaderInjectionEvidenceSeparator = ": ";
    private const string OperationNameStackTraceLeak = "stacktrace_leak";
    private const string OperationNameWeakHash = "weak_hashing";
    private const string OperationNameWeakCipher = "weak_cipher";
    private const string OperationNameSqlInjection = "sql_injection";
    private const string OperationNameNoSqlMongoDbInjection = "nosql_mongodb_injection";
    private const string OperationNameCommandInjection = "command_injection";
    private const string OperationNamePathTraversal = "path_traversal";
    private const string OperationNameLdapInjection = "ldap_injection";
    private const string OperationNameSsrf = "ssrf";
    private const string OperationNameWeakRandomness = "weak_randomness";
    private const string OperationNameHardcodedSecret = "hardcoded_secret";
    private const string OperationNameTrustBoundaryViolation = "trust_boundary_violation";
    private const string OperationNameUnvalidatedRedirect = "unvalidated_redirect";
    private const string OperationNameHeaderInjection = "header_injection";
    private const string OperationNameXPathInjection = "xpath_injection";
    private const string OperationNameReflectionInjection = "reflection_injection";
    private const string OperationNameXss = "xss";
    private const string OperationNameSessionTimeout = "session_timeout";
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
        if (Iast.Instance.Settings.Enabled && evidence != null && OnUnvalidatedRedirect(evidence, IntegrationId.UnvalidatedRedirect).VulnerabilityAdded)
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

        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
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
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

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
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

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
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

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
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

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
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

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
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

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

    public static IastModuleResponse OnNoSqlMongoDbQuery(string query, IntegrationId integrationId)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

        try
        {
            OnExecutedSinkTelemetry(IastInstrumentedSinks.NoSqlMongoDbInjection);
            return GetScope(query, integrationId, VulnerabilityTypeName.NoSqlMongoDbInjection, OperationNameNoSqlMongoDbInjection, Always);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for MongoDb NoSql injection.");
            return IastModuleResponse.Empty;
        }
    }

    public static IastModuleResponse OnCommandInjection(string file, string argumentLine, Collection<string> argumentList, IntegrationId integrationId)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

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

    public static IastModuleResponse OnReflectionInjection(string param, IntegrationId integrationId)
    {
        try
        {
            OnExecutedSinkTelemetry(IastInstrumentedSinks.ReflectionInjection);
            return GetScope(param, integrationId, VulnerabilityTypeName.ReflectionInjection, OperationNameReflectionInjection, Always);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for reflection injection.");
            return IastModuleResponse.Empty;
        }
    }

    internal static EvidenceRedactor? CreateRedactor(IastSettings settings)
    {
        var timeout = TimeSpan.FromMilliseconds(settings.RegexTimeout);

        if (timeout.TotalMilliseconds == 0)
        {
            timeout = Regex.InfiniteMatchTimeout;
        }

        return settings.RedactionEnabled ? new EvidenceRedactor(settings.RedactionKeysRegex, settings.RedactionValuesRegex, timeout) : null;
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
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

        OnExecutedSinkTelemetry(IastInstrumentedSinks.InsecureCookie);
        // We provide a hash value for the vulnerability instead of calculating one, following the agreed conventions
        return AddWebVulnerability(cookieName, integrationId, VulnerabilityTypeName.InsecureCookie, (VulnerabilityTypeName.InsecureCookie.ToString() + ":" + cookieName).GetStaticHashCode());
    }

    public static IastModuleResponse OnNoHttpOnlyCookie(IntegrationId integrationId, string cookieName)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

        OnExecutedSinkTelemetry(IastInstrumentedSinks.NoHttpOnlyCookie);
        // We provide a hash value for the vulnerability instead of calculating one, following the agreed conventions
        return AddWebVulnerability(cookieName, integrationId, VulnerabilityTypeName.NoHttpOnlyCookie, (VulnerabilityTypeName.NoHttpOnlyCookie.ToString() + ":" + cookieName).GetStaticHashCode());
    }

    public static IastModuleResponse OnNoSamesiteCookie(IntegrationId integrationId, string cookieName)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

        OnExecutedSinkTelemetry(IastInstrumentedSinks.NoSameSiteCookie);
        // We provide a hash value for the vulnerability instead of calculating one, following the agreed conventions
        return AddWebVulnerability(cookieName, integrationId, VulnerabilityTypeName.NoSameSiteCookie, (VulnerabilityTypeName.NoSameSiteCookie.ToString() + ":" + cookieName).GetStaticHashCode());
    }

    public static void OnHardcodedSecret(Vulnerability vulnerability)
    {
        if (Iast.Instance.Settings.Enabled)
        {
            // We provide a hash value for the vulnerability instead of calculating one, following the agreed conventions
            AddVulnerabilityAsSingleSpan(Tracer.Instance, IntegrationId.HardcodedSecret, OperationNameHardcodedSecret, vulnerability).SingleSpan?.Dispose();
        }
    }

    public static void OnHardcodedSecret(List<Vulnerability> vulnerabilities)
    {
        if (Iast.Instance.Settings.Enabled)
        {
            // We provide a hash value for the vulnerability instead of calculating one, following the agreed conventions
            AddVulnerabilityAsSingleSpan(Tracer.Instance, IntegrationId.HardcodedSecret, OperationNameHardcodedSecret, vulnerabilities).SingleSpan?.Dispose();
        }
    }

    public static IastModuleResponse OnInsecureAuthProtocol(string authHeader, IntegrationId integrationId)
    {
        OnExecutedSinkTelemetry(IastInstrumentedSinks.InsecureAuthProtocol);
        // We provide a hash value for the vulnerability instead of calculating one, following the agreed conventions
        return AddWebVulnerability(authHeader, integrationId, VulnerabilityTypeName.InsecureAuthProtocol, (VulnerabilityTypeName.InsecureAuthProtocol + ':' + authHeader).GetStaticHashCode());
    }

    public static void OnDirectoryListingLeak(string methodName)
    {
        if (!Iast.Instance.Settings.Enabled) { return; }

        var vulnerability = new Vulnerability(
            VulnerabilityTypeName.DirectoryListingLeak,
            VulnerabilityTypeName.DirectoryListingLeak.GetStaticHashCode(),
            GetLocation(),
            new Evidence($"Directory listing is configured with: {methodName}"),
            IntegrationId.DirectoryListingLeak);

        AddVulnerabilityAsSingleSpan(Tracer.Instance, IntegrationId.DirectoryListingLeak, OperationNameHardcodedSecret, vulnerability).SingleSpan?.Dispose();
    }

    public static void OnSessionTimeout(string methodName, TimeSpan value)
    {
        if (!Iast.Instance.Settings.Enabled) { return; }

        // Not a vulnerability if the timeout is less than 30 minutes
        if (value.TotalMinutes < 30) { return; }

        var vulnerability = new Vulnerability(
            VulnerabilityTypeName.SessionTimeout,
            (VulnerabilityTypeName.SessionTimeout + ':' + methodName + ':' + value.TotalMinutes).GetStaticHashCode(),
            GetLocation(),
            new Evidence($"Session idle timeout is configured with: {methodName}, with a value of {value.TotalMinutes} minutes"),
            IntegrationId.SessionTimeout);

        AddVulnerabilityAsSingleSpan(Tracer.Instance, IntegrationId.SessionTimeout, OperationNameSessionTimeout, vulnerability).SingleSpan?.Dispose();
    }

    public static IastModuleResponse OnCipherAlgorithm(Type type, IntegrationId integrationId)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

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
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

        OnExecutedSinkTelemetry(IastInstrumentedSinks.StackTraceLeak);
        var evidence = $"{ex.Source},{ex.GetType().Name}";
        // We report the stack of the exception instead of the current stack
        var stack = new StackTrace(ex, true);
        return GetScope(evidence, integrationId, VulnerabilityTypeName.StackTraceLeak, OperationNameStackTraceLeak, externalStack: stack);
    }

    public static IastModuleResponse OnHashingAlgorithm(string? algorithm, IntegrationId integrationId)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

        OnExecutedSinkTelemetry(IastInstrumentedSinks.WeakHash);
        if (algorithm == null || !InvalidHashAlgorithm(algorithm))
        {
            return IastModuleResponse.Empty;
        }

        return GetScope(algorithm, integrationId, VulnerabilityTypeName.WeakHash, OperationNameWeakHash);
    }

    public static IastModuleResponse OnXss(string? text)
    {
        try
        {
            if (!Iast.Instance.Settings.Enabled || string.IsNullOrEmpty(text))
            {
                return IastModuleResponse.Empty;
            }

            OnExecutedSinkTelemetry(IastInstrumentedSinks.Xss);
            return GetScope(text!, IntegrationId.Xss, VulnerabilityTypeName.Xss, OperationNameXss, Always, exclusionSecureMarks: SecureMarks.Xss);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for XSS.");
            return IastModuleResponse.Empty;
        }
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
        return new VulnerabilityBatch(iastSettings.TruncationMaxValueLength, EvidenceRedactorLazy.Value);
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

    private static IastModuleResponse GetScope(string evidenceValue, IntegrationId integrationId, string vulnerabilityType, string operationName, Func<TaintedObject, bool>? taintValidator = null, bool addLocation = true, int? hash = null, StackTrace? externalStack = null, SecureMarks exclusionSecureMarks = SecureMarks.None)
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

        // Contains at least one range that is not not safe (when analyzing a vulnerability that can have secure marks)
        if (exclusionSecureMarks != SecureMarks.None && !Ranges.ContainsUnsafeRange(tainted?.Ranges))
        {
            return IastModuleResponse.Empty;
        }

        var location = addLocation ? GetLocation(externalStack, currentSpan?.SpanId) : null;
        if (addLocation && location is null)
        {
            return IastModuleResponse.Empty;
        }

        var unsafeRanges = Ranges.UnsafeRanges(tainted?.Ranges, exclusionSecureMarks);
        var vulnerability = (hash is null) ?
            new Vulnerability(vulnerabilityType, location, new Evidence(evidenceValue, unsafeRanges), integrationId) :
            new Vulnerability(vulnerabilityType, (int)hash, location, new Evidence(evidenceValue, unsafeRanges), integrationId);

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

    private static Location? GetLocation(StackTrace? externalStack = null, ulong? currentSpanId = null)
    {
        var frameInfo = StackWalker.GetFrame(externalStack);
        if (!frameInfo.IsValid)
        {
            return null;
        }

        return new Location(frameInfo.StackFrame, currentSpanId);
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
            _ => (string.Equals(FrameworkDescription.Instance.OSPlatform, OSPlatformName.Linux, StringComparison.Ordinal) || string.Equals(FrameworkDescription.Instance.OSPlatform, OSPlatformName.MacOS, StringComparison.Ordinal)) && name.EndsWith("provider", StringComparison.OrdinalIgnoreCase)
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
        if (!Iast.Instance.Settings.Enabled)
        {
            return;
        }

        var evidence = StringAspects.Concat(headerName, HeaderInjectionEvidenceSeparator, headerValue);
        var hash = ("HEADER_INJECTION:" + headerName).GetStaticHashCode();
        GetScope(evidence, integrationId, VulnerabilityTypeName.HeaderInjection, OperationNameHeaderInjection, Always, false, hash);
    }

    internal static IastModuleResponse OnXpathInjection(string xpath)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

        try
        {
            OnExecutedSinkTelemetry(IastInstrumentedSinks.XPathInjection);
            return GetScope(xpath, IntegrationId.XpathInjection, VulnerabilityTypeName.XPathInjection, OperationNameXPathInjection, Always);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for xpath injection.");
            return IastModuleResponse.Empty;
        }
    }
}
