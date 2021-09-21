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
            if (Enabled)
            {
                TracerSettings = new TracerSettings(source);
            }
        }

        public bool Enabled { get; set; }

        public TracerSettings TracerSettings { get; set; }

        public static CIVisibilitySettings FromDefaultSources()
        {
            var source = GlobalSettings.CreateDefaultConfigurationSource();
            return new CIVisibilitySettings(source);
        }
    }
}
