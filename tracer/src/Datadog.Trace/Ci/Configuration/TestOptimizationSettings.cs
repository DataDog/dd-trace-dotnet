// <copyright file="TestOptimizationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.LibDatadog;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Configuration
{
    internal sealed class TestOptimizationSettings
    {
        private TracerSettings? _tracerSettings;

        public TestOptimizationSettings(IConfigurationSource source, IConfigurationTelemetry telemetry)
        {
            var config = new ConfigurationBuilder(source, telemetry);
            Agentless = config.WithKeys(ConfigurationKeys.CIVisibility.AgentlessEnabled).AsBool(false);
            Site = config.WithKeys(ConfigurationKeys.Site).AsString("datadoghq.com");
            Logs = config.WithKeys(ConfigurationKeys.CIVisibility.Logs).AsBool(false);
            ApiKey = config.WithKeys(ConfigurationKeys.ApiKey).AsRedactedString();
            AgentlessUrl = config.WithKeys(ConfigurationKeys.CIVisibility.AgentlessUrl).AsString();

            // By default intake payloads has a 5MB limit
            MaximumAgentlessPayloadSize = 5 * 1024 * 1024;

            ProxyHttps = config.WithKeys(ConfigurationKeys.Proxy.ProxyHttps).AsString();
            var proxyNoProxy = config.WithKeys(ConfigurationKeys.Proxy.ProxyNoProxy).AsString() ?? string.Empty;
            ProxyNoProxy = proxyNoProxy.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Intelligent Test Runner
            IntelligentTestRunnerEnabled = config.WithKeys(ConfigurationKeys.CIVisibility.IntelligentTestRunnerEnabled).AsBool(true);

            // Tests skipping
            TestsSkippingEnabled = config.WithKeys(ConfigurationKeys.CIVisibility.TestsSkippingEnabled).AsBool();

            // Code coverage
            CodeCoverageEnabled = config.WithKeys(ConfigurationKeys.CIVisibility.CodeCoverage).AsBool();
            CodeCoverageSnkFilePath = config.WithKeys(ConfigurationKeys.CIVisibility.CodeCoverageSnkFile).AsString();
            CodeCoveragePath = config.WithKeys(ConfigurationKeys.CIVisibility.CodeCoveragePath).AsString();
            CodeCoverageEnableJitOptimizations = config.WithKeys(ConfigurationKeys.CIVisibility.CodeCoverageEnableJitOptimizations).AsBool(true);
            CodeCoverageMode = config.WithKeys(ConfigurationKeys.CIVisibility.CodeCoverageMode).AsString();

            // Git upload
            GitUploadEnabled = config.WithKeys(ConfigurationKeys.CIVisibility.GitUploadEnabled).AsBool();

            // Force evp proxy
            ForceAgentsEvpProxy = config.WithKeys(ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy).AsString();

            // Check if Datadog.Trace should be installed in the GAC
            InstallDatadogTraceInGac = config.WithKeys(ConfigurationKeys.CIVisibility.InstallDatadogTraceInGac).AsBool(true);

            // Known tests feature
            KnownTestsEnabled = config.WithKeys(ConfigurationKeys.CIVisibility.KnownTestsEnabled).AsBool();

            // Early flake detection
            EarlyFlakeDetectionEnabled = config.WithKeys(ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled).AsBool();
            if (KnownTestsEnabled == false)
            {
                EarlyFlakeDetectionEnabled = false;
            }

            // Dynamic Instrumentation
            DynamicInstrumentationEnabled = config.WithKeys(ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled).AsBool(false);

            // RUM flush milliseconds
            RumFlushWaitMillis = config.WithKeys(ConfigurationKeys.CIVisibility.RumFlushWaitMillis).AsInt32(500);

            // Test session name
            TestSessionName = config.WithKeys(ConfigurationKeys.CIVisibility.TestSessionName).AsString(
                getDefaultValue: () =>
                {
                    // We try to get the command from the active test session or test module
                    var command = TestSession.ActiveTestSessions.FirstOrDefault()?.Command ??
                                  TestModule.ActiveTestModules.FirstOrDefault()?.Tags.Command ??
                                  string.Empty;

                    if (string.IsNullOrEmpty(command))
                    {
                        // If there's no active test session or test module we try to get the command from the environment (sent by dd-trace session)
                        var environmentVariables = EnvironmentHelpers.GetEnvironmentVariables();
                        if (environmentVariables.TryGetValue<string>(TestSuiteVisibilityTags.TestSessionCommandEnvironmentVariable, out var testSessionCommand) && !string.IsNullOrEmpty(testSessionCommand))
                        {
                            command = testSessionCommand;
                        }
                        else
                        {
                            // As last resort we use the command line that started this process
                            command = Environment.CommandLine;
                        }
                    }

                    if (CiEnvironment.CIEnvironmentValues.Instance.JobName is { } jobName)
                    {
                        return $"{jobName}-{command}";
                    }

                    return command;
                },
                validator: null);

            // Flaky retry
            FlakyRetryEnabled = config.WithKeys(ConfigurationKeys.CIVisibility.FlakyRetryEnabled).AsBool();

            // Maximum number of retry attempts for a single test case.
            FlakyRetryCount = config.WithKeys(ConfigurationKeys.CIVisibility.FlakyRetryCount).AsInt32(defaultValue: 5, validator: val => val >= 1) ?? 5;

            // Maximum number of retry attempts for the entire session.
            TotalFlakyRetryCount = config.WithKeys(ConfigurationKeys.CIVisibility.TotalFlakyRetryCount).AsInt32(defaultValue: 1_000, validator: val => val >= 1) ?? 1_000;

            // Test Impact Analysis enablement
            ImpactedTestsDetectionEnabled = config.WithKeys(ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled).AsBool();

            // Test Management enablement
            TestManagementEnabled = config.WithKeys(ConfigurationKeys.CIVisibility.TestManagementEnabled).AsBool();
            TestManagementAttemptToFixRetryCount = config.WithKeys(ConfigurationKeys.CIVisibility.TestManagementAttemptToFixRetries).AsInt32();
        }

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
        /// Gets the code coverage mode
        /// </summary>
        public string? CodeCoverageMode { get; private set; }

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
        public string? ForceAgentsEvpProxy { get; }

        /// <summary>
        /// Gets a value indicating whether we ensure Datadog.Trace GAC installation.
        /// </summary>
        public bool InstallDatadogTraceInGac { get; }

        /// <summary>
        /// Gets a value indicating whether the Early flake detection feature is enabled.
        /// </summary>
        public bool? EarlyFlakeDetectionEnabled { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the Known Tests feature is enabled.
        /// </summary>
        public bool? KnownTestsEnabled { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the Dynamic Instrumentation feature is enabled.
        /// </summary>
        public bool? DynamicInstrumentationEnabled { get; private set; }

        /// <summary>
        /// Gets a value indicating the number of milliseconds to wait after flushing RUM data.
        /// </summary>
        public int RumFlushWaitMillis { get; }

        /// <summary>
        /// Gets the test session name
        /// </summary>
        public string TestSessionName { get; }

        /// <summary>
        /// Gets a value indicating whether the Flaky Retry feature is enabled.
        /// </summary>
        public bool? FlakyRetryEnabled { get; private set; }

        /// <summary>
        /// Gets a value indicating the maximum number of retry attempts for a single test case.
        /// </summary>
        public int FlakyRetryCount { get; private set; }

        /// <summary>
        /// Gets a value indicating the maximum number of retry attempts for the entire session.
        /// </summary>
        public int TotalFlakyRetryCount { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the Impacted Tests Detection feature is enabled.
        /// </summary>
        public bool? ImpactedTestsDetectionEnabled { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the Test Management feature is enabled.
        /// </summary>
        public bool? TestManagementEnabled { get; private set; }

        /// <summary>
        /// Gets a value indicating the number of retries to fix a test management issue.
        /// </summary>
        public int? TestManagementAttemptToFixRetryCount { get; private set; }

        /// <summary>
        /// Gets the tracer settings
        /// </summary>
        public TracerSettings TracerSettings => LazyInitializer.EnsureInitialized(ref _tracerSettings, () => InitializeTracerSettings())!;

        public static TestOptimizationSettings FromDefaultSources()
        {
            return new TestOptimizationSettings(GlobalConfigurationSource.Instance, TelemetryFactory.Config);
        }

        internal void SetCodeCoverageEnabled(bool value)
        {
            CodeCoverageEnabled = value;
        }

        internal void SetTestsSkippingEnabled(bool value)
        {
            TestsSkippingEnabled = value;
        }

        internal void SetKnownTestsEnabled(bool value)
        {
            KnownTestsEnabled = value;
        }

        internal void SetDynamicInstrumentationEnabled(bool value)
        {
            DynamicInstrumentationEnabled = value;
        }

        internal void SetEarlyFlakeDetectionEnabled(bool value)
        {
            EarlyFlakeDetectionEnabled = value;
        }

        internal void SetFlakyRetryEnabled(bool value)
        {
            FlakyRetryEnabled = value;
        }

        internal void SetImpactedTestsEnabled(bool value)
        {
            ImpactedTestsDetectionEnabled = value;
        }

        internal void SetTestManagementEnabled(bool value)
        {
            TestManagementEnabled = value;
        }

        internal void SetAgentlessConfiguration(bool enabled, string? apiKey, string? agentlessUrl)
        {
            Agentless = enabled;
            ApiKey = apiKey;
            AgentlessUrl = agentlessUrl;
        }

        internal void SetCodeCoverageMode(string? coverageMode)
        {
            CodeCoverageMode = coverageMode;
        }

        internal void SetDefaultManualInstrumentationSettings()
        {
            // If we are using only the Public API without auto-instrumentation (TestSession/TestModule/TestSuite/Test classes only)
            // then we can disable both GitUpload and Intelligent Test Runner feature (only used by our integration).
            GitUploadEnabled = false;
            IntelligentTestRunnerEnabled = false;
        }

        private TracerSettings InitializeTracerSettings()
            => InitializeTracerSettings(GlobalConfigurationSource.Instance);

        [TestingAndPrivateOnly]
        internal TracerSettings InitializeTracerSettings(IConfigurationSource source)
        {
            // This is a somewhat hacky way to "tell" TracerSettings that we're running in CI Visibility
            // There's no doubt various other ways we could flag it based on values we're _already_ extracting,
            // but we don't want to set up too much interdependence there.
            var telemetry = new ConfigurationTelemetry();
            var additionalSource = new Dictionary<string, object?> { { ConfigurationKeys.CIVisibility.IsRunningInCiVisMode, true } };

            if (Logs)
            {
                // fetch the "original" values, and update them with the CI Visibility settings
                var enabledDirectLogSubmissionIntegrations = new ConfigurationBuilder(source, telemetry)
                                                            .WithKeys(ConfigurationKeys.DirectLogSubmission.EnabledIntegrations)
                                                            .AsString();
                var logIntegrations = enabledDirectLogSubmissionIntegrations + ";XUnit";
                additionalSource[ConfigurationKeys.DirectLogSubmission.EnabledIntegrations] = logIntegrations;
                additionalSource[ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds] = 1;
            }

            var newSource = new CompositeConfigurationSource([new DictionaryObjectConfigurationSource(additionalSource), source]);
            return new TracerSettings(newSource, telemetry, new OverrideErrorLog(), new LibDatadogAvailableResult(false));
        }
    }
}
