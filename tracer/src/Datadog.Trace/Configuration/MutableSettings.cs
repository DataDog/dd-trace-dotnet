// <copyright file="MutableSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Configuration;

/// <summary>
/// Settings that can change during the lifetime of the application. This can include settings updated in
/// code or via remote configuration. Note that the specific instance is immutable, but there may be a
/// new version in the lifetime of the application
/// </summary>
internal sealed class MutableSettings : IEquatable<MutableSettings>
{
    // we cached the static instance here, because is being used in the hotpath
    // by IsIntegrationEnabled method (called from all integrations)
    private readonly DomainMetadata _domainMetadata = DomainMetadata.Instance;

    private MutableSettings(
        bool isInitialSettings,
        bool traceEnabled,
        string? customSamplingRules,
        bool customSamplingRulesIsRemote,
        double? globalSamplingRate,
        bool logsInjectionEnabled,
        ReadOnlyDictionary<string, string> globalTags,
        ReadOnlyDictionary<string, string> headerTags,
        bool startupDiagnosticLogEnabled,
        string? environment,
        string? serviceName,
        string defaultServiceName,
        string? serviceVersion,
        HashSet<string> disabledIntegrationNames,
        ReadOnlyDictionary<string, string> grpcTags,
        bool tracerMetricsEnabled,
        IntegrationSettingsCollection integrations,
        bool analyticsEnabled,
        int maxTracesSubmittedPerSecond,
        bool kafkaCreateConsumerScopeEnabled,
        bool[] httpServerErrorStatusCodes,
        bool[] httpClientErrorStatusCodes,
        ReadOnlyDictionary<string, string> serviceNameMappings,
        string? gitRepositoryUrl,
        string? gitCommitSha,
        OverrideErrorLog errorLog)
    {
        IsInitialSettings = isInitialSettings;
        TraceEnabled = traceEnabled;
        CustomSamplingRules = customSamplingRules;
        CustomSamplingRulesIsRemote = customSamplingRulesIsRemote;
        GlobalSamplingRate = globalSamplingRate;
        LogsInjectionEnabled = logsInjectionEnabled;
        GlobalTags = globalTags;
        HeaderTags = headerTags;
        StartupDiagnosticLogEnabled = startupDiagnosticLogEnabled;
        Environment = environment;
        ServiceName = serviceName;
        DefaultServiceName = defaultServiceName;
        ServiceVersion = serviceVersion;
        DisabledIntegrationNames = disabledIntegrationNames;
        GrpcTags = grpcTags;
        TracerMetricsEnabled = tracerMetricsEnabled;
        Integrations = integrations;
#pragma warning disable CS0618 // Type or member is obsolete
        AnalyticsEnabled = analyticsEnabled;
#pragma warning restore CS0618 // Type or member is obsolete
        MaxTracesSubmittedPerSecond = maxTracesSubmittedPerSecond;
        KafkaCreateConsumerScopeEnabled = kafkaCreateConsumerScopeEnabled;
        HttpServerErrorStatusCodes = httpServerErrorStatusCodes;
        HttpClientErrorStatusCodes = httpClientErrorStatusCodes;
        ServiceNameMappings = serviceNameMappings;
        GitRepositoryUrl = gitRepositoryUrl;
        GitCommitSha = gitCommitSha;
        ErrorLog = errorLog;
    }

    // Settings that can be set via remote config

    /// <summary>
    /// Gets a value indicating whether tracing is enabled.
    /// Default is <c>true</c>.
    /// </summary>
    /// <seealso cref="ConfigurationKeys.TraceEnabled"/>
    public bool TraceEnabled { get; }

    /// <summary>
    /// Gets a value indicating custom sampling rules.
    /// </summary>
    /// <seealso cref="ConfigurationKeys.CustomSamplingRules"/>
    public string? CustomSamplingRules { get; }

    /// <summary>
    /// Gets a value indicating whether the sampling rules came from a remote source
    /// </summary>
    public bool CustomSamplingRulesIsRemote { get; }

    /// <summary>
    /// Gets a value indicating a global rate for sampling.
    /// </summary>
    /// <seealso cref="ConfigurationKeys.GlobalSamplingRate"/>
    public double? GlobalSamplingRate { get; }

    /// <summary>
    /// Gets a value indicating whether correlation identifiers are
    /// automatically injected into the logging context.
    /// Default is <c>true</c>.
    /// </summary>
    /// <seealso cref="ConfigurationKeys.LogsInjectionEnabled"/>
    public bool LogsInjectionEnabled { get; }

    /// <summary>
    /// Gets the global tags, which are applied to all <see cref="Span"/>s.
    /// </summary>
    public ReadOnlyDictionary<string, string> GlobalTags { get; }

    /// <summary>
    /// Gets the map of header keys to tag names, which are applied to the root <see cref="Span"/>
    /// of incoming and outgoing HTTP requests.
    /// </summary>
    public ReadOnlyDictionary<string, string> HeaderTags { get; }

    // Additional settings that can be set in code
    // NOTE: this includes everything except:
    // - DD_TRACE_AGENT_URL; This can be set in code, but is handled separately in TracerSettings
    // - DD_TRACE_STATS_COMPUTATION_ENABLED; This can currently be set in code, but it's problematic for
    //   various reasons related to data pipeline, so this change makes it so that you CAN'T set the value
    //   in code, despite the API appearing to let you. The change is addressed via documentation, and
    //   in the future we should remove the property entirely (or at least mark it as obsolete)

    /// <summary>
    /// Gets a value indicating whether the diagnostic log at startup is enabled
    /// </summary>
    public bool StartupDiagnosticLogEnabled { get; }

    /// <summary>
    /// Gets the default environment name applied to all spans.
    /// </summary>
    /// <seealso cref="ConfigurationKeys.Environment"/>
    public string? Environment { get; }

    /// <summary>
    /// Gets the user-specified service name for the application. You should typically
    /// favor <see cref="DefaultServiceName"/> which includes the calculated application
    /// name where an explicit service name is not provided.
    /// </summary>
    /// <seealso cref="ConfigurationKeys.ServiceName"/>
    public string? ServiceName { get; }

    /// <summary>
    /// Gets the service name applied to top-level spans and used to build derived service names.
    /// Composed based on <see cref="ServiceName"/> if provided, or a fallback value
    /// </summary>
    public string DefaultServiceName { get; }

    /// <summary>
    /// Gets the version tag applied to all spans.
    /// </summary>
    /// <seealso cref="ConfigurationKeys.ServiceVersion"/>
    public string? ServiceVersion { get; }

