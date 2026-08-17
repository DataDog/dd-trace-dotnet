// <copyright file="FeatureFlagsSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
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

    internal const int DefaultPollIntervalSeconds = 30;
    internal const int DefaultRequestTimeoutSeconds = 5;
    internal const int DefaultInitializationTimeoutMs = 10_000;

    // An interval above this is indistinguishable from "never poll" and is more likely a
    // misconfiguration (for example milliseconds passed as seconds) than an intent.
    private const int MaxPollIntervalSeconds = 3600;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FeatureFlagsSettings));

    public FeatureFlagsSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry, string? site, string? env, string? apiKey)
    {
        source ??= NullConfigurationSource.Instance;
        var config = new ConfigurationBuilder(source, telemetry);

        // Read as nullable: the precedence rules distinguish "explicitly provided" from "left unset",
        // so a default value here would erase the difference the legacy key depends on.
        var enabled = config.WithKeys(ConfigurationKeys.FeatureFlags.FeatureFlagsEnabled).AsBool();
#pragma warning disable 618 // superseded, but still honoured so existing adopters keep their source
        var legacyEnabled = config.WithKeys(ConfigurationKeys.FeatureFlags.FlaggingProviderEnabled).AsBool();
#pragma warning restore 618

        // Use GetAsClass so the config framework records both the raw string and the resolved
        // value in configuration telemetry. The converter maps recognised values to a
        // SourceSelection record, returns Failure for unrecognised values (so the framework
        // records the fallback), and returns Failure for null/empty (so "not set" also falls
        // back to the default, which is null — meaning "not explicitly set").
        var configuredSource = config
            .WithKeys(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource)
            .GetAsClass<SourceSelection>(
                validator: null,
                converter: ConvertSource);

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

        var agentlessBaseUrl = config
                                .WithKeys(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessBaseUrl)
                                .AsRedactedString();
        AgentlessBaseUrl = !StringUtil.IsNullOrWhiteSpace(agentlessBaseUrl) ? agentlessBaseUrl : null;

        PollInterval = TimeSpan.FromSeconds(
            config.WithKeys(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessPollIntervalSeconds)
                  .AsInt32(DefaultPollIntervalSeconds, v => v > 0 && v <= MaxPollIntervalSeconds)
                  .Value);

        RequestTimeout = TimeSpan.FromSeconds(
            config.WithKeys(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessRequestTimeoutSeconds)
                  .AsInt32(DefaultRequestTimeoutSeconds, v => v > 0)
                  .Value);

        Site = StringUtil.IsNullOrWhiteSpace(site) ? DefaultSite : site;
        Env = env;
        ApiKey = apiKey;

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
    {
        var source = GlobalConfigurationSource.Instance;
        var config = new ConfigurationBuilder(source, TelemetryFactory.Config);
        var site = config.WithKeys(ConfigurationKeys.Site).AsString(DefaultSite, s => !StringUtil.IsNullOrWhiteSpace(s));
        var env = config.WithKeys(ConfigurationKeys.Environment).AsString();
        var apiKey = config.WithKeys(ConfigurationKeys.ApiKey).AsRedactedString();
        return new FeatureFlagsSettings(source, TelemetryFactory.Config, site, env, apiKey);
    }

    /// <summary>
    /// Converts a configuration source string to a <see cref="SourceSelection"/>.
    /// Used as the converter for GetAsClass so the
    /// config framework records both the raw string and the resolved value in telemetry.
    /// </summary>
    private static ParsingResult<SourceSelection> ConvertSource(string? value)
    {
        if (value is null)
        {
            return ParsingResult<SourceSelection>.Failure();
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return ParsingResult<SourceSelection>.Failure();
        }

        return normalized switch
        {
            AgentlessSourceName => ParsingResult<SourceSelection>.Success(new SourceSelection(FeatureFlagsSource.Agentless, isValid: true)),
            RemoteConfigSourceName => ParsingResult<SourceSelection>.Success(new SourceSelection(FeatureFlagsSource.RemoteConfig, isValid: true)),
            OfflineSourceName => ParsingResult<SourceSelection>.Success(new SourceSelection(FeatureFlagsSource.Disabled, isValid: true)),
            _ => ParsingResult<SourceSelection>.Success(new SourceSelection(FeatureFlagsSource.Disabled, isValid: false)),
        };
    }

    /// <summary>
    /// Resolves the delivery source. Shared across tracers, so the ordering is deliberate:
    /// the stable kill switch wins over everything, an explicit source wins over the legacy key
    /// (and fails closed when unrecognised), the legacy key grandfathers existing adopters onto
    /// Remote Configuration, and everything else defaults to agentless.
    /// </summary>
    private static FeatureFlagsSource ResolveSource(bool? enabled, SourceSelection? configuredSource, bool? legacyEnabled)
    {
        if (enabled == false)
        {
            return FeatureFlagsSource.Disabled;
        }

        if (configuredSource is not null)
        {
            // "offline" is a reserved fail-closed sentinel: the provider is intentionally off.
            // An invalid value also fails closed. Both are mapped to Disabled by the converter.
            return configuredSource.Source;
        }

        // The legacy key only grandfathers adopters who have not migrated: an explicit new-key
        // value (true or false) takes precedence, so the legacy key is consulted only when the
        // new key was left unset.
        if (enabled is null && legacyEnabled is not null)
        {
            return legacyEnabled.Value ? FeatureFlagsSource.RemoteConfig : FeatureFlagsSource.Disabled;
        }

        return FeatureFlagsSource.Agentless;
    }
}
