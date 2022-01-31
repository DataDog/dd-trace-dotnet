// <copyright file="CIVisibilitySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;

namespace Datadog.Trace.Ci.Configuration
{
    internal class CIVisibilitySettings
    {
        public CIVisibilitySettings(IConfigurationSource source)
        {
            Enabled = source?.GetBool(ConfigurationKeys.CIVisibilityEnabled) ?? false;
            ApiKey = source?.GetString(ConfigurationKeys.ApiKey);
            TracerSettings = new TracerSettings(source) ?? TracerSettings.FromDefaultSources();
        }

        public bool Enabled { get; }

        public string ApiKey { get; }

        public TracerSettings TracerSettings { get; }

        public static CIVisibilitySettings FromDefaultSources()
        {
            var source = GlobalSettings.CreateDefaultConfigurationSource();
            return new CIVisibilitySettings(source);
        }
    }
}
