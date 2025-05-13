// <copyright file="GlobalSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains global datadog settings.
    /// </summary>
    public class GlobalSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        /// <param name="telemetry">Records the origin of telemetry values</param>
        /// <param name="overrideHandler">Records any errors </param>
        internal GlobalSettings(
            IConfigurationSource source,
            IConfigurationTelemetry telemetry,
            IConfigurationOverrideHandler overrideHandler)
        {
            var builder = new ConfigurationBuilder(source, telemetry);

            var otelConfig = builder
                            .WithKeys(ConfigurationKeys.OpenTelemetry.LogLevel)
                            .AsBoolResult(
                                 value => string.Equals(value, "debug", StringComparison.OrdinalIgnoreCase)
                                              ? ParsingResult<bool>.Success(result: true)
                                              : ParsingResult<bool>.Failure());

            DebugEnabledInternal = builder
                                  .WithKeys(ConfigurationKeys.DebugEnabled)
                                  .AsBoolResult()
                                  .OverrideWith(in otelConfig, overrideHandler, false);

            DiagnosticSourceEnabled = builder
                                     .WithKeys(ConfigurationKeys.DiagnosticSourceEnabled)
                                     .AsBool(true);
        }

        /// <summary>
        /// Gets a value indicating whether debug mode is enabled.
        /// Default is <c>false</c>.
        /// Set in code via <see cref="SetDebugEnabled"/>
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DebugEnabled"/>
        public bool DebugEnabled
        {
            get
            {
                TelemetryFactory.Metrics.Record(PublicApiUsage.GlobalSettings_DebugEnabled_Get);
                return DebugEnabledInternal;
            }
        }

        /// <summary>
        /// Gets the global settings instance.
        /// </summary>
        internal static GlobalSettings Instance { get; private set; } = CreateFromDefaultSources();

        /// <summary>
        /// Gets a value indicating whether the use
        /// of System.Diagnostics.DiagnosticSource is enabled.
        /// This value can only be set with environment variables
        /// or a configuration file, not through code.
        /// </summary>
        internal bool DiagnosticSourceEnabled { get; }

        internal bool DebugEnabledInternal { get; private set; }

        /// <summary>
        /// Set whether debug mode is enabled.
        /// Affects the level of logs written to file.
        /// </summary>
        /// <param name="enabled">Whether debug is enabled.</param>
        [PublicApi]
        public static void SetDebugEnabled(bool enabled)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.GlobalSettings_SetDebugEnabled);
            SetDebugEnabledInternal(enabled);
        }

        internal static void SetDebugEnabledInternal(bool enabled)
        {
            Instance.DebugEnabledInternal = enabled;

            if (enabled)
            {
                DatadogLogging.SetLogLevel(LogEventLevel.Debug);
            }
            else
            {
                DatadogLogging.UseDefaultLevel();
            }

            TelemetryFactory.Config.Record(ConfigurationKeys.DebugEnabled, enabled, ConfigurationOrigins.Code);
        }

        /// <summary>
        /// Used to refresh global settings when environment variables or config sources change.
        /// This is not necessary if changes are set via code, only environment.
        /// </summary>
        [PublicApi]
        public static void Reload()
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.GlobalSettings_Reload);
            DatadogLogging.Reset();
            GlobalConfigurationSource.Reload();
            Instance = CreateFromDefaultSources();
        }

        /// <summary>
        /// Create a <see cref="GlobalSettings"/> populated from the default sources
        /// returned by <see cref="GlobalConfigurationSource.Instance"/>.
        /// </summary>
        /// <returns>A <see cref="TracerSettings"/> populated from the default sources.</returns>
        [PublicApi]
        public static GlobalSettings FromDefaultSources()
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.GlobalSettings_FromDefaultSources);
            return CreateFromDefaultSources();
        }

        private static GlobalSettings CreateFromDefaultSources()
            => new(GlobalConfigurationSource.Instance, TelemetryFactory.Config, OverrideErrorLog.Instance);
    }
}
