// <copyright file="ConfigurationTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Threading;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;

namespace Datadog.Trace.Telemetry
{
    internal class ConfigurationTelemetryCollector
    {
        private int _tracerInstanceCount = 0;
        private int _hasChangesFlag = 0;
        private ImmutableTracerSettings _settings;
        private SecuritySettings _securitySettings;
        private AzureAppServices _azureApServicesMetadata;
        private volatile bool _isTracerInitialized = false;
        private ApplicationTelemetryData _applicationData = null;

        public void RecordTracerSettings(
            ImmutableTracerSettings settings,
            string defaultServiceName,
            AzureAppServices appServicesMetadata)
        {
            // Increment number of tracer instances
            var tracerCount = Interlocked.Increment(ref _tracerInstanceCount);
            if (tracerCount != 1)
            {
                // We only record configuration telemetry from the first Tracer created
                SetHasChanges();
                return;
            }

            _settings = settings;
            _azureApServicesMetadata = appServicesMetadata;

            _applicationData = new ApplicationTelemetryData
            {
                ServiceName = defaultServiceName,
                Env = settings.Environment,
                ServiceVersion = settings.ServiceVersion,
                TracerVersion = TracerConstants.AssemblyVersion,
                LanguageName = "dotnet",
                LanguageVersion = FrameworkDescription.Instance.ProductVersion,
                RuntimeName = FrameworkDescription.Instance.Name,
            };

            _isTracerInitialized = true;
            SetHasChanges();
        }

        public void RecordSecuritySettings(SecuritySettings securitySettings)
        {
            _securitySettings = securitySettings;
            SetHasChanges();
        }

        public bool HasChanges()
        {
            return _isTracerInitialized && _hasChangesFlag == 1;
        }

        /// <summary>
        /// Get the application data. Will be null if not yet initialized.
        /// </summary>
        public ApplicationTelemetryData GetApplicationData()
        {
            return _applicationData;
        }

        /// <summary>
        /// Get the latest data to send to the intake.
        /// </summary>
        /// <returns>Null if there are no changes, or the collector is not yet initialized</returns>
        public ConfigTelemetryData GetConfigurationData()
        {
            var hasChanges = Interlocked.CompareExchange(ref _hasChangesFlag, 0, 1) == 1;
            if (!_isTracerInitialized || !hasChanges)
            {
                return null;
            }

            var data = new ConfigTelemetryData
            {
                OsName = FrameworkDescription.Instance.OSPlatform,
                OsVersion = Environment.OSVersion.ToString(),
                Platform = FrameworkDescription.Instance.ProcessArchitecture,
                Enabled = _settings.TraceEnabled,
                AgentUrl = _settings.AgentUri.ToString(),
                Debug = GlobalSettings.Source.DebugEnabled,
#pragma warning disable CS0618
                AnalyticsEnabled = _settings.AnalyticsEnabled,
#pragma warning restore CS0618
                SampleRate = _settings.GlobalSamplingRate,
                SamplingRules = _settings.CustomSamplingRules,
                LogInjectionEnabled = _settings.LogsInjectionEnabled,
                RuntimeMetricsEnabled = _settings.RuntimeMetricsEnabled,
                RoutetemplateResourcenamesEnabled = _settings.RouteTemplateResourceNamesEnabled,
                PartialflushEnabled = _settings.PartialFlushEnabled,
                PartialflushMinspans = _settings.PartialFlushMinSpans,
                AasConfigurationError = _azureApServicesMetadata.IsUnsafeToTrace,
                TracerInstanceCount = _tracerInstanceCount,
                SecurityEnabled = _securitySettings?.Enabled,
                SecurityBlockingEnabled = _securitySettings?.BlockingEnabled,
            };

            if (_azureApServicesMetadata.IsRelevant)
            {
                data.CloudHosting = "Azure";
                data.AasSiteExtensionVersion = _azureApServicesMetadata.SiteExtensionVersion;
                data.AasAppType = _azureApServicesMetadata.SiteType;
                data.AasFunctionsRuntimeVersion = _azureApServicesMetadata.FunctionsExtensionVersion;
            }

            // data.Configuration["agent_reachable"] = agentError == null;
            // data.Configuration["agent_error"] = agentError ?? string.Empty;

            // Global tags?
            // Agent reachable
            // Agent error
            // Is CallTarget
            // Is Docker
            // Is Fargate etc

            // additional values
            // Native metrics

            return data;
        }

        private void SetHasChanges()
        {
            Interlocked.Exchange(ref _hasChangesFlag, 1);
        }
    }
}