    /// <summary>
    /// Gets the names of disabled integrations.
    /// </summary>
    /// <seealso cref="ConfigurationKeys.DisabledIntegrations"/>
    public HashSet<string> DisabledIntegrationNames { get; }

    /// <summary>
    /// Gets the map of metadata keys to tag names, which are applied to the root <see cref="Span"/>
    /// of incoming and outgoing GRPC requests.
    /// </summary>
    public ReadOnlyDictionary<string, string> GrpcTags { get; }

    /// <summary>
    /// Gets a value indicating whether internal metrics
    /// are enabled and sent to DogStatsd.
    /// </summary>
    public bool TracerMetricsEnabled { get; }

    /// <summary>
    /// Gets a collection of <see cref="IntegrationSettings"/> keyed by integration name.
    /// </summary>
    public IntegrationSettingsCollection Integrations { get; }

    /// <summary>
    /// Gets a value indicating whether default Analytics are enabled.
    /// Settings this value is a shortcut for setting
    /// <see cref="Configuration.IntegrationSettings.AnalyticsEnabled"/> on some predetermined integrations.
    /// See the documentation for more details.
    /// </summary>
    /// <seealso cref="ConfigurationKeys.GlobalAnalyticsEnabled"/>
    [Obsolete(DeprecationMessages.AppAnalytics)]
    public bool AnalyticsEnabled { get; }

    /// <summary>
    /// Gets a value indicating the maximum number of traces set to AutoKeep (p1) per second.
    /// Default is <c>100</c>.
    /// </summary>
    /// <seealso cref="ConfigurationKeys.TraceRateLimit"/>
    public int MaxTracesSubmittedPerSecond { get; }

    /// <summary>
    /// Gets a value indicating whether a span context should be created on exiting a successful Kafka
    /// Consumer.Consume() call, and closed on entering Consumer.Consume().
    /// </summary>
    /// <seealso cref="ConfigurationKeys.KafkaCreateConsumerScopeEnabled"/>
    public bool KafkaCreateConsumerScopeEnabled { get; }

    /// <summary>
    /// Gets the HTTP status code that should be marked as errors for server integrations.
    /// </summary>
    /// <seealso cref="ConfigurationKeys.HttpServerErrorStatusCodes"/>
    public bool[] HttpServerErrorStatusCodes { get; }

    /// <summary>
    /// Gets the HTTP status code that should be marked as errors for client integrations.
    /// </summary>
    /// <seealso cref="ConfigurationKeys.HttpClientErrorStatusCodes"/>
    public bool[] HttpClientErrorStatusCodes { get; }

    /// <summary>
    /// Gets configuration values for changing service names based on configuration
    /// </summary>
    public ReadOnlyDictionary<string, string> ServiceNameMappings { get; }

    // These ones can't be _directly_ changed, but are dependent on things that _can_
    // so they are _implicitly_ dynamic

    /// <summary>
    /// Gets the application's git repository url.
    /// </summary>
    /// <seealso cref="ConfigurationKeys.GitRepositoryUrl"/>
    public string? GitRepositoryUrl { get; }

    /// <summary>
    /// Gets the application's git commit hash.
    /// </summary>
    /// <seealso cref="ConfigurationKeys.GitCommitSha"/>
    public string? GitCommitSha { get; }

    // Infra
    internal bool IsInitialSettings { get; }

    internal OverrideErrorLog ErrorLog { get; }

    internal static ReadOnlyDictionary<string, string>? InitializeHeaderTags(ConfigurationBuilder config, string key, bool headerTagsNormalizationFixEnabled)
        => InitializeHeaderTags(
            key.AsDictionaryResult(allowOptionalMappings: true),
            headerTagsNormalizationFixEnabled);

    private static ReadOnlyDictionary<string, string>? InitializeHeaderTags(
        ConfigurationBuilder.ClassConfigurationResultWithKey<IDictionary<string, string>> configurationResult,
        bool headerTagsNormalizationFixEnabled)
    {
        var configurationDictionary = configurationResult.WithDefault(new DefaultResult<IDictionary<string, string>>(null!, "[]"));

        if (configurationDictionary == null!)
        {
            return null;
        }

        var headerTags = new Dictionary<string, string>(configurationDictionary.Count);

        foreach (var kvp in configurationDictionary)
        {
            var headerName = kvp.Key.Trim();

            if (string.IsNullOrEmpty(headerName))
            {
                continue;
            }

            if (InitializeHeaderTag(tagName: kvp.Value, headerTagsNormalizationFixEnabled, out var finalTagName))
            {
                headerTags.Add(headerName, finalTagName);
            }
        }

        return new(headerTags);
    }

    internal static bool InitializeHeaderTag(
        string? tagName,
        bool headerTagsNormalizationFixEnabled,
        [NotNullWhen(true)] out string? finalTagName)
    {
        tagName = tagName?.Trim();

        if (string.IsNullOrEmpty(tagName))
        {
            // The user did not provide a tag name. Normalization will happen later, when adding the tag prefix.
            finalTagName = string.Empty;
            return true;
        }

        if (!SpanTagHelper.IsValidTagName(tagName!, out tagName))
        {
            // invalid tag name
            finalTagName = null;
            return false;
        }

        if (headerTagsNormalizationFixEnabled)
        {
            // Default code path: if the user provided a tag name, don't try to normalize it.
            finalTagName = tagName;
            return true;
        }

        // user opted via feature flag into the previous behavior,
        // where tag names were normalized even when specified
        // (but _not_ spaces, due to a bug in the normalization code)
        return SpanTagHelper.TryNormalizeTagName(tagName, normalizeSpaces: false, out finalTagName);
    }

