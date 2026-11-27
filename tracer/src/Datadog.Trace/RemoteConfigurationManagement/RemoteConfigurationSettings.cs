// <copyright file="RemoteConfigurationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal sealed class RemoteConfigurationSettings
    {
        internal const double DefaultPollIntervalSeconds = 5;

        public RemoteConfigurationSettings(IConfigurationSource? configurationSource, IConfigurationTelemetry telemetry)
        {
            configurationSource ??= NullConfigurationSource.Instance;

            var config = new ConfigurationBuilder(configurationSource, telemetry);
            var pollInterval = config
                              .WithKeys(ConfigurationKeys.Rcm.PollInterval)
                              .AsDouble(DefaultPollIntervalSeconds, pollInterval => pollInterval is > 0 and <= 5);

            PollInterval = TimeSpan.FromSeconds(pollInterval.Value);

            NativeClientEnabled = config
                                 .WithKeys(ConfigurationKeys.Rcm.RemoteConfigurationNativeClientEnabled)
                                 .AsBool(defaultValue: true);
        }

        public TimeSpan PollInterval { get; }

        /// <summary>
        /// Gets a value indicating whether libdatadog's client is used where available, rather than the managed one.
        /// See <see cref="ConfigurationKeys.Rcm.RemoteConfigurationNativeClientEnabled"/>.
        /// </summary>
        public bool NativeClientEnabled { get; }

        public static RemoteConfigurationSettings FromSource(IConfigurationSource source, IConfigurationTelemetry telemetry)
            => new(source, telemetry);

        public static RemoteConfigurationSettings FromDefaultSource()
            => FromSource(GlobalConfigurationSource.Instance, TelemetryFactory.Config);
    }
}
