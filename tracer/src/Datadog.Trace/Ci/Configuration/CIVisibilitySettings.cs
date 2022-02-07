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

            // TODO: change the default after the POC to datadoghq.com
            Site = source?.GetString(ConfigurationKeys.Site) ?? "datad0g.com";

            Agentless = source?.GetBool(ConfigurationKeys.CIVisibilityAgentlessEnabled) ?? false;

            // By default intake payloads has a 5MB limit
            MaximumAgentlessPayloadSize = 5 * 1024 * 1024;

            TracerSettings = new TracerSettings(source) ?? TracerSettings.FromDefaultSources();
        }

        /// <summary>
        /// Gets a value indicating whether the CI Visibility mode was enabled by configuration
        /// </summary>
        public bool Enabled { get; }

        /// <summary>
        /// Gets the Api Key to use in Agentless mode
        /// Note: This enables the Agentless mode
        /// </summary>
        public string ApiKey { get; }

        /// <summary>
        /// Gets the Datadog site
        /// </summary>
        public string Site { get; }

        /// <summary>
        /// Gets the maximum agentless payload size
        /// </summary>
        public int MaximumAgentlessPayloadSize { get; }

        /// <summary>
        /// Gets a value indicating whether the Agentless writer is going to be used.
        /// </summary>
        public bool Agentless { get; }

        /// <summary>
        /// Gets the tracer settings
        /// </summary>
        public TracerSettings TracerSettings { get; }

        public static CIVisibilitySettings FromDefaultSources()
        {
            var source = GlobalSettings.CreateDefaultConfigurationSource();
            return new CIVisibilitySettings(source);
        }
    }
}
