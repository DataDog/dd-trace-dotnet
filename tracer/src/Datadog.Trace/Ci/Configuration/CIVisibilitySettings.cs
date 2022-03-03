// <copyright file="CIVisibilitySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Ci.Configuration
{
    internal class CIVisibilitySettings
    {
        public CIVisibilitySettings(IConfigurationSource source)
        {
            Enabled = source?.GetBool(ConfigurationKeys.CIVisibility.Enabled) ?? false;
            Agentless = source?.GetBool(ConfigurationKeys.CIVisibility.AgentlessEnabled) ?? false;
            Logs = source?.GetBool(ConfigurationKeys.CIVisibility.Logs) ?? false;
            ApiKey = source?.GetString(ConfigurationKeys.ApiKey);
            Site = source?.GetString(ConfigurationKeys.Site) ?? "datadoghq.com";

            // By default intake payloads has a 5MB limit
            MaximumAgentlessPayloadSize = 5 * 1024 * 1024;

            ProxyHttps = source?.GetString(ConfigurationKeys.Proxy.ProxyHttps);
            var proxyNoProxy = source?.GetString(ConfigurationKeys.Proxy.ProxyNoProxy) ?? string.Empty;
            ProxyNoProxy = proxyNoProxy.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            TracerSettings = new TracerSettings(source);

            if (Logs)
            {
                // On agentless we also enable the direct log submission
                TracerSettings.LogSubmissionSettings.DirectLogSubmissionEnabledIntegrations.Add("XUnit,Serilog,ILogger,Log4Net,NLog");
                TracerSettings.LogSubmissionSettings.DirectLogSubmissionBatchPeriod = TimeSpan.FromSeconds(1);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the CI Visibility mode was enabled by configuration
        /// </summary>
        public bool Enabled { get; }

        /// <summary>
        /// Gets a value indicating whether the Agentless writer is going to be used.
        /// </summary>
        public bool Agentless { get; }

        /// <summary>
        /// Gets the Api Key to use in Agentless mode
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
        /// Gets the https proxy
        /// </summary>
        public string ProxyHttps { get; }

        /// <summary>
        /// Gets the no proxy list
        /// </summary>
        public string[] ProxyNoProxy { get; }

        /// <summary>
        /// Gets a value indicating whether the Logs submission is going to be used.
        /// </summary>
        public bool Logs { get; }

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
