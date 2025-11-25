// <copyright file="GlobalSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.LibDatadog;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using LogEventLevel = Datadog.Trace.Vendors.Serilog.Events.LogEventLevel;

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

            DebugEnabled = builder
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
        public bool DebugEnabled { get; private set; }

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

        /// <summary>
        /// Set whether debug mode is enabled.
        /// Affects the level of logs written to file.
        /// </summary>
        /// <param name="enabled">Whether debug is enabled.</param>
        internal static void SetDebugEnabled(bool enabled)
        {
            Instance.DebugEnabled = enabled;

            if (enabled)
            {
                DatadogLogging.SetLogLevel(LogEventLevel.Debug);
            }
            else
            {
                DatadogLogging.UseDefaultLevel();
            }

            LibDatadog.Logging.Logger.Instance.SetLogLevel(debugEnabled: enabled);

            TelemetryFactory.Config.Record(ConfigurationKeys.DebugEnabled, enabled, ConfigurationOrigins.Code);
        }

        private static GlobalSettings CreateFromDefaultSources()
            => new(GlobalConfigurationSource.Instance, TelemetryFactory.Config, OverrideErrorLog.Instance);
    }
}
