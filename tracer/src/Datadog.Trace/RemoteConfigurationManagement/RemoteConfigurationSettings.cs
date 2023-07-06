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
    internal class RemoteConfigurationSettings
    {
        internal const double DefaultPollIntervalSeconds = 5;

        public RemoteConfigurationSettings(IConfigurationSource? configurationSource, IConfigurationTelemetry telemetry)
        {
            configurationSource ??= NullConfigurationSource.Instance;

            RuntimeId = Util.RuntimeId.Get();
            TracerVersion = TracerConstants.ThreePartVersion;

            var pollInterval = new ConfigurationBuilder(configurationSource, telemetry)
#pragma warning disable CS0618
                              .WithKeys(ConfigurationKeys.Rcm.PollInterval, ConfigurationKeys.Rcm.PollIntervalInternal)
#pragma warning restore CS0618
                              .AsDouble(DefaultPollIntervalSeconds, pollInterval => pollInterval is > 0 and <= 5);

            PollInterval = TimeSpan.FromSeconds(pollInterval.Value);
        }

        public string RuntimeId { get; }

        public string TracerVersion { get; }

        public TimeSpan PollInterval { get; }

        public static RemoteConfigurationSettings FromSource(IConfigurationSource source, IConfigurationTelemetry telemetry)
            => new(source, telemetry);

        public static RemoteConfigurationSettings FromDefaultSource()
            => FromSource(GlobalConfigurationSource.Instance, TelemetryFactory.Config);
    }
}
