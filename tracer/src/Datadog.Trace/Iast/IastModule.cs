// <copyright file="IastModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Web;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast.Aspects;
using Datadog.Trace.Iast.Aspects.System;
using Datadog.Trace.Iast.Propagation;
using Datadog.Trace.Iast.SensitiveData;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Iast;

internal static partial class IastModule
{
    public const string HeaderInjectionEvidenceSeparator = ": ";
    private const string ReferrerHeaderName = "Referrer";
    private const int VulnerabilityStatsRouteLimit = 4096;
    internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(IastModule));
    private static readonly IastSettings IastSettings = Iast.Instance.Settings;
    private static readonly Func<TaintedObject, bool> Always = (x) => true;
    private static readonly DbRecordManager DbRecords = new DbRecordManager(IastSettings);
    private static readonly SourceType[] _dbSources = [SourceType.SqlRowValue];
    private static readonly HashSet<int> LoggedAspectExceptionMessages = [];
    private static bool _showTimeoutExceptionError = true;
    private static ConcurrentDictionary<string, VulnerabilityStats> _vulnerabilityStats = new ConcurrentDictionary<string, VulnerabilityStats>();

    internal static void LogTimeoutError(RegexMatchTimeoutException err)
    {
        if (_showTimeoutExceptionError)
        {
            Log.Warning(err, "IAST Regex timed out when trying to match value against pattern {Pattern}.", err.Pattern);
            _showTimeoutExceptionError = false;
        }
        else
        {
            Log.Debug(err, "IAST Regex timed out when trying to match value against pattern {Pattern}.", err.Pattern);
        }
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
                    if (origin == SourceType.SqlRowValue) { continue; }
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
            if (!Iast.Instance.Settings.Enabled)
            {
                return IastModuleResponse.Empty;
            }

            return GetScope(evidence, integrationId, VulnerabilityTypeUtils.UnvalidatedRedirect, HasInvalidOrigin);
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
            if (!Iast.Instance.Settings.Enabled)
            {
                return IastModuleResponse.Empty;
            }

            return GetScope(name, IntegrationId.TrustBoundaryViolation, VulnerabilityTypeUtils.TrustBoundaryViolation, taintValidator: Always, safeSources: _dbSources);
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
            if (!Iast.Instance.Settings.Enabled)
            {
                return IastModuleResponse.Empty;
            }

            return GetScope(evidence, IntegrationId.Ldap, VulnerabilityTypeUtils.LdapInjection, taintValidator: Always, safeSources: _dbSources);
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
            return GetScope(evidence, IntegrationId.Ssrf, VulnerabilityTypeUtils.Ssrf, taintValidator: Always, safeSources: _dbSources, exclusionSecureMarks: SecureMarks.Ssrf);
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
            return GetScope(evidence, IntegrationId.SystemRandom, VulnerabilityTypeUtils.WeakRandomness);
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
            return GetScope(evidence, IntegrationId.PathTraversal, VulnerabilityTypeUtils.PathTraversal, taintValidator: Always, safeSources: _dbSources);
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
            return GetScope(query, integrationId, VulnerabilityTypeUtils.SqlInjection, Always);
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
            return GetScope(query, integrationId, VulnerabilityTypeUtils.NoSqlMongoDbInjection, Always);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for MongoDb NoSql injection.");
            return IastModuleResponse.Empty;
        }
    }

    public static IastModuleResponse OnCommandInjection(string file, string argumentLine, Collection<string>? argumentList, IntegrationId integrationId)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

        try
        {
            var evidence = BuildCommandInjectionEvidence(file, argumentLine, argumentList);
            return string.IsNullOrEmpty(evidence) ? IastModuleResponse.Empty : GetScope(evidence, integrationId, VulnerabilityTypeUtils.CommandInjection, taintValidator: Always, safeSources: _dbSources);
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
            return GetScope(param, integrationId, VulnerabilityTypeUtils.ReflectionInjection, taintValidator: Always, safeSources: _dbSources);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetCookieHash(string vulnerability, string cookieName, bool isFiltered)
    {
        return (isFiltered ? ("FILTERED_" + vulnerability) : (vulnerability + ":" + cookieName)).GetStaticHashCode();
    }

    public static IastModuleResponse OnInsecureCookie(IntegrationId integrationId, string cookieName, bool isFiltered)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

        // We provide a hash value for the vulnerability instead of calculating one, following the agreed conventions
        return AddWebVulnerability(cookieName, integrationId, VulnerabilityTypeUtils.InsecureCookie, GetCookieHash(VulnerabilityTypeUtils.InsecureCookie, cookieName, isFiltered));
    }

    public static IastModuleResponse OnNoHttpOnlyCookie(IntegrationId integrationId, string cookieName, bool isFiltered)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

        // We provide a hash value for the vulnerability instead of calculating one, following the agreed conventions
        return AddWebVulnerability(cookieName, integrationId, VulnerabilityTypeUtils.NoHttpOnlyCookie, GetCookieHash(VulnerabilityTypeUtils.NoHttpOnlyCookie, cookieName, isFiltered));
    }

    public static IastModuleResponse OnNoSamesiteCookie(IntegrationId integrationId, string cookieName, bool isFiltered)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

        // We provide a hash value for the vulnerability instead of calculating one, following the agreed conventions
        return AddWebVulnerability(cookieName, integrationId, VulnerabilityTypeUtils.NoSameSiteCookie, GetCookieHash(VulnerabilityTypeUtils.NoSameSiteCookie, cookieName, isFiltered));
    }

    public static IastModuleResponse OnInsecureAuthProtocol(string authHeader, IntegrationId integrationId)
    {
        // We provide a hash value for the vulnerability instead of calculating one, following the agreed conventions
        return AddWebVulnerability(authHeader, integrationId, VulnerabilityTypeUtils.InsecureAuthProtocol, (VulnerabilityTypeUtils.InsecureAuthProtocol + ':' + authHeader).GetStaticHashCode());
    }

    public static void OnDirectoryListingLeak(string methodName)
    {
        if (!Iast.Instance.Settings.Enabled) { return; }

        var vulnerability = new Vulnerability(
            VulnerabilityTypeUtils.DirectoryListingLeak,
            VulnerabilityTypeUtils.DirectoryListingLeak.GetStaticHashCode(),
            GetLocation(),
            new Evidence($"Directory listing is configured with: {methodName}"),
            IntegrationId.DirectoryListingLeak);

        VulnerabilityReporter.Report(vulnerability);
    }

    public static void OnSessionTimeout(string methodName, TimeSpan value)
    {
        if (!Iast.Instance.Settings.Enabled) { return; }

        // Not a vulnerability if the timeout is less than 30 minutes
        if (value.TotalMinutes < 30) { return; }

        var vulnerability = new Vulnerability(
            VulnerabilityTypeUtils.SessionTimeout,
            (VulnerabilityTypeUtils.SessionTimeout + ':' + methodName + ':' + value.TotalMinutes).GetStaticHashCode(),
            GetLocation(),
            new Evidence($"Session idle timeout is configured with: {methodName}, with a value of {value.TotalMinutes} minutes"),
            IntegrationId.SessionTimeout);

        VulnerabilityReporter.Report(vulnerability);
    }

    public static IastModuleResponse OnCipherAlgorithm(Type type, IntegrationId integrationId)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

        var algorithm = type.BaseType?.Name;

        if (algorithm is null || !InvalidCipherAlgorithm(type, algorithm))
        {
            return IastModuleResponse.Empty;
        }

        return GetScope(algorithm, integrationId, VulnerabilityTypeUtils.WeakCipher);
    }

    public static IastModuleResponse OnStackTraceLeak(Exception ex, IntegrationId integrationId)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

        var evidence = $"{ex.Source},{ex.GetType().Name}";
        // We report the stack of the exception instead of the current stack
        var stack = new StackTrace(ex, true);
        return GetScope(evidence, integrationId, VulnerabilityTypeUtils.StackTraceLeak, externalStack: stack);
    }

    public static IastModuleResponse OnHashingAlgorithm(string? algorithm, IntegrationId integrationId)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

        if (algorithm == null || !InvalidHashAlgorithm(algorithm))
        {
            return IastModuleResponse.Empty;
        }

        return GetScope(algorithm, integrationId, VulnerabilityTypeUtils.WeakHash);
    }

    public static IastModuleResponse OnXss(string? text)
    {
        try
        {
            if (!Iast.Instance.Settings.Enabled || string.IsNullOrEmpty(text))
            {
                return IastModuleResponse.Empty;
            }

            return GetScope(text!, IntegrationId.Xss, VulnerabilityTypeUtils.Xss, Always, exclusionSecureMarks: SecureMarks.Xss);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for XSS.");
            return IastModuleResponse.Empty;
        }
    }

    public static IastRequestContext? GetIastContext()
    {
        if (!IastSettings.Enabled)
        {
            // integration disabled, don't create a scope, skip this span
            return null;
        }

        return IastLifecycle.CurrentContext;
    }

    // This method adds web vulnerabilities, with no location, only on web environments
    private static IastModuleResponse AddWebVulnerability(string? evidenceValue, IntegrationId integrationId, string vulnerabilityType, int hashId)
    {
        var tracer = Tracer.Instance;
        if (!IastSettings.Enabled || !tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(integrationId))
        {
            // integration disabled, don't create a scope, skip this span
            return IastModuleResponse.Empty;
        }

        var requestContext = IastLifecycle.CurrentContext;
        var rootSpan = IastLifecycle.CurrentRootSpan;
        var traceContext = rootSpan?.Context?.TraceContext;

        if (requestContext?.AddVulnerabilityTypeAllowed(vulnerabilityType, GetRouteVulnerabilityStats) != true)
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

        if (!IastSettings.DeduplicationEnabled || HashBasedDeduplication.Instance.Add(vulnerability))
        {
            VulnerabilityReporter.Report(vulnerability, requestContext);
            requestContext.AddVulnerability(vulnerability);
            traceContext?.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Asm);
            traceContext?.Tags.EnableTraceSources(TraceSources.Asm);

            return IastModuleResponse.Vulnerable;
        }

        return IastModuleResponse.Empty;
    }

    public static bool AddRequestVulnerabilitiesAllowed()
    {
        // Check for global budget only
        return IastLifecycle.CurrentContext?.AddVulnerabilitiesAllowed() == true;
    }

    private static VulnerabilityStats GetRouteVulnerabilityStats(IastRequestContext request)
    {
        if (_vulnerabilityStats.Count >= VulnerabilityStatsRouteLimit)
        {
            _vulnerabilityStats.Clear(); // Poor man's LRU cache
        }

        var route = request.HttpRoute ?? string.Empty;
        var method = request.HttpMethod ?? string.Empty;
        var key = $"{method}#{route}";
        if (key.Length > 1)
        {
            return _vulnerabilityStats.GetOrAdd(key, (k) => new(k));
        }

        return new VulnerabilityStats(string.Empty);
    }

    internal static void UpdateRouteVulnerabilityStats(ref VulnerabilityStats stats)
    {
        if (stats.Route is not null)
        {
            _vulnerabilityStats[stats.Route] = stats;
        }
    }

    private static IastModuleResponse GetScope(
        string evidenceValue,
        IntegrationId integrationId,
        string vulnerabilityType,
        Func<TaintedObject, bool>? taintValidator = null,
        bool addLocation = true,
        int? hash = null,
        StackTrace? externalStack = null,
        SecureMarks exclusionSecureMarks = SecureMarks.None,
        SourceType[]? safeSources = null)
    {
        var tracer = Tracer.Instance;
        if (!IastSettings.Enabled || !tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(integrationId))
        {
            // integration disabled, don't create a scope, skip this span
            return IastModuleResponse.Empty;
        }

        var scope = tracer.ActiveScope as Scope;
        var currentSpan = scope?.Span;
        var isRequest = IastLifecycle.InRequest;
        var requestContext = IastLifecycle.CurrentContext;

        // Inside a request the web span is the root; outside a request keep the trace root, as before
        var rootSpan = isRequest ? IastLifecycle.CurrentRootSpan : currentSpan?.Context?.TraceContext?.RootSpan;
        var traceContext = rootSpan?.Context?.TraceContext;

        // We do not have, for now, tainted objects in console apps, so further checking is not neccessary.
        if (!isRequest && taintValidator != null)
        {
            return IastModuleResponse.Empty;
        }

        if (isRequest && requestContext?.AddVulnerabilitiesAllowed() != true)
        {
            // we are inside a request but we don't accept more vulnerabilities or IastRequestContext is null, which means that iast is
            // not activated for this particular request
            return IastModuleResponse.Empty;
        }

        TaintedObject? tainted = null;
        if (taintValidator != null)
        {
            tainted = requestContext?.GetTainted(evidenceValue);
            if (tainted is null || !taintValidator(tainted))
            {
                return IastModuleResponse.Empty;
            }
        }

        var ranges = tainted?.Ranges;
        if (ranges is not null)
        {
            var unsafeRanges = Ranges.GetUnsafeRanges(ranges, exclusionSecureMarks, safeSources);
            if (unsafeRanges is null || unsafeRanges.Length == 0)
            {
                return IastModuleResponse.Empty;
            }

            // Contains at least one range that is not safe (when analyzing a vulnerability that can have secure marks)
            ranges = unsafeRanges;
        }

        if (isRequest && requestContext?.AddVulnerabilityTypeAllowed(vulnerabilityType, GetRouteVulnerabilityStats) != true)
        {
            // we are inside a request but we don't accept more vulnerabilities or IastRequestContext is null, which means that iast is
            // not activated for this particular request
            return IastModuleResponse.Empty;
        }

        var location = addLocation ? GetLocation(externalStack, currentSpan, requestContext) : null;
        if (addLocation && location is null)
        {
            return IastModuleResponse.Empty;
        }

        var vulnerability = (hash is null) ?
            new Vulnerability(vulnerabilityType, location, new Evidence(evidenceValue, ranges), integrationId) :
            new Vulnerability(vulnerabilityType, (int)hash, location, new Evidence(evidenceValue, ranges), integrationId);

        if (!IastSettings.DeduplicationEnabled || HashBasedDeduplication.Instance.Add(vulnerability))
        {
            VulnerabilityReporter.Report(vulnerability, requestContext);

            if (isRequest)
            {
                traceContext?.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Asm);
                traceContext?.Tags.EnableTraceSources(TraceSources.Asm);

                requestContext?.AddVulnerability(vulnerability);
            }

            return IastModuleResponse.Vulnerable;
        }

        return IastModuleResponse.Empty;
    }

    private static Location? GetLocation(StackTrace? stack = null, Span? currentSpan = null, IastRequestContext? requestContext = null)
    {
        stack ??= StackWalker.GetStackTrace();
        if (!StackWalker.TryGetFrame(stack, out var stackFrame))
        {
            return null;
        }

        string? stackId = null;
        if (IastSettings.StackTraceEnabled)
        {
            stackId = requestContext?.GetNextVulnerabilityStackId();
        }

        return new Location(stackFrame, stack, stackId, currentSpan?.SpanId);
    }

    private static bool InvalidHashAlgorithm(string algorithm)
    {
        foreach (var weakHashAlgorithm in IastSettings.WeakHashAlgorithmsArray)
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
        foreach (var weakCipherAlgorithm in IastSettings.WeakCipherAlgorithmsArray)
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
        return AddWebVulnerability(evidence, integrationId, VulnerabilityTypeUtils.XContentTypeHeaderMissing, (VulnerabilityTypeUtils.XContentTypeHeaderMissing + ":" + serviceName).GetStaticHashCode());
    }

    internal static IastModuleResponse OnStrictTransportSecurityHeaderMissing(IntegrationId integrationId, string serviceName)
    {
        return AddWebVulnerability(null, integrationId, VulnerabilityTypeUtils.HstsHeaderMissing, (VulnerabilityTypeUtils.HstsHeaderMissing + ":" + serviceName).GetStaticHashCode());
    }

    internal static void OnHeaderInjection(IntegrationId integrationId, string headerName, string headerValue)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return;
        }

        var evidence = StringAspects.Concat(headerName, HeaderInjectionEvidenceSeparator, headerValue);
        var hash = ("HEADER_INJECTION:" + headerName).GetStaticHashCode();
        GetScope(evidence, integrationId, VulnerabilityTypeUtils.HeaderInjection, taintValidator: Always, safeSources: _dbSources, addLocation: false, hash: hash);
    }

    internal static IastModuleResponse OnXpathInjection(string xpath)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return IastModuleResponse.Empty;
        }

        try
        {
            return GetScope(xpath, IntegrationId.XpathInjection, VulnerabilityTypeUtils.XPathInjection, taintValidator: Always, safeSources: _dbSources);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for xpath injection.");
            return IastModuleResponse.Empty;
        }
    }

    internal static void OnEmailHtmlInjection(string text)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return;
        }

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // We use the same secure marks as XSS, but excluding db sources
        GetScope(text, IntegrationId.EmailHtmlInjection, VulnerabilityTypeUtils.EmailHtmlInjection, taintValidator: Always, safeSources: _dbSources, exclusionSecureMarks: SecureMarks.Xss);
    }

    internal static void OnEmailHtmlInjection(object? message, EmailInjectionType type)
    {
        if (!Iast.Instance.Settings.Enabled)
        {
            return;
        }

        if (message is null)
        {
            return;
        }

        ExtractProperties(message, type, out var body, out var isHtml);

        if (!isHtml || string.IsNullOrEmpty(body))
        {
            return;
        }

        // We use the same secure marks as XSS, but excluding db sources
        GetScope(body!, IntegrationId.EmailHtmlInjection, VulnerabilityTypeUtils.EmailHtmlInjection, taintValidator: Always, safeSources: _dbSources, exclusionSecureMarks: SecureMarks.Xss);
    }

    private static void ExtractProperties(object mail, EmailInjectionType type, out string? body, out bool isHtml)
    {
        switch (type)
        {
            case EmailInjectionType.SystemNetMail:
                var mailMessage = mail.DuckCast<IMailMessage>();
                body = mailMessage.Body;
                isHtml = mailMessage.IsBodyHtml;
                break;
            case EmailInjectionType.AmazonSimpleEmail:
                var sendEmailRequest = mail.DuckCast<ISendEmailRequest>();
                body = sendEmailRequest.Message?.Body?.Html?.Data;
                isHtml = !string.IsNullOrEmpty(body);
                break;
            default:
                Log.Error("Error while checking for email injection type.");
                body = string.Empty;
                isHtml = false;
                break;
        }
    }

    public static void LogAspectException(Exception ex, string extraInfo = "", [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "")
    {
        try
        {
            var text = $"{Path.GetFileNameWithoutExtension(filePath)}.{memberName} {extraInfo}";
            var hashCode = text.GetHashCode();
            bool alreadyLogged;
            lock (LoggedAspectExceptionMessages)
            {
                alreadyLogged = !LoggedAspectExceptionMessages.Add(hashCode);
            }

// intentionally using string interpolation in logging, as the resulting string is actually a constant, and contains the important aspect information
#pragma warning disable DDLOG004 // Message templates should be constant
            if (alreadyLogged)
            {
                Log.Debug(ex, $"Error invoking {extraInfo}");
            }
            else
            {
                Log.Error(ex, $"Error invoking {extraInfo}");
            }
#pragma warning restore DDLOG004 // Message templates should be constant
        }
        catch (Exception e)
        {
            Log.Debug(new AggregateException(ex, e), "Error while logging aspect exception");
        }
    }

    internal static void RegisterDbRecord(object instance)
    {
        DbRecords.RegisterDbRecord(instance);
    }

    internal static void UnregisterDbRecord(object instance)
    {
        DbRecords.UnregisterDbRecord(instance);
    }

    internal static bool AddDbValue(object instance, string? column, string value)
    {
        return DbRecords.AddDbValue(instance, column, value);
    }

    internal sealed class DbRecordManager
    {
        private ConditionalWeakTable<object, DbRecordData> dataBaseRows = new ConditionalWeakTable<object, DbRecordData>();
        private IastSettings iastSettings;

        public DbRecordManager(IastSettings settings)
        {
            iastSettings = settings;
        }

        public void RegisterDbRecord(object instance)
        {
            if (!iastSettings.Enabled || iastSettings.DataBaseRowsToTaint <= 0)
            {
                return;
            }

            var recordData = dataBaseRows.GetOrCreateValue(instance);
            recordData.Count++;
        }

        public void UnregisterDbRecord(object instance)
        {
            if (!iastSettings.Enabled || iastSettings.DataBaseRowsToTaint <= 0)
            {
                return;
            }

            dataBaseRows.Remove(instance);
        }

        /// <summary>
        /// Add a DB value to be tainted
        /// </summary>
        /// <returns>The return value this method indicates if the value could have been tainted
        /// if a iast context is available. It will return true if iast is enabled and the row
        /// count conditions are met. The result is used in the unit tests, don't use it
        /// anywhere else
        /// </returns>
        public bool AddDbValue(object instance, string? column, string value)
        {
            if (!iastSettings.Enabled || iastSettings.DataBaseRowsToTaint <= 0)
            {
                return false;
            }

            if (iastSettings.DataBaseRowsToTaint > 0)
            {
                if (!dataBaseRows.TryGetValue(instance, out var recordData))
                {
                    return false;
                }

                if (recordData.Count > iastSettings.DataBaseRowsToTaint)
                {
                    return false;
                }
            }

            IastLifecycle.CurrentContext?.AddDbValue(column, value);

            return true;
        }

        private sealed class DbRecordData
        {
            public int Count { get; set; }
        }
    }
}
