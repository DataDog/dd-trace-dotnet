// <copyright file="FeatureFlagsSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;

namespace Datadog.Trace.FeatureFlags;

/// <summary>
/// Feature Flags configuration: which delivery source is selected, and how the agentless
/// source is operated.
/// </summary>
internal sealed class FeatureFlagsSettings
{
    internal const string AgentlessSourceName = "agentless";
    internal const string RemoteConfigSourceName = "remote_config";
    internal const string OfflineSourceName = "offline";

    internal const string DefaultSite = "datadoghq.com";

    internal const double DefaultPollIntervalSeconds = 30;
    internal const double DefaultRequestTimeoutSeconds = 5;
    internal const int DefaultInitializationTimeoutMs = 10_000;

    // An interval above this is indistinguishable from "never poll" and is more likely a
    // misconfiguration (for example milliseconds passed as seconds) than an intent.
    private const double MaxPollIntervalSeconds = 3600;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FeatureFlagsSettings));

    public FeatureFlagsSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry)
    {
        source ??= NullConfigurationSource.Instance;
        var config = new ConfigurationBuilder(source, telemetry);

        // Read as nullable: the precedence rules distinguish "explicitly provided" from "left unset",
        // so a default value here would erase the difference the legacy key depends on.
        var enabled = config.WithKeys(ConfigurationKeys.FeatureFlags.FeatureFlagsEnabled).AsBool();
#pragma warning disable 618 // superseded, but still honoured so existing adopters keep their source
        var legacyEnabled = config.WithKeys(ConfigurationKeys.FeatureFlags.FlaggingProviderEnabled).AsBool();
#pragma warning restore 618
        var configuredSource = config.WithKeys(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource).AsString();

        if (legacyEnabled is not null)
        {
#pragma warning disable 618
            Log.Warning<string, string, string>(
                "{LegacyKey} is deprecated. Use {EnabledKey} and {SourceKey} instead.",
                ConfigurationKeys.FeatureFlags.FlaggingProviderEnabled,
                ConfigurationKeys.FeatureFlags.FeatureFlagsEnabled,
                ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource);
#pragma warning restore 618
        }

        Source = ResolveSource(enabled, configuredSource, legacyEnabled);

        AgentlessBaseUrl = config
                          .WithKeys(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessBaseUrl)
                          .AsString(url => !StringUtil.IsNullOrEmpty(url?.Trim()));

        PollInterval = TimeSpan.FromSeconds(
            InRangeOrDefault(
                config.WithKeys(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessPollIntervalSeconds).AsDouble(),
                ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessPollIntervalSeconds,
                DefaultPollIntervalSeconds,
                MaxPollIntervalSeconds));

        RequestTimeout = TimeSpan.FromSeconds(
            InRangeOrDefault(
                config.WithKeys(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessRequestTimeoutSeconds).AsDouble(),
                ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessRequestTimeoutSeconds,
                DefaultRequestTimeoutSeconds,
                maximumSeconds: null));

        Site = config
              .WithKeys(ConfigurationKeys.Site)
              .AsString(DefaultSite, site => !StringUtil.IsNullOrEmpty(site?.Trim()));

        Env = config.WithKeys(ConfigurationKeys.Environment).AsString();

        ApiKey = config.WithKeys(ConfigurationKeys.ApiKey).AsRedactedString();

        var initializationTimeoutMs = config
                                     .WithKeys(ConfigurationKeys.FeatureFlags.FlaggingProviderInitializationTimeoutMs)
                                     .AsInt32(DefaultInitializationTimeoutMs, timeout => timeout > 0)
                                     .Value;
        InitializationTimeout = TimeSpan.FromMilliseconds(initializationTimeoutMs);
    }

    /// <summary>
    /// Gets the resolved delivery source. <see cref="FeatureFlagsSource.Disabled"/> means nothing is contacted.
    /// </summary>
    public FeatureFlagsSource Source { get; }

    /// <summary>
    /// Gets a value indicating whether Feature Flags are enabled at all.
    /// </summary>
    public bool Enabled => Source != FeatureFlagsSource.Disabled;

    /// <summary>
    /// Gets the configured override for the agentless endpoint, or <c>null</c> to derive it from the site.
    /// </summary>
    public string? AgentlessBaseUrl { get; }

    /// <summary>
    /// Gets the Datadog site the managed agentless endpoint is derived from.
    /// </summary>
    public string Site { get; }

    /// <summary>
    /// Gets the configured environment, sent to the agentless endpoint as <c>dd_env</c>.
    /// </summary>
    public string? Env { get; }

    /// <summary>
    /// Gets the API key, required by the managed agentless endpoint.
    /// </summary>
    public string? ApiKey { get; }

    /// <summary>
    /// Gets how often the agentless source polls for configuration.
    /// </summary>
    public TimeSpan PollInterval { get; }

    /// <summary>
    /// Gets the per-request timeout used by the agentless source.
    /// </summary>
    public TimeSpan RequestTimeout { get; }

    /// <summary>
    /// Gets how long provider initialization waits for the first configuration.
    /// </summary>
    public TimeSpan InitializationTimeout { get; }

    public static FeatureFlagsSettings FromDefaultSource()
        => new(GlobalConfigurationSource.Instance, TelemetryFactory.Config);

    /// <summary>
    /// Resolves the delivery source. Shared across tracers, so the ordering is deliberate:
    /// the stable kill switch wins over everything, an explicit source wins over the legacy key
    /// (and fails closed when unrecognised), the legacy key grandfathers existing adopters onto
    /// Remote Configuration, and everything else defaults to agentless.
    /// </summary>
    internal static FeatureFlagsSource ResolveSource(bool? enabled, string? configuredSource, bool? legacyEnabled)
    {
        var normalizedSource = NormalizeSource(configuredSource);

        if (enabled == false)
        {
            return FeatureFlagsSource.Disabled;
        }

        if (normalizedSource is not null)
        {
            switch (normalizedSource)
            {
                case AgentlessSourceName:
                    return FeatureFlagsSource.Agentless;
                case RemoteConfigSourceName:
                    return FeatureFlagsSource.RemoteConfig;
                case OfflineSourceName:
                    // Reserved fail-closed sentinel: the provider is intentionally off, so no warning.
                    return FeatureFlagsSource.Disabled;
                default:
                    Log.Warning(
                        "Unsupported {SourceKey} value '{Source}'. Feature Flags are disabled.",
                        ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource,
                        normalizedSource);
                    return FeatureFlagsSource.Disabled;
            }
        }

        if (legacyEnabled is not null)
        {
            return legacyEnabled.Value ? FeatureFlagsSource.RemoteConfig : FeatureFlagsSource.Disabled;
        }

        return FeatureFlagsSource.Agentless;
    }

    /// <summary>
    /// An empty or whitespace-only source is semantically unset, not an unrecognised value.
    /// </summary>
    private static string? NormalizeSource(string? configuredSource)
    {
        if (configuredSource is null)
        {
            return null;
        }

        var normalized = configuredSource.Trim().ToLowerInvariant();
        return normalized.Length == 0 ? null : normalized;
    }

    internal static double InRangeOrDefault(double? configured, string key, double defaultSeconds, double? maximumSeconds)
    {
        if (configured is null)
        {
            return defaultSeconds;
        }

        var value = configured.Value;
        if (value <= 0 || (maximumSeconds is { } maximum && value > maximum))
        {
            // A non-positive interval would turn polling into a tight loop against the endpoint,
            // so an out-of-range value is rejected rather than honoured.
            Log.Warning<string, double, double>(
                "Invalid value {Key}={Value}. Using {Default} seconds instead.",
                key,
                value,
                defaultSeconds);
            return defaultSeconds;
        }

        return value;
    }
}
