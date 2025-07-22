// <copyright file="ImmutableAzureAppServiceSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;

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
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ImmutableAzureAppServiceSettings>();

        /// <summary>
        /// The http client paths we don't want to trace when running in AAS or Azure Functions.
        /// </summary>
        /// <seealso cref="Datadog.Trace.Configuration.TracerSettings.HttpClientExcludedUrlSubstrings"/>
        public const string DefaultHttpClientExclusions =
#pragma warning disable SA1025 // CodeMustNotContainMultipleWhitespaceInARow
            ".LOGS.DATADOGHQ.," +
            ".SERVICES.VISUALSTUDIO.," +
            ".APPLICATIONINSIGHTS.AZURE.," +
            ".MONITOR.AZURE.," +
            ".BLOB.CORE.WINDOWS.NET/AZURE-WEBJOBS," +                   // no trailing slash, /azure-webjobs-hosts/*, /azure-webjobs-secrets/*, etc
            ".TABLE.CORE.WINDOWS.NET/TABLES," +                         // no trailing slash, /Tables, /Tables('AzureFunctionsDiagnosticEventsCheck')
            ".TABLE.CORE.WINDOWS.NET/AZUREFUNCTIONSDIAGNOSTICEVENTS," + // no trailing slash, /AzureFunctionsDiagnosticEvents202507()?$format=...
            ".AZUREWEBSITES.NET/ADMIN/," +                              // trailing slash, /admin/*
            "CDN.FUNCTIONS.AZURE.COM/PUBLIC/EXTENSIONBUNDLES/";         // trailing slash, /public/ExtensionBundles*
