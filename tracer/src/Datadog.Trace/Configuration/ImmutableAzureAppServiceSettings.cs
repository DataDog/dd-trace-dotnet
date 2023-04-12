// <copyright file="ImmutableAzureAppServiceSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using MetricsTransportType = Datadog.Trace.Vendors.StatsdClient.Transport.TransportType;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Settings class for gathering metadata about the execution context in Azure App Services.
    /// References:
    /// https://docs.microsoft.com/en-us/azure/app-service/environment/intro
    /// https://docs.microsoft.com/en-us/azure/azure-functions/functions-app-settings
    /// https://github.com/projectkudu/kudu/wiki/Azure-runtime-environment
    /// </summary>
    internal class ImmutableAzureAppServiceSettings
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ImmutableAzureAppServiceSettings));

        public static readonly string DefaultHttpClientExclusions = "logs.datadoghq, services.visualstudio, applicationinsights.azure, blob.core.windows.net/azure-webjobs, azurewebsites.net/admin, /azure-webjobs-hosts/".ToUpperInvariant();

        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableAzureAppServiceSettings"/> class with default values.
        /// </summary>
        public ImmutableAzureAppServiceSettings()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableAzureAppServiceSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        public ImmutableAzureAppServiceSettings(IConfigurationSource? source)
        {
            source ??= NullConfigurationSource.Instance;
            var apiKey = source.GetString(Configuration.ConfigurationKeys.ApiKey);
            if (string.IsNullOrEmpty(apiKey))
            {
                Log.Error("The Azure Site Extension will not work if you have not configured DD_API_KEY.");
                IsUnsafeToTrace = true;
            }

            // Azure App Services Basis
            SubscriptionId = GetSubscriptionId(source);
            ResourceGroup = source.GetString(ConfigurationKeys.AzureAppService.ResourceGroupKey);
            SiteName = source.GetString(ConfigurationKeys.AzureAppService.SiteNameKey);
            ResourceId = CompileResourceId();

            InstanceId = source.GetString(ConfigurationKeys.AzureAppService.InstanceIdKey) ?? "unknown";
            InstanceName = source.GetString(ConfigurationKeys.AzureAppService.InstanceNameKey) ?? "unknown";
            OperatingSystem = source.GetString(ConfigurationKeys.AzureAppService.OperatingSystemKey) ?? "unknown";
            SiteExtensionVersion = source.GetString(ConfigurationKeys.AzureAppService.SiteExtensionVersionKey) ?? "unknown";

            FunctionsWorkerRuntime = source.GetString(ConfigurationKeys.AzureAppService.FunctionsWorkerRuntimeKey);
            FunctionsExtensionVersion = source.GetString(ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey);

            if (FunctionsWorkerRuntime is not null || FunctionsExtensionVersion is not null)
            {
                AzureContext = AzureContext.AzureFunctions;
            }

            switch (AzureContext)
            {
                case AzureContext.AzureFunctions:
                    SiteKind = "functionapp";
                    SiteType = "function";
                    IsFunctionsApp = true;
                    IsIsolatedFunctionsApp = FunctionsWorkerRuntime?.EndsWith("-isolated", StringComparison.OrdinalIgnoreCase) == true;
                    PlatformStrategy.ShouldSkipClientSpan = ShouldSkipClientSpanWithinFunctions;
                    break;
                case AzureContext.AzureAppService:
                    SiteKind = "app";
                    SiteType = "app";
                    IsFunctionsApp = false;
                    break;
                default:
                    SiteKind = "unknown";
                    SiteType = "unknown";
                    break;
            }

            Runtime = FrameworkDescription.Instance.Name;

            DebugModeEnabled = source.GetString(Configuration.ConfigurationKeys.DebugEnabled)?.ToBoolean() ?? false;
            CustomTracingEnabled = source.GetString(ConfigurationKeys.AzureAppService.AasEnableCustomTracing)?.ToBoolean() ?? false;
            NeedsDogStatsD = source.GetString(ConfigurationKeys.AzureAppService.AasEnableCustomMetrics)?.ToBoolean() ?? false;
        }

        public bool DebugModeEnabled { get; }

        public bool CustomTracingEnabled { get; }

        public bool NeedsDogStatsD { get; }

        public bool IsUnsafeToTrace { get; }

        public string SiteExtensionVersion { get; }

        public string SiteType { get; }

        public string SiteKind { get; }

        public string? SubscriptionId { get; }

        public string? ResourceGroup { get; }

        public string? SiteName { get; }

        public string? ResourceId { get; }

        public AzureContext AzureContext { get; private set; } = AzureContext.AzureAppService;

        public bool IsFunctionsApp { get; private set; }

        public string? FunctionsExtensionVersion { get; }

        public string? FunctionsWorkerRuntime { get; }

        public bool IsIsolatedFunctionsApp { get; }

        public string InstanceName { get; }

        public string InstanceId { get; }

        public string OperatingSystem { get; }

        public string Runtime { get; }

        private static bool ShouldSkipClientSpanWithinFunctions(Scope? scope)
        {
            // Ignore isolated client spans within azure functions
            return scope == null;
        }

        private string? CompileResourceId()
        {
            string? resourceId = null;

            var success = true;
            if (SubscriptionId == null)
            {
                success = false;
                Log.Warning("Could not successfully retrieve the subscription ID from variable: {Variable}", ConfigurationKeys.AzureAppService.WebsiteOwnerNameKey);
            }

            if (SiteName == null)
            {
                success = false;
                Log.Warning("Could not successfully retrieve the deployment ID from variable: {Variable}", ConfigurationKeys.AzureAppService.SiteNameKey);
            }

            if (ResourceGroup == null)
            {
                success = false;
                Log.Warning("Could not successfully retrieve the resource group name from variable: {Variable}", ConfigurationKeys.AzureAppService.ResourceGroupKey);
            }

            if (success)
            {
                resourceId = $"/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroup}/providers/microsoft.web/sites/{SiteName}".ToLowerInvariant();
            }

            return resourceId;
        }

        private string? GetSubscriptionId(IConfigurationSource source)
        {
            var websiteOwner = source.GetString(ConfigurationKeys.AzureAppService.WebsiteOwnerNameKey);
            if (!string.IsNullOrWhiteSpace(websiteOwner))
            {
                var plusSplit = websiteOwner!.Split('+');
                if (plusSplit.Length > 0 && !string.IsNullOrWhiteSpace(plusSplit[0]))
                {
                    return plusSplit[0];
                }
            }

            return null;
        }
    }
}
