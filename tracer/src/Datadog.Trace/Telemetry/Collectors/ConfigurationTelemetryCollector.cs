// <copyright file="ConfigurationTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
        private volatile CurrentSettings _settings;
        private volatile SecuritySettings _securitySettings;
        private volatile bool _isTracerInitialized = false;
        private AzureAppServices _azureApServicesMetadata;
        private HostTelemetryData _hostData = null;

        public void RecordTracerSettings(
            ImmutableTracerSettings tracerSettings,
            string defaultServiceName,
            AzureAppServices appServicesMetadata)
        {
            // Increment number of times this has been called
            var reconfigureCount = Interlocked.Increment(ref _tracerInstanceCount);
            var appData = new ApplicationTelemetryData(
                serviceName: defaultServiceName,
                env: tracerSettings.Environment,
                tracerVersion: TracerConstants.AssemblyVersion,
                languageName: "dotnet",
                languageVersion: FrameworkDescription.Instance.ProductVersion)
            {
                ServiceVersion = tracerSettings.ServiceVersion,
                RuntimeName = FrameworkDescription.Instance.Name,
            };

            _settings = new CurrentSettings(tracerSettings, appData);

            // The remaining properties can't change, so only need to set them the first time
            if (reconfigureCount != 1)
            {
                SetHasChanges();
                return;
            }

            _azureApServicesMetadata = appServicesMetadata;
            var host = HostMetadata.Instance;
            _hostData = new HostTelemetryData
            {
                ContainerId = ContainerMetadata.GetContainerId(),
                Os = FrameworkDescription.Instance.OSPlatform,
                OsVersion = Environment.OSVersion.ToString(),
                Hostname = host.Hostname,
                KernelName = host.KernelName,
                KernelRelease = host.KernelRelease,
                KernelVersion = host.KernelVersion,
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
            var settings = _settings;
            return settings?.ApplicationData;
        }

        /// <summary>
        /// Get the application data. Will be null if not yet initialized.
        /// </summary>
        public HostTelemetryData GetHostData()
        {
            return _hostData;
        }

        /// <summary>
        /// Get the latest data to send to the intake.
        /// </summary>
        /// <returns>Null if there are no changes, or the collector is not yet initialized</returns>
        public ICollection<TelemetryValue> GetConfigurationData()
        {
            var hasChanges = Interlocked.CompareExchange(ref _hasChangesFlag, 0, 1) == 1;
            if (!_isTracerInitialized || !hasChanges)
            {
                return null;
            }

            var settings = _settings.Settings;

            var data = new List<TelemetryValue>(_azureApServicesMetadata.IsRelevant ? 20 : 16)
            {
                new(name: "platform", value: FrameworkDescription.Instance.ProcessArchitecture),
                new(name: "enabled", value: settings.TraceEnabled),
                new(name: "agent_url", value: settings.Exporter.AgentUri.ToString()),
                new(name: "debug", value: GlobalSettings.Source.DebugEnabled),
#pragma warning disable CS0618
                new(name: "analytics_enabled", value: settings.AnalyticsEnabled),
#pragma warning restore CS0618
                new(name: "sample_rate", value: settings.GlobalSamplingRate),
                new(name: "sampling_rules", value: settings.CustomSamplingRules),
                new(name: "logInjection_enabled", value: settings.LogsInjectionEnabled),
                new(name: "runtimemetrics_enabled", value: settings.RuntimeMetricsEnabled),
                new(name: "routetemplate_resourcenames_enabled", value: settings.RouteTemplateResourceNamesEnabled),
                new(name: "partialflush_enabled", value: settings.Exporter.PartialFlushEnabled),
                new(name: "partialflush_minspans", value: settings.Exporter.PartialFlushMinSpans),
                new(name: "aas_configuration_error", value: _azureApServicesMetadata.IsUnsafeToTrace),
                new(name: "tracer_instance_count", value: _tracerInstanceCount),
                new(name: "security_enabled", value: _securitySettings?.Enabled),
                new(name: "security_blocking_enabled", value: _securitySettings?.BlockingEnabled),
            };

            if (_azureApServicesMetadata.IsRelevant)
            {
                data.Add(new("cloud_hosting", "Azure"));
                data.Add(new("aas_siteextensions_version", _azureApServicesMetadata.SiteExtensionVersion));
                data.Add(new("aas_app_type", _azureApServicesMetadata.SiteType));
                data.Add(new("aas_functions_runtime_version", _azureApServicesMetadata.FunctionsExtensionVersion));
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

        private class CurrentSettings
        {
            public CurrentSettings(ImmutableTracerSettings settings, ApplicationTelemetryData applicationData)
            {
                Settings = settings;
                ApplicationData = applicationData;
            }

            public ImmutableTracerSettings Settings { get; }

            public ApplicationTelemetryData ApplicationData { get; }
        }
    }
}