    internal static bool[] ParseHttpCodesToArray(string httpStatusErrorCodes)
    {
        bool[] httpErrorCodesArray = new bool[600];

        void TrySetValue(int index)
        {
            if (index >= 0 && index < httpErrorCodesArray.Length)
            {
                httpErrorCodesArray[index] = true;
            }
        }

        string[] configurationsArray = httpStatusErrorCodes.Replace(" ", string.Empty).Split(',');

        foreach (string statusConfiguration in configurationsArray)
        {
            int startStatus;

            // Checks that the value about to be used follows the `401-404` structure or single 3 digit number i.e. `401` else log the warning
            if (!Regex.IsMatch(statusConfiguration, @"^\d{3}-\d{3}$|^\d{3}$"))
            {
                // TODO: this should be logged in telemetry or the ErrorLog override or something
                // Log.Warning("Wrong format '{0}' for DD_TRACE_HTTP_SERVER/CLIENT_ERROR_STATUSES configuration.", statusConfiguration);
            }

            // If statusConfiguration equals a single value i.e. `401` parse the value and save to the array
            else if (int.TryParse(statusConfiguration, out startStatus))
            {
                TrySetValue(startStatus);
            }
            else
            {
                string[] statusCodeLimitsRange = statusConfiguration.Split('-');

                startStatus = int.Parse(statusCodeLimitsRange[0]);
                int endStatus = int.Parse(statusCodeLimitsRange[1]);

                if (endStatus < startStatus)
                {
                    startStatus = endStatus;
                    endStatus = int.Parse(statusCodeLimitsRange[0]);
                }

                for (int statusCode = startStatus; statusCode <= endStatus; statusCode++)
                {
                    TrySetValue(statusCode);
                }
            }
        }

        return httpErrorCodesArray;
    }

