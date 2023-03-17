// <copyright file="CIVisibilitySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Ci.Configuration
{
    internal class CIVisibilitySettings
    {
        private TracerSettings? _tracerSettings;

        public CIVisibilitySettings(IConfigurationSource source)
        {
            Enabled = source?.GetBool(ConfigurationKeys.CIVisibility.Enabled) ?? false;
            Agentless = source?.GetBool(ConfigurationKeys.CIVisibility.AgentlessEnabled) ?? false;
            Logs = source?.GetBool(ConfigurationKeys.CIVisibility.Logs) ?? false;
            ApiKey = source?.GetString(ConfigurationKeys.ApiKey);
            ApplicationKey = source?.GetString(ConfigurationKeys.ApplicationKey);
            Site = source?.GetString(ConfigurationKeys.Site) ?? "datadoghq.com";
            AgentlessUrl = source?.GetString(ConfigurationKeys.CIVisibility.AgentlessUrl);

            // By default intake payloads has a 5MB limit
            MaximumAgentlessPayloadSize = 5 * 1024 * 1024;

            ProxyHttps = source?.GetString(ConfigurationKeys.Proxy.ProxyHttps);
            var proxyNoProxy = source?.GetString(ConfigurationKeys.Proxy.ProxyNoProxy) ?? string.Empty;
            ProxyNoProxy = proxyNoProxy.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Intelligent Test Runner
            IntelligentTestRunnerEnabled = source?.GetBool(ConfigurationKeys.CIVisibility.IntelligentTestRunnerEnabled) ?? true;

            // Tests skipping
            TestsSkippingEnabled = source?.GetBool(ConfigurationKeys.CIVisibility.TestsSkippingEnabled);

            // Code coverage
            CodeCoverageEnabled = source?.GetBool(ConfigurationKeys.CIVisibility.CodeCoverage);
            CodeCoverageSnkFilePath = source?.GetString(ConfigurationKeys.CIVisibility.CodeCoverageSnkFile);
            CodeCoveragePath = source?.GetString(ConfigurationKeys.CIVisibility.CodeCoveragePath);
            CodeCoverageEnableJitOptimizations = source?.GetBool(ConfigurationKeys.CIVisibility.CodeCoverageEnableJitOptimizations) ?? true;

            // Git upload
            GitUploadEnabled = source?.GetBool(ConfigurationKeys.CIVisibility.GitUploadEnabled);

            // Force evp proxy
            ForceAgentsEvpProxy = source?.GetBool(ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy) ?? false;
        }

        /// <summary>
        /// Gets a value indicating whether the CI Visibility mode was enabled by configuration
        /// </summary>
        public bool Enabled { get; }

        /// <summary>
        /// Gets a value indicating whether the Agentless writer is going to be used.
        /// </summary>
        public bool Agentless { get; private set; }

        /// <summary>
        /// Gets the Agentless url.
        /// </summary>
        public string? AgentlessUrl { get; private set; }

        /// <summary>
        /// Gets the Api Key to use in Agentless mode
        /// </summary>
        public string? ApiKey { get; private set; }

        /// <summary>
        /// Gets the Application Key to use in ITR
        /// </summary>
        public string? ApplicationKey { get; private set; }

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
        public string? ProxyHttps { get; }

        /// <summary>
        /// Gets the no proxy list
        /// </summary>
        public string[]? ProxyNoProxy { get; }

        /// <summary>
        /// Gets a value indicating whether the Logs submission is going to be used.
        /// </summary>
        public bool Logs { get; }

        /// <summary>
        /// Gets a value indicating whether the Code Coverage is enabled.
        /// </summary>
        public bool? CodeCoverageEnabled { get; private set; }

        /// <summary>
        /// Gets the snk filepath to re-signing assemblies after the code coverage modification.
        /// </summary>
        public string? CodeCoverageSnkFilePath { get; }

        /// <summary>
        /// Gets the path to store the code coverage json files.
        /// </summary>
        public string? CodeCoveragePath { get; }

        /// <summary>
        /// Gets a value indicating whether the Code Coverage Jit Optimizations should be enabled
        /// </summary>
        public bool CodeCoverageEnableJitOptimizations { get; }

        /// <summary>
        /// Gets a value indicating whether the Git Upload metadata is going to be used.
        /// </summary>
        public bool? GitUploadEnabled { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the Intelligent Test Runner Tests skipping feature is enabled.
        /// </summary>
        public bool? TestsSkippingEnabled { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the Intelligent Test Runner is enabled.
        /// </summary>
        public bool IntelligentTestRunnerEnabled { get; private set; }

        /// <summary>
        /// Gets a value indicating whether EVP Proxy must be used.
        /// </summary>
        public bool ForceAgentsEvpProxy { get; }

        /// <summary>
        /// Gets the tracer settings
        /// </summary>
        public TracerSettings TracerSettings => LazyInitializer.EnsureInitialized(ref _tracerSettings, () => InitializeTracerSettings())!;

        public static CIVisibilitySettings FromDefaultSources()
        {
            return new CIVisibilitySettings(GlobalConfigurationSource.Instance);
        }

        internal void SetCodeCoverageEnabled(bool value)
        {
            CodeCoverageEnabled = value;
        }

        internal void SetTestsSkippingEnabled(bool value)
        {
            TestsSkippingEnabled = value;
        }

        internal void SetAgentlessConfiguration(bool enabled, string? apiKey, string? applicationKey, string? agentlessUrl)
        {
            Agentless = enabled;
            ApiKey = apiKey;
            ApplicationKey = applicationKey;
            AgentlessUrl = agentlessUrl;
        }

        internal void SetDefaultManualInstrumentationSettings()
        {
            // If we are using only the Public API without auto-instrumentation (TestSession/TestModule/TestSuite/Test classes only)
            // then we can disable both GitUpload and Intelligent Test Runner feature (only used by our integration).
            GitUploadEnabled = false;
            IntelligentTestRunnerEnabled = false;
        }

        private TracerSettings InitializeTracerSettings()
        {
            var tracerSettings = new TracerSettings(GlobalConfigurationSource.Instance);

            if (Logs)
            {
                // Enable the direct log submission
                tracerSettings.LogSubmissionSettings.DirectLogSubmissionEnabledIntegrations.Add("XUnit");
                tracerSettings.LogSubmissionSettings.DirectLogSubmissionBatchPeriod = TimeSpan.FromSeconds(1);
            }

            return tracerSettings;
        }
    }
}
