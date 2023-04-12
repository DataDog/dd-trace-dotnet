// <copyright file="RemoteConfigurationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal class RemoteConfigurationSettings
    {
        private const int DefaultPollIntervalMilliseconds = 5000;

        public RemoteConfigurationSettings()
            : this(configurationSource: null)
        {
        }

        public RemoteConfigurationSettings(IConfigurationSource? configurationSource)
        {
            configurationSource ??= NullConfigurationSource.Instance;

            Id = Guid.NewGuid().ToString();
            RuntimeId = Util.RuntimeId.Get();
            TracerVersion = TracerConstants.ThreePartVersion;

            var pollInterval =
                configurationSource.GetInt32(ConfigurationKeys.Rcm.PollInterval)
#pragma warning disable CS0618
                    ?? configurationSource.GetInt32(ConfigurationKeys.Rcm.PollIntervalInternal);
#pragma warning restore CS0618

            pollInterval =
                pollInterval is null or <= 0 or > 5000
                    ? DefaultPollIntervalMilliseconds
                    : pollInterval.Value;

            PollInterval = TimeSpan.FromMilliseconds(pollInterval.Value);
        }

        public string Id { get; }

        public string RuntimeId { get; }

        public string TracerVersion { get; }

        public TimeSpan PollInterval { get; }

        public static RemoteConfigurationSettings FromSource(IConfigurationSource source)
        {
            return new RemoteConfigurationSettings(source);
        }

        public static RemoteConfigurationSettings FromDefaultSource()
        {
            return FromSource(GlobalConfigurationSource.Instance);
        }
    }
}