    public bool Equals(MutableSettings? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return TraceEnabled == other.TraceEnabled &&
               CustomSamplingRules == other.CustomSamplingRules &&
               CustomSamplingRulesIsRemote == other.CustomSamplingRulesIsRemote &&
               Nullable.Equals(GlobalSamplingRate, other.GlobalSamplingRate) &&
               LogsInjectionEnabled == other.LogsInjectionEnabled &&
               StartupDiagnosticLogEnabled == other.StartupDiagnosticLogEnabled &&
               Environment == other.Environment &&
               ServiceName == other.ServiceName &&
               ServiceVersion == other.ServiceVersion &&
               TracerMetricsEnabled == other.TracerMetricsEnabled &&
#pragma warning disable 618 // App analytics is deprecated, but still used
               AnalyticsEnabled == other.AnalyticsEnabled &&
#pragma warning restore 618
               MaxTracesSubmittedPerSecond == other.MaxTracesSubmittedPerSecond &&
               KafkaCreateConsumerScopeEnabled == other.KafkaCreateConsumerScopeEnabled &&
               GitRepositoryUrl == other.GitRepositoryUrl &&
               GitCommitSha == other.GitCommitSha &&
               // Do collection comparisons at the end, as generally more expensive
               AreEqual(GlobalTags, other.GlobalTags) &&
               AreEqual(HeaderTags, other.HeaderTags) &&
               AreEqual(GrpcTags, other.GrpcTags) &&
               AreEqual(ServiceNameMappings, other.ServiceNameMappings) &&
               DisabledIntegrationNames.SetEquals(other.DisabledIntegrationNames) &&
               // Could unroll the Linq, but prob not worth the hassle
               HttpServerErrorStatusCodes.SequenceEqual(other.HttpServerErrorStatusCodes) &&
               HttpClientErrorStatusCodes.SequenceEqual(other.HttpClientErrorStatusCodes) &&
               // Most expensive one
               AreEqualIntegrations(Integrations, other.Integrations);

        static bool AreEqual(ReadOnlyDictionary<string, string>? dictionary1, ReadOnlyDictionary<string, string>? dictionary2)
        {
            if (dictionary1 == null || dictionary2 == null)
            {
                return ReferenceEquals(dictionary1, dictionary2);
            }

            if (dictionary1.Count != dictionary2.Count)
            {
                return false;
            }

            foreach (var pair in dictionary1)
            {
                if (dictionary2.TryGetValue(pair.Key, out var value))
                {
                    if (!string.Equals(value, pair.Value))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        static bool AreEqualIntegrations(IntegrationSettingsCollection integrations1, IntegrationSettingsCollection integrations2)
        {
            if (integrations1.Settings.Length != integrations2.Settings.Length)
            {
                return false;
            }

            // They should be the exact same settings in both cases
            for (var i = 0; i < integrations1.Settings.Length; i++)
            {
                var integration1 = integrations1.Settings[i];
                var integration2 = integrations2.Settings[i];

                if (!integration1.Equals(integration2))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || (obj is MutableSettings other && Equals(other));
    }

    public override int GetHashCode()
    {
        // we can't easily include the collections in the hash code
        var hashCode = new HashCode();
        hashCode.Add(TraceEnabled);
        hashCode.Add(CustomSamplingRules);
        hashCode.Add(CustomSamplingRulesIsRemote);
        hashCode.Add(GlobalSamplingRate);
        hashCode.Add(LogsInjectionEnabled);
        // hashCode.Add(GlobalTags);
        // hashCode.Add(HeaderTags);
        hashCode.Add(StartupDiagnosticLogEnabled);
        hashCode.Add(Environment);
        hashCode.Add(ServiceName);
        hashCode.Add(ServiceVersion);
        // hashCode.Add(DisabledIntegrationNames);
        // hashCode.Add(GrpcTags);
        hashCode.Add(TracerMetricsEnabled);
        // hashCode.Add(Integrations);
#pragma warning disable 618 // App analytics is deprecated, but still used
        hashCode.Add(AnalyticsEnabled);
#pragma warning restore 618
        hashCode.Add(MaxTracesSubmittedPerSecond);
        hashCode.Add(KafkaCreateConsumerScopeEnabled);
        // hashCode.Add(HttpServerErrorStatusCodes);
        // hashCode.Add(HttpClientErrorStatusCodes);
        // hashCode.Add(ServiceNameMappings);
        hashCode.Add(GitRepositoryUrl);
        hashCode.Add(GitCommitSha);
        return hashCode.ToHashCode();
    }

    internal bool IsErrorStatusCode(int statusCode, bool serverStatusCode)
    {
        var source = serverStatusCode ? HttpServerErrorStatusCodes : HttpClientErrorStatusCodes;

        if (source == null)
        {
            return false;
        }

        if (statusCode >= source.Length)
        {
            return false;
        }

        return source[statusCode];
    }

    internal bool IsIntegrationEnabled(IntegrationId integration, bool defaultValue = true)
    {
        if (TraceEnabled && !_domainMetadata.ShouldAvoidAppDomain())
        {
            return Integrations[integration].Enabled ?? defaultValue;
        }

        return false;
    }

    [Obsolete(DeprecationMessages.AppAnalytics)]
    internal double? GetIntegrationAnalyticsSampleRate(IntegrationId integration, bool enabledWithGlobalSetting)
    {
        var integrationSettings = Integrations[integration];
        var analyticsEnabled = integrationSettings.AnalyticsEnabled ?? (enabledWithGlobalSetting && AnalyticsEnabled);
        return analyticsEnabled ? integrationSettings.AnalyticsSampleRate : (double?)null;
    }

    private static void RemoveDisallowedGlobalTags(IDictionary<string, string> globalTags)
    {
        globalTags.Remove(Tags.Service);
        globalTags.Remove(Tags.Env);
        globalTags.Remove(Tags.Version);
        globalTags.Remove(Ci.Tags.CommonTags.GitCommit);
        globalTags.Remove(Ci.Tags.CommonTags.GitRepository);
    }

    /// <summary>
    /// Create an instance of <see cref="MutableSettings"/> based on dynamic configuration sources
    /// </summary>
    /// <param name="dynamicSource">The <see cref="IConfigurationSource"/> for dynamic config</param>
    /// <param name="manualSource">The <see cref="IConfigurationSource"/> for manual configuration</param>
    /// <param name="initialSettings">The initial mutable settings created from static sources </param>
    /// <param name="tracerSettings">The global <see cref="TracerSettings"/> object</param>
    /// <param name="telemetry">The <see cref="IConfigurationTelemetry"/> for recording telemetry updates</param>
    /// <param name="errorLog">The <see cref="OverrideErrorLog"/> for recording errors in configuration</param>
    /// <returns>The <see cref="MutableSettings"/> updated images</returns>
    public static MutableSettings CreateUpdatedMutableSettings(
        IConfigurationSource dynamicSource,
        ManualInstrumentationConfigurationSourceBase manualSource,
        MutableSettings initialSettings, // Might be the "real" initial mutable settings or the "null" version
        TracerSettings tracerSettings,
        IConfigurationTelemetry telemetry,
        OverrideErrorLog errorLog)
    {
        // For most configs we can do "combined" config where dynamic config has higher precedence
        var config = new ConfigurationBuilder(new CompositeConfigurationSource([dynamicSource, manualSource]), telemetry);

        var traceEnabled = GetResult(
            config.WithKeys(ConfigurationKeys.TraceEnabled).AsBoolResult().ConfigurationResult,
            initialSettings.TraceEnabled);

        var logsInjectionEnabled = GetResult(
            config.WithKeys(ConfigurationKeys.LogsInjectionEnabled).AsBoolResult().ConfigurationResult,
            initialSettings.LogsInjectionEnabled);

        // We can't use the `GetResult` helper because of nullability annoyances. Meh.
        var globalSamplingRateResult = config.WithKeys(ConfigurationKeys.GlobalSamplingRate).AsDoubleResult().ConfigurationResult;
        var globalSamplingRate = globalSamplingRateResult is { IsValid: true, Result: var result } ? result : initialSettings.GlobalSamplingRate;

        var headerTags = GetHeaderTagsResult(
            config.WithKeys(ConfigurationKeys.HeaderTags).AsDictionaryResult(allowOptionalMappings: true),
            headerTagsNormalizationFixEnabled: true,
            initialSettings.HeaderTags);

        // No point checking the fallback keys, they're not used
        // TODO: should we be checking for experimental tags format here?
        // Also, note that this _prevents_ customers from setting service etc via the tags collection
        // They have to set it via the specific properties instead
        var globalTags = GetDictionaryResult(
            config.WithKeys(ConfigurationKeys.GlobalTags).AsDictionaryResult(),
            initialSettings.GlobalTags,
            removeGlobalTags: true);

        // The remaining properties are only exposed via manual config, so that's all we need to check
        var startupDiagnosticLogEnabled = GetResult(
            config.WithKeys(ConfigurationKeys.StartupDiagnosticLogEnabled).AsBoolResult().ConfigurationResult,
            initialSettings.StartupDiagnosticLogEnabled);

        var environment = GetResult(
            config.WithKeys(ConfigurationKeys.Environment).AsStringResult().ConfigurationResult,
            initialSettings.Environment);

        var serviceName = GetResult(
            config.WithKeys(ConfigurationKeys.ServiceName).AsStringResult().ConfigurationResult,
            initialSettings.ServiceName);

        var serviceVersion = GetResult(
            config.WithKeys(ConfigurationKeys.ServiceVersion).AsStringResult().ConfigurationResult,
            initialSettings.ServiceVersion);

        var disabledIntegrationNameResult = config.WithKeys(ConfigurationKeys.DisabledIntegrations)
                                                        .AsStringResult();
        var disabledIntegrationNames = initialSettings.DisabledIntegrationNames;
        if (disabledIntegrationNameResult.ConfigurationResult is { IsValid: true, Result: var stringResult })
        {
            // If Activity support is enabled, we shouldn't enable the OTel listener
            var disabledIntegrationNamesArray = stringResult.Split([';'], StringSplitOptions.RemoveEmptyEntries);
            disabledIntegrationNames = tracerSettings.IsActivityListenerEnabled
                                           ? new HashSet<string>(disabledIntegrationNamesArray, StringComparer.OrdinalIgnoreCase)
                                           : new HashSet<string>([..disabledIntegrationNamesArray, nameof(IntegrationId.OpenTelemetry)], StringComparer.OrdinalIgnoreCase);
        }

        var integrations = new IntegrationSettingsCollection(manualSource, disabledIntegrationNames, initialSettings.Integrations);

        var grpcTags = GetHeaderTagsResult(
            config.WithKeys(ConfigurationKeys.GrpcTags).AsDictionaryResult(allowOptionalMappings: true),
            headerTagsNormalizationFixEnabled: true,
            initialSettings.GrpcTags);

        var tracerMetricsEnabled = GetResult(
            config.WithKeys(ConfigurationKeys.TracerMetricsEnabled).AsBoolResult().ConfigurationResult,
            initialSettings.TracerMetricsEnabled);

#pragma warning disable 618 // App analytics is deprecated, but still used
        var analyticsEnabled = GetResult(
            config.WithKeys(ConfigurationKeys.GlobalAnalyticsEnabled).AsBoolResult().ConfigurationResult,
            initialSettings.AnalyticsEnabled);
#pragma warning restore 618

#pragma warning disable 618 // this parameter has been replaced but may still be used
        var maxTracesSubmittedPerSecond = GetResult(
            config.WithKeys(ConfigurationKeys.MaxTracesSubmittedPerSecond).AsInt32Result().ConfigurationResult,
            initialSettings.MaxTracesSubmittedPerSecond);
#pragma warning restore 618

        var kafkaCreateConsumerScopeEnabled = GetResult(
            config.WithKeys(ConfigurationKeys.KafkaCreateConsumerScopeEnabled).AsBoolResult().ConfigurationResult,
            initialSettings.KafkaCreateConsumerScopeEnabled);

        var httpServerErrorStatusCodes = GetStatusCodesResult(
            config.WithKeys(ConfigurationKeys.HttpServerErrorStatusCodes).AsStringResult(),
            initialSettings.HttpServerErrorStatusCodes);

        var httpClientErrorStatusCodes = GetStatusCodesResult(
            config.WithKeys(ConfigurationKeys.HttpClientErrorStatusCodes).AsStringResult(),
            initialSettings.HttpClientErrorStatusCodes);

        var serviceNamesResult = config.WithKeys(ConfigurationKeys.ServiceNameMappings).AsDictionaryResult();
        var serviceNameMappings = GetDictionaryResult(
            serviceNamesResult,
            initialSettings.ServiceNameMappings,
            removeGlobalTags: false);

        // These behave differently depending on which source the telemetry came from, so inspect them separately
        // Reading the manual value first is important to ensure correct telemetry
        var manualConfig = new ConfigurationBuilder(manualSource, telemetry);
        var dynamicConfig = new ConfigurationBuilder(dynamicSource, telemetry);
        var manualCustomSamplingRules = manualConfig.WithKeys(ConfigurationKeys.CustomSamplingRules).AsStringResult();
        // Note: Calling GetAsClass<string>() here instead of GetAsString() as we need to get the
        // "serialized JToken", which in JsonConfigurationSource is different, as it allows for non-string tokens
        var remoteCustomSamplingRules = dynamicConfig.WithKeys(ConfigurationKeys.CustomSamplingRules).GetAsClassResult<string>(validator: null, converter: s => s);
        string? customSamplingRules;
        bool customSamplingRulesIsRemote;

        if (remoteCustomSamplingRules.ConfigurationResult is { IsValid: true, Result: var remoteRules })
        {
            customSamplingRules = remoteRules;
            customSamplingRulesIsRemote = true;
        }
        else if (manualCustomSamplingRules.ConfigurationResult is { IsValid: true, Result: var manualRules })
        {
            customSamplingRules = manualRules;
            customSamplingRulesIsRemote = false;
        }
        else
        {
            customSamplingRules = initialSettings.CustomSamplingRules;
            customSamplingRulesIsRemote = initialSettings.CustomSamplingRulesIsRemote;
        }

        // These can't actually be changed in code right now, so just set them to the same values
        var gitRepositoryUrl = initialSettings.GitRepositoryUrl;
        var gitCommitSha = initialSettings.GitCommitSha;

        return new MutableSettings(
            isInitialSettings: false,
            traceEnabled: traceEnabled,
            customSamplingRules: customSamplingRules,
            customSamplingRulesIsRemote: customSamplingRulesIsRemote,
            globalSamplingRate: globalSamplingRate,
            logsInjectionEnabled: logsInjectionEnabled,
            globalTags: globalTags,
            headerTags: headerTags,
            startupDiagnosticLogEnabled: startupDiagnosticLogEnabled,
            environment: environment,
            serviceName: serviceName,
            defaultServiceName: serviceName ?? tracerSettings.FallbackApplicationName,
            serviceVersion: serviceVersion,
            disabledIntegrationNames: disabledIntegrationNames,
            grpcTags: grpcTags,
            tracerMetricsEnabled: tracerMetricsEnabled,
            integrations: integrations,
            analyticsEnabled: analyticsEnabled,
            maxTracesSubmittedPerSecond: maxTracesSubmittedPerSecond,
            kafkaCreateConsumerScopeEnabled: kafkaCreateConsumerScopeEnabled,
            httpServerErrorStatusCodes: httpServerErrorStatusCodes,
            httpClientErrorStatusCodes: httpClientErrorStatusCodes,
            serviceNameMappings: serviceNameMappings,
            gitRepositoryUrl: gitRepositoryUrl,
            gitCommitSha: gitCommitSha,
            errorLog: errorLog);

        static ReadOnlyDictionary<string, string> GetHeaderTagsResult(
            ConfigurationBuilder.ClassConfigurationResultWithKey<IDictionary<string, string>> result,
            bool headerTagsNormalizationFixEnabled,
            ReadOnlyDictionary<string, string> fallback)
        {
            if (result.ConfigurationResult is { IsValid: true })
            {
                // Non-null return if result is valid, but play it safe
                return InitializeHeaderTags(result, headerTagsNormalizationFixEnabled) ?? ReadOnlyDictionary.Empty;
            }

            return fallback;
        }

        static ReadOnlyDictionary<string, string> GetDictionaryResult(
            ConfigurationBuilder.ClassConfigurationResultWithKey<IDictionary<string, string>> result,
            ReadOnlyDictionary<string, string> fallback,
            bool removeGlobalTags)
        {
            return result.ConfigurationResult is { IsValid: true, Result: var r1 }
                       ? FixupDictionary(r1, removeGlobalTags)
                       : fallback;

            static ReadOnlyDictionary<string, string> FixupDictionary(IDictionary<string, string>? r1, bool removeGlobalTags)
            {
                if (r1 is null)
                {
                    return ReadOnlyDictionary.Empty;
                }

                // Filter out tags with empty keys or empty values, and trim whitespace
                r1 = r1
                    .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                    .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim());

                if (removeGlobalTags)
                {
                    RemoveDisallowedGlobalTags(r1);
                }

                return new(r1);
            }
        }

        static bool[] GetStatusCodesResult(ConfigurationBuilder.ClassConfigurationResultWithKey<string> result, bool[] fallback)
            => result.ConfigurationResult is { IsValid: true, Result: var codes } ? ParseHttpCodesToArray(codes) : fallback;

        static T? GetResult<T>(ConfigurationResult<T> configResult, T? fallback)
            => configResult is { IsValid: true, Result: var result } ? result : fallback;
    }

    /// <summary>
    /// Create an instance of <see cref="MutableSettings"/> based on static source
    /// </summary>
    /// <param name="source">The global, static, <see cref="IConfigurationSource"/></param>
    /// <param name="telemetry">The <see cref="IConfigurationTelemetry"/> for recording telemetry updates</param>
    /// <param name="errorLog">The <see cref="OverrideErrorLog"/> for recording errors in configuration</param>
    /// <param name="tracerSettings">The global <see cref="TracerSettings"/> object</param>
    /// <returns>The initial, static settings. These are fixed for the lifetime of the application</returns>
    public static MutableSettings CreateInitialMutableSettings(
        IConfigurationSource source,
        IConfigurationTelemetry telemetry,
        OverrideErrorLog errorLog,
        TracerSettings tracerSettings)
    {
        var config = new ConfigurationBuilder(source, telemetry);

        var logsInjectionEnabled = config
                                  .WithKeys(ConfigurationKeys.LogsInjectionEnabled)
                                  .AsBool(defaultValue: true);

        var otelTags = config
                      .WithKeys(ConfigurationKeys.OpenTelemetry.ResourceAttributes)
                      .AsDictionaryResult(separator: '=');

        Dictionary<string, string>? globalTags;
        if (tracerSettings.ExperimentalFeaturesEnabled.Contains("DD_TAGS"))
        {
            // New behavior: If ExperimentalFeaturesEnabled configures DD_TAGS, we want to change DD_TAGS parsing to do the following:
            // 1. If a comma is in the value, split on comma as before. Otherwise, split on space
            // 2. Key-value pairs with empty values are allowed, instead of discarded
            // 3. Key-value pairs without values (i.e. no `:` separator) are allowed and treated as key-value pairs with empty values, instead of discarded
            Func<string, IDictionary<string, string>> updatedTagsParser = (data) =>
            {
                var dictionary = new ConcurrentDictionary<string, string>();
                if (string.IsNullOrWhiteSpace(data))
                {
                    // return empty collection
                    return dictionary;
                }

                char[] separatorChars = data.Contains(',') ? [','] : [' '];
                var entries = data.Split(separatorChars, StringSplitOptions.RemoveEmptyEntries);

                foreach (var entry in entries)
                {
                    // we need Trim() before looking forthe separator so we can skip entries with no key
                    // (that is, entries with a leading separator, like "<empty or whitespace>:value")
                    var trimmedEntry = entry.Trim();
                    if (trimmedEntry.Length == 0 || trimmedEntry[0] == ':')
                    {
                        continue;
                    }

                    var separatorIndex = trimmedEntry.IndexOf(':');
                    if (separatorIndex < 0)
                    {
                        // entries with no separator are allowed (e.g. key1 and key3 in "key1, key2:value2, key3"),
                        // it's a key with no value.
                        var key = trimmedEntry;
                        dictionary[key] = string.Empty;
                    }
                    else if (separatorIndex > 0)
                    {
                        // if a separator is present with no value, we take the value to be empty (e.g. "key1:, key2: ").
                        // note we already did Trim() on the entire entry, so the key portion only needs TrimEnd().
                        var key = trimmedEntry.Substring(0, separatorIndex).TrimEnd();
                        var value = trimmedEntry.Substring(separatorIndex + 1).Trim();
                        dictionary[key] = value;
                    }
                }

                return dictionary;
            };

            globalTags = config
                        .WithKeys(ConfigurationKeys.GlobalTags)
                        .AsDictionaryResult(parser: updatedTagsParser)
                        .OverrideWith(
                             RemapOtelTags(in otelTags),
                             errorLog,
                             () => new DefaultResult<IDictionary<string, string>>(new Dictionary<string, string>(), string.Empty))

                         // Filter out tags with empty keys, and trim whitespace
                        .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                        .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value?.Trim() ?? string.Empty);
        }
        else
        {
            globalTags = config
                        .WithKeys(ConfigurationKeys.GlobalTags)
                        .AsDictionaryResult()
                        .OverrideWith(
                             RemapOtelTags(in otelTags),
                             errorLog,
                             () => new DefaultResult<IDictionary<string, string>>(new Dictionary<string, string>(), string.Empty))

                         // Filter out tags with empty keys or empty values, and trim whitespace
                        .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                        .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim());
        }

        var environment = config
                         .WithKeys(ConfigurationKeys.Environment)
                         .AsString();

        // DD_ENV has precedence over DD_TAGS
        environment = GetExplicitSettingOrTag(environment, globalTags, Tags.Env, ConfigurationKeys.Environment, telemetry);

        var otelServiceName = config.WithKeys(ConfigurationKeys.OpenTelemetry.ServiceName).AsStringResult();
        var serviceName = config
                         .WithKeys(ConfigurationKeys.ServiceName)
                         .AsStringResult()
                         .OverrideWith(in otelServiceName, errorLog);

        // DD_SERVICE has precedence over DD_TAGS
        serviceName = GetExplicitSettingOrTag(serviceName, globalTags, Tags.Service, ConfigurationKeys.ServiceName, telemetry);

        if (tracerSettings.IsRunningInCiVisibility)
        {
            // Set the service name if not set
            var isUserProvidedTestServiceTag = true;
            var ciVisServiceName = serviceName;
            if (string.IsNullOrEmpty(serviceName))
            {
                // Extract repository name from the git url and use it as a default service name.
                ciVisServiceName = TestOptimization.Instance.TracerManagement?.GetServiceNameFromRepository(CIEnvironmentValues.Instance.Repository);
                isUserProvidedTestServiceTag = false;
            }

            globalTags[Ci.Tags.CommonTags.UserProvidedTestServiceTag] = isUserProvidedTestServiceTag ? "true" : "false";

            // Normalize the service name
            ciVisServiceName = NormalizerTraceProcessor.NormalizeService(ciVisServiceName);
            if (ciVisServiceName != serviceName)
            {
                serviceName = ciVisServiceName;
                telemetry.Record(ConfigurationKeys.ServiceName, serviceName, recordValue: true, ConfigurationOrigins.Calculated);
            }
        }

        var serviceVersion = config
                            .WithKeys(ConfigurationKeys.ServiceVersion)
                            .AsString();

        // DD_VERSION has precedence over DD_TAGS
        serviceVersion = GetExplicitSettingOrTag(serviceVersion, globalTags, Tags.Version, ConfigurationKeys.ServiceVersion, telemetry);

        var gitCommitSha = config
                          .WithKeys(ConfigurationKeys.GitCommitSha)
                          .AsString();

        // DD_GIT_COMMIT_SHA has precedence over DD_TAGS
        gitCommitSha = GetExplicitSettingOrTag(gitCommitSha, globalTags, Ci.Tags.CommonTags.GitCommit, ConfigurationKeys.GitCommitSha, telemetry);

        var gitRepositoryUrl = config
                              .WithKeys(ConfigurationKeys.GitRepositoryUrl)
                              .AsString();

        // DD_GIT_REPOSITORY_URL has precedence over DD_TAGS
        gitRepositoryUrl = GetExplicitSettingOrTag(gitRepositoryUrl, globalTags, Ci.Tags.CommonTags.GitRepository, ConfigurationKeys.GitRepositoryUrl, telemetry);

        var otelTraceEnabled = config
                              .WithKeys(ConfigurationKeys.OpenTelemetry.TracesExporter)
                              .AsBoolResult(value => string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)
                                                         ? ParsingResult<bool>.Success(result: false)
                                                         : ParsingResult<bool>.Failure());
        var traceEnabled = config
                          .WithKeys(ConfigurationKeys.TraceEnabled)
                          .AsBoolResult()
                          .OverrideWith(in otelTraceEnabled, errorLog, defaultValue: true);

        if (tracerSettings.AzureAppServiceMetadata?.IsUnsafeToTrace == true)
        {
            telemetry.Record(ConfigurationKeys.TraceEnabled, false, ConfigurationOrigins.Calculated);
            traceEnabled = false;
        }

        var disabledIntegrationNamesArray = config.WithKeys(ConfigurationKeys.DisabledIntegrations)
                                                  .AsString()
                                                 ?.Split([';'], StringSplitOptions.RemoveEmptyEntries) ?? [];

        // If Activity support is enabled, we shouldn't enable the OTel listener
        var disabledIntegrationNames = tracerSettings.IsActivityListenerEnabled
                                           ? new HashSet<string>(disabledIntegrationNamesArray, StringComparer.OrdinalIgnoreCase)
                                           : new HashSet<string>([..disabledIntegrationNamesArray, nameof(IntegrationId.OpenTelemetry)], StringComparer.OrdinalIgnoreCase);

        var integrations = new IntegrationSettingsCollection(source, disabledIntegrationNames);
        RecordDisabledIntegrationsTelemetry(integrations, telemetry);

#pragma warning disable 618 // App analytics is deprecated, but still used
        var analyticsEnabled = config.WithKeys(ConfigurationKeys.GlobalAnalyticsEnabled)
                                     .AsBool(defaultValue: false);
#pragma warning restore 618

#pragma warning disable 618 // this parameter has been replaced but may still be used
        var maxTracesSubmittedPerSecond = config
                                         .WithKeys(ConfigurationKeys.TraceRateLimit)
#pragma warning restore 618
                                         .AsInt32(defaultValue: 100);

        // mutate dictionary to remove without "env", "version", "git.commit.sha" or "git.repository.url" tags
        // these value are used for "Environment" and "ServiceVersion", "GitCommitSha" and "GitRepositoryUrl" properties
        // or overriden with DD_ENV, DD_VERSION, DD_GIT_COMMIT_SHA and DD_GIT_REPOSITORY_URL respectively
        RemoveDisallowedGlobalTags(globalTags);

        var headerTagsNormalizationFixEnabled = config
                                               .WithKeys(ConfigurationKeys.FeatureFlags.HeaderTagsNormalizationFixEnabled)
                                               .AsBool(defaultValue: true);

        // Filter out tags with empty keys or empty values, and trim whitespaces
        var headerTags = InitializeHeaderTags(config.WithKeys(ConfigurationKeys.HeaderTags), headerTagsNormalizationFixEnabled) ?? ReadOnlyDictionary.Empty;

        // Filter out tags with empty keys or empty values, and trim whitespaces
        var grpcTags = InitializeHeaderTags(config.WithKeys(ConfigurationKeys.GrpcTags), headerTagsNormalizationFixEnabled: true) ?? ReadOnlyDictionary.Empty;

        var customSamplingRules = config.WithKeys(ConfigurationKeys.CustomSamplingRules).AsString();

        var globalSamplingRate = BuildSampleRate(errorLog, in config);

        // We need to record a default value for configuration reporting
        // However, we need to keep GlobalSamplingRateInternal null because it changes the behavior of the tracer in subtle ways
        // (= we don't run the sampler at all if it's null, so it changes the tagging of the spans, and it's enforced by system tests)
        if (globalSamplingRate is null)
        {
            telemetry.Record(ConfigurationKeys.GlobalSamplingRate, 1.0, ConfigurationOrigins.Default);
        }

        var startupDiagnosticLogEnabled = config.WithKeys(ConfigurationKeys.StartupDiagnosticLogEnabled).AsBool(defaultValue: true);

        var kafkaCreateConsumerScopeEnabled = config
                                             .WithKeys(ConfigurationKeys.KafkaCreateConsumerScopeEnabled)
                                             .AsBool(defaultValue: true);
        var serviceNameMappings = TracerSettings.TrimConfigKeysValues(config.WithKeys(ConfigurationKeys.ServiceNameMappings)) ?? ReadOnlyDictionary.Empty;

        var tracerMetricsEnabled = config
                                  .WithKeys(ConfigurationKeys.TracerMetricsEnabled)
                                  .AsBool(defaultValue: false);

        var httpServerErrorStatusCodesString = config
                                              .WithKeys(ConfigurationKeys.HttpServerErrorStatusCodes)
                                              .AsString(defaultValue: "500-599");

        var httpServerErrorStatusCodes = ParseHttpCodesToArray(httpServerErrorStatusCodesString);

        var httpClientErrorStatusCodesString = config
                                              .WithKeys(ConfigurationKeys.HttpClientErrorStatusCodes)
                                              .AsString(defaultValue: "400-499");

        var httpClientErrorStatusCodes = ParseHttpCodesToArray(httpClientErrorStatusCodesString);

        return new MutableSettings(
            isInitialSettings: true,
            traceEnabled: traceEnabled,
            customSamplingRules: customSamplingRules,
            customSamplingRulesIsRemote: false, // Can't be remote as these are static sources
            globalSamplingRate: globalSamplingRate,
            logsInjectionEnabled: logsInjectionEnabled,
            globalTags: new(globalTags),
            headerTags: headerTags,
            startupDiagnosticLogEnabled: startupDiagnosticLogEnabled,
            environment: environment,
            serviceName: serviceName,
            defaultServiceName: serviceName ?? tracerSettings.FallbackApplicationName,
            serviceVersion: serviceVersion,
            disabledIntegrationNames: disabledIntegrationNames,
            grpcTags: grpcTags,
            tracerMetricsEnabled: tracerMetricsEnabled,
            integrations: integrations,
            analyticsEnabled: analyticsEnabled,
            maxTracesSubmittedPerSecond: maxTracesSubmittedPerSecond,
            kafkaCreateConsumerScopeEnabled: kafkaCreateConsumerScopeEnabled,
            httpServerErrorStatusCodes: httpServerErrorStatusCodes,
            httpClientErrorStatusCodes: httpClientErrorStatusCodes,
            serviceNameMappings: serviceNameMappings,
            gitRepositoryUrl: gitRepositoryUrl,
            gitCommitSha: gitCommitSha,
            errorLog: errorLog);
    }

    /// <summary>
    /// Creates an instance of <see cref="MutableSettings"/> built
    /// by excluding all the default sources. Effectively gives all the settings their default
    /// values. Should only be used with the manual instrumentation source
    /// </summary>
    public static MutableSettings CreateWithoutDefaultSources(TracerSettings tracerSettings, ConfigurationTelemetry telemetry)
        => CreateInitialMutableSettings(
            NullConfigurationSource.Instance,
            telemetry,
            new OverrideErrorLog(),
            tracerSettings);

    public static MutableSettings CreateForTesting(TracerSettings tracerSettings, Dictionary<string, object?> settings)
        => CreateInitialMutableSettings(
            new DictionaryConfigurationSource(settings.ToDictionary(x => x.Key, x => x.Value?.ToString()!)),
            new ConfigurationTelemetry(),
            new OverrideErrorLog(),
            tracerSettings);

    private static ConfigurationBuilder.ClassConfigurationResultWithKey<IDictionary<string, string>> RemapOtelTags(
        in ConfigurationBuilder.ClassConfigurationResultWithKey<IDictionary<string, string>> original)
    {
        if (original.ConfigurationResult is { IsValid: true, Result: { } values })
        {
            // Update well-known service information resources
            if (values.TryGetValue("deployment.environment", out var envValue))
            {
                values.Remove("deployment.environment");
                values[Tags.Env] = envValue;
            }

            if (values.TryGetValue("service.name", out var serviceValue))
            {
                values.Remove("service.name");
                values[Tags.Service] = serviceValue;
            }

            if (values.TryGetValue("service.version", out var versionValue))
            {
                values.Remove("service.version");
                values[Tags.Version] = versionValue;
            }
        }

        return original;
    }

    private static void RecordDisabledIntegrationsTelemetry(IntegrationSettingsCollection integrations, IConfigurationTelemetry telemetry)
    {
        // Record the final disabled settings values in the telemetry, we can't quite get this information
        // through the IntegrationTelemetryCollector currently so record it here instead
        StringBuilder? sb = null;

        foreach (var setting in integrations.Settings)
        {
            if (setting.Enabled == false)
            {
                sb ??= StringBuilderCache.Acquire();
                sb.Append(setting.IntegrationName);
                sb.Append(';');
            }
        }

        var value = sb is null ? null : StringBuilderCache.GetStringAndRelease(sb);
        telemetry.Record(ConfigurationKeys.DisabledIntegrations, value, recordValue: true, ConfigurationOrigins.Calculated);
    }

    private static double? BuildSampleRate(OverrideErrorLog log, in ConfigurationBuilder config)
    {
        // The "overriding" is complex, so we can't use the usual `OverrideWith()` approach
        var ddSampleRate = config.WithKeys(ConfigurationKeys.GlobalSamplingRate).AsDoubleResult();
        var otelSampleType = config.WithKeys(ConfigurationKeys.OpenTelemetry.TracesSampler).AsStringResult();
        var otelSampleRate = config.WithKeys(ConfigurationKeys.OpenTelemetry.TracesSamplerArg).AsDoubleResult();

        double? ddResult = ddSampleRate.ConfigurationResult.IsValid ? ddSampleRate.ConfigurationResult.Result : null;

        // more complex, so can't use built-in `Merge()` support
        if (ddSampleRate.ConfigurationResult.IsPresent)
        {
            if (otelSampleType.ConfigurationResult.IsPresent)
            {
                log.LogDuplicateConfiguration(ddSampleRate.Key, otelSampleType.Key);
            }

            if (otelSampleRate.ConfigurationResult.IsPresent)
            {
                log.LogDuplicateConfiguration(ddSampleRate.Key, otelSampleRate.Key);
            }
        }
        else if (otelSampleType.ConfigurationResult is { IsValid: true, Result: { } samplerName })
        {
            const string parentbasedAlwaysOn = "parentbased_always_on";
            const string parentbasedAlwaysOff = "parentbased_always_off";
            const string parentbasedTraceidratio = "parentbased_traceidratio";

            string? supportedSamplerName = samplerName switch
            {
                parentbasedAlwaysOn => parentbasedAlwaysOn,
                "always_on" => parentbasedAlwaysOn,
                parentbasedAlwaysOff => parentbasedAlwaysOff,
                "always_off" => parentbasedAlwaysOff,
                parentbasedTraceidratio => parentbasedTraceidratio,
                "traceidratio" => parentbasedTraceidratio,
                _ => null,
            };

            if (supportedSamplerName is null)
            {
                log.EnqueueAction((log, _) =>
                {
                    log.Warning(
                        "OpenTelemetry configuration {OpenTelemetryConfiguration}={OpenTelemetryValue} is not supported. Using default configuration.",
                        otelSampleType.Key,
                        samplerName);
                });
                return ddResult;
            }

            if (!string.Equals(samplerName, supportedSamplerName, StringComparison.OrdinalIgnoreCase))
            {
                log.LogUnsupportedConfiguration(otelSampleType.Key, samplerName, supportedSamplerName);
            }

            var openTelemetrySampleRateResult = supportedSamplerName switch
            {
                parentbasedAlwaysOn => ConfigurationResult<double>.Valid(1.0),
                parentbasedAlwaysOff => ConfigurationResult<double>.Valid(0.0),
                parentbasedTraceidratio => otelSampleRate.ConfigurationResult,
                _ => ConfigurationResult<double>.ParseFailure(),
            };

            if (openTelemetrySampleRateResult is { Result: { } sampleRateResult, IsValid: true })
            {
                return sampleRateResult;
            }

            log.LogInvalidConfiguration(otelSampleRate.Key);
        }

        return ddResult;
    }

    private static string? GetExplicitSettingOrTag(
        string? explicitSetting,
        Dictionary<string, string> globalTags,
        string tag,
        string telemetryKey,
        IConfigurationTelemetry telemetry)
    {
        string? result = null;
        if (!string.IsNullOrWhiteSpace(explicitSetting))
        {
            result = explicitSetting!.Trim();
            if (result != explicitSetting)
            {
                telemetry.Record(telemetryKey, result, recordValue: true, ConfigurationOrigins.Calculated);
            }
        }
        else
        {
            var version = globalTags.GetValueOrDefault(tag);
            if (!string.IsNullOrWhiteSpace(version))
            {
                result = version.Trim();
                telemetry.Record(telemetryKey, result, recordValue: true, ConfigurationOrigins.Calculated);
            }
        }

        return result;
    }
}