#pragma warning restore SA1025                                          // CodeMustNotContainMultipleWhitespaceInARow

        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableAzureAppServiceSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        /// <param name="telemetry"><see cref="IConfigurationTelemetry"/> instance for recording telemetry</param>
        public ImmutableAzureAppServiceSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry)
        {
            source ??= NullConfigurationSource.Instance;
            // TODO: This is retrieved from other places too... need to work out how to not replace config
            var config = new ConfigurationBuilder(source, telemetry);
            var apiKey = config.WithKeys(ConfigurationKeys.ApiKey).AsRedactedString();
            if (string.IsNullOrEmpty(apiKey))
            {
                Log.Error("The Azure Site Extension will not work if you have not configured DD_API_KEY.");
                IsUnsafeToTrace = true;
            }

            // Azure App Services Basis
            SubscriptionId = GetSubscriptionId(source, telemetry);
            ResourceGroup = config.WithKeys(ConfigurationKeys.AzureAppService.ResourceGroupKey).AsString();
            SiteName = config.WithKeys(ConfigurationKeys.AzureAppService.SiteNameKey).AsString();
            ResourceId = CompileResourceId();

            InstanceId = config.WithKeys(ConfigurationKeys.AzureAppService.InstanceIdKey).AsString("unknown");
            InstanceName = config.WithKeys(ConfigurationKeys.AzureAppService.InstanceNameKey).AsString("unknown");
            OperatingSystem = config.WithKeys(ConfigurationKeys.AzureAppService.OperatingSystemKey).AsString("unknown");
            SiteExtensionVersion = config.WithKeys(ConfigurationKeys.AzureAppService.SiteExtensionVersionKey).AsString("unknown");

            FunctionsWorkerRuntime = config.WithKeys(ConfigurationKeys.AzureAppService.FunctionsWorkerRuntimeKey).AsString();
            FunctionsExtensionVersion = config.WithKeys(ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey).AsString();

            WebsiteSKU = config.WithKeys(ConfigurationKeys.AzureAppService.WebsiteSKU).AsString();

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
                    IsRunningMiniAgentInAzureFunctions = GetIsFunctionsAppUsingMiniAgent(source, telemetry);
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

            DebugModeEnabled = config.WithKeys(Configuration.ConfigurationKeys.DebugEnabled).AsBool(false);
            CustomTracingEnabled = config.WithKeys(ConfigurationKeys.AzureAppService.AasEnableCustomTracing).AsBool(false);
            NeedsDogStatsD = config.WithKeys(ConfigurationKeys.AzureAppService.AasEnableCustomMetrics).AsBool(false);
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

        public AzureContext AzureContext { get; }

        public bool IsFunctionsApp { get; }

        public bool IsRunningMiniAgentInAzureFunctions { get; }

        public string? WebsiteSKU { get; }

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

        private string? GetSubscriptionId(IConfigurationSource source, IConfigurationTelemetry telemetry)
        {
            var websiteOwner = new ConfigurationBuilder(source, telemetry)
                              .WithKeys(ConfigurationKeys.AzureAppService.WebsiteOwnerNameKey)
                              .AsString(websiteOwner => !string.IsNullOrWhiteSpace(websiteOwner));

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

        public static bool GetIsFunctionsAppUsingMiniAgent(IConfigurationSource source, IConfigurationTelemetry telemetry)
        {
            var config = new ConfigurationBuilder(source, telemetry);

            var functionsExtensionVersion = config.WithKeys(ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey).AsString();
            var functionsWorkerRuntime = config.WithKeys(ConfigurationKeys.AzureAppService.FunctionsWorkerRuntimeKey).AsString();
            var isFunctionApp = functionsExtensionVersion is not null || functionsWorkerRuntime is not null;

            var websiteSKU = config.WithKeys(ConfigurationKeys.AzureAppService.WebsiteSKU).AsString();

            // Start mini agent on all Linux function apps and only Windows function apps on consumption plans
            // Windows function apps on non-consumption plans do not use the mini agent and instead use the .NET APM Extension which packages the Datadog agent
            return isFunctionApp && (Environment.OSVersion.Platform == PlatformID.Unix || websiteSKU is "Dynamic" or null);
        }

        /// <summary>
        /// Returns <c>true</c> if the app is running in Azure App Services.
        /// Checks for the presence of "WEBSITE_SITE_NAME" in the configuration.
        /// </summary>
        public static bool IsRunningInAzureAppServices(IConfigurationSource source, IConfigurationTelemetry telemetry)
        {
            var siteName = new ConfigurationBuilder(source, telemetry)
                           .WithKeys(ConfigurationKeys.AzureAppService.SiteNameKey)
                           .AsString();

            return !string.IsNullOrEmpty(siteName);
        }

        /// <summary>
        /// Returns <c>true</c> if the app is running in Azure Functions.
        /// Checks for the presence of "FUNCTIONS_WORKER_RUNTIME" and "FUNCTIONS_EXTENSION_VERSION" in the configuration.
        /// </summary>
        public static bool IsRunningInAzureFunctions(IConfigurationSource source, IConfigurationTelemetry telemetry)
        {
            var workerRuntime = new ConfigurationBuilder(source, telemetry)
                           .WithKeys(ConfigurationKeys.AzureFunctions.FunctionsWorkerRuntime)
                           .AsString();

            var extensionVersion = new ConfigurationBuilder(source, telemetry)
                           .WithKeys(ConfigurationKeys.AzureFunctions.FunctionsExtensionVersion)
                           .AsString();

            return !string.IsNullOrEmpty(workerRuntime) && !string.IsNullOrEmpty(extensionVersion);
        }

        /// <summary>
        /// Returns <c>true</c> if the app is instrumented using the Azure App Services Site Extension.
        /// Checks for the presence of "DD_AZURE_APP_SERVICES=1" and "DD_AAS_DOTNET_EXTENSION_VERSION" in the configuration.
        /// </summary>
        public static bool IsUsingAzureAppServicesSiteExtension(IConfigurationSource source, IConfigurationTelemetry telemetry)
        {
            var flag = new ConfigurationBuilder(source, telemetry)
                       .WithKeys(ConfigurationKeys.AzureAppService.AzureAppServicesContextKey)
                       .AsBool(false);

            var siteExtensionVersion = new ConfigurationBuilder(source, telemetry)
                                       .WithKeys(ConfigurationKeys.AzureAppService.SiteExtensionVersionKey)
                                       .AsString();

            return flag && !string.IsNullOrEmpty(siteExtensionVersion);
        }
    }
}
