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
    // Matches the Go, Java and Node tracers, so the same slow first configuration does not give
    // one language the caller's default value while the others still return a real one.
    internal const int DefaultInitializationTimeoutMs = 30_000;

    // One hour. An interval above this is more likely a misconfiguration (for example milliseconds
    // passed as seconds) than an intent, and the other tracers cap it at the same value.
    private const int MaxPollIntervalSeconds = 3600;

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

        // The source key is read once, with every other outcome expressed as its default, so
        // configuration telemetry reports the one value we act on rather than one entry per
        // candidate key. Shared across tracers, so the precedence is deliberate: the stable kill
        // switch wins over everything (expressed as a validator that rejects any configured value),
        // an explicit source wins over the legacy key (an unrecognised one fails closed, so a typo
        // never starts billed delivery), the legacy key grandfathers existing adopters onto Remote
        // Configuration, and everything else defaults to agentless.
        // Not a tuple pattern: net461 has no System.ValueTuple, so (enabled, legacyEnabled) switch
        // does not compile there.
        DefaultResult<FeatureFlagsSource> defaultSource = enabled switch
        {
            false => new(FeatureFlagsSource.Offline, OfflineSourceName),
            null when legacyEnabled is not null => legacyEnabled.Value
                                                       ? new(FeatureFlagsSource.RemoteConfig, RemoteConfigSourceName)
                                                       : new(FeatureFlagsSource.Offline, OfflineSourceName),
            _ => new(FeatureFlagsSource.Agentless, AgentlessSourceName),
        };

        Source = config
                .WithKeys(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource)
                .GetAs(
                     defaultSource,
                     validator: enabled == false ? static _ => false : static _ => true,
                     converter: ConvertSource);

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

        // DD_SITE and DD_API_KEY are not extracted on TracerSettings, so they are read here as the
        // other product settings do (TelemetrySettings, DirectLogSubmissionSettings). DD_ENV is not
        // read at all: it can be changed in code while the application runs, so the delivery source
        // takes the current value per request rather than capturing one here.
        Site = config
              .WithKeys(ConfigurationKeys.Site)
              .AsString(DefaultSite, static site => !StringUtil.IsNullOrWhiteSpace(site));
        ApiKey = config.WithKeys(ConfigurationKeys.ApiKey).AsRedactedString();

        var initializationTimeoutMs = config
                                     .WithKeys(ConfigurationKeys.FeatureFlags.FlaggingProviderInitializationTimeoutMs)
                                     .AsInt32(DefaultInitializationTimeoutMs, timeout => timeout > 0)
                                     .Value;
        InitializationTimeout = TimeSpan.FromMilliseconds(initializationTimeoutMs);
    }

    /// <summary>
    /// Gets the resolved delivery source. <see cref="FeatureFlagsSource.Offline"/> means nothing is contacted.
    /// </summary>
    public FeatureFlagsSource Source { get; }

    /// <summary>
    /// Gets a value indicating whether Feature Flags are enabled at all.
    /// </summary>
    public bool Enabled => Source != FeatureFlagsSource.Offline;

    /// <summary>
    /// Gets the configured override for the agentless endpoint, or <c>null</c> to derive it from the site.
    /// </summary>
    public string? AgentlessBaseUrl { get; }

    /// <summary>
    /// Gets the Datadog site the managed agentless endpoint is derived from.
    /// </summary>
    public string Site { get; }

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

    /// <summary>
    /// Converts a configured source name to a <see cref="FeatureFlagsSource"/>. A blank value is
    /// treated as unset and falls back to the default, which is what an absent key would have
    /// selected anyway. An unrecognised value fails closed instead: nothing is contacted.
    /// </summary>
    private static ParsingResult<FeatureFlagsSource> ConvertSource(string? value)
    {
        if (StringUtil.IsNullOrWhiteSpace(value))
        {
            return ParsingResult<FeatureFlagsSource>.Failure();
        }

        if (TryMatch(value.Trim(), out var source))
        {
            return ParsingResult<FeatureFlagsSource>.Success(source);
        }

        // A value nobody recognises fails closed rather than falling back to agentless: guessing a
        // billed delivery path from a typo is worse than delivering nothing. Shared across tracers,
        // and asserted by the system-tests parametric suite.
        Log.Warning<string>(
            "Unsupported Feature Flags configuration source {Source}. No configuration will be delivered.",
            value);

        return ParsingResult<FeatureFlagsSource>.Success(FeatureFlagsSource.Offline);
    }

    private static bool TryMatch(string value, out FeatureFlagsSource source)
    {
        if (string.Equals(value, AgentlessSourceName, StringComparison.OrdinalIgnoreCase))
        {
            source = FeatureFlagsSource.Agentless;
            return true;
        }

        if (string.Equals(value, RemoteConfigSourceName, StringComparison.OrdinalIgnoreCase))
        {
            source = FeatureFlagsSource.RemoteConfig;
            return true;
        }

        // "offline" is a reserved fail-closed sentinel: the provider is intentionally off.
        if (string.Equals(value, OfflineSourceName, StringComparison.OrdinalIgnoreCase))
        {
            source = FeatureFlagsSource.Offline;
            return true;
        }

        source = FeatureFlagsSource.Offline;
        return false;
    }
}
