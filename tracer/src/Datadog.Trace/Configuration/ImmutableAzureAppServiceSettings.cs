// <copyright file="ImmutableAzureAppServiceSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Util;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Settings class for gathering metadata about the execution context in Azure App Services.
    /// References:
    /// https://docs.microsoft.com/en-us/azure/app-service/environment/intro
    /// https://docs.microsoft.com/en-us/azure/azure-functions/functions-app-settings
    /// https://github.com/projectkudu/kudu/wiki/Azure-runtime-environment
    /// </summary>
    internal sealed class ImmutableAzureAppServiceSettings
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
                Log.ErrorSkipTelemetry("The Azure Site Extension will not work if you have not configured DD_API_KEY.");
                IsUnsafeToTrace = true;
            }

            SubscriptionId = GetSubscriptionId(source, telemetry);
            ResourceGroup = config.WithKeys(PlatformKeys.AzureAppService.ResourceGroupKey).AsString();
            SiteName = config.WithKeys(PlatformKeys.AzureAppService.SiteNameKey).AsString();
            ResourceId = CompileResourceId(subscriptionId: SubscriptionId, siteName: SiteName, resourceGroup: ResourceGroup);
            InstanceId = config.WithKeys(PlatformKeys.AzureAppService.InstanceIdKey).AsString("unknown");
            InstanceName = config.WithKeys(PlatformKeys.ComputerNameKey).AsString("unknown");
            OperatingSystem = config.WithKeys(PlatformKeys.AzureAppService.OperatingSystemKey).AsString("unknown");
            SiteExtensionVersion = config.WithKeys(ConfigurationKeys.AzureAppService.SiteExtensionVersionKey).AsString("unknown");
            WebsiteSku = config.WithKeys(PlatformKeys.AzureAppService.WebsiteSku).AsString();

            FunctionsWorkerRuntime = config.WithKeys(PlatformKeys.AzureFunctions.FunctionsWorkerRuntime).AsString();
            FunctionsExtensionVersion = config.WithKeys(PlatformKeys.AzureFunctions.FunctionsExtensionVersion).AsString();

            if (FunctionsWorkerRuntime is not null && FunctionsExtensionVersion is not null)
            {
                IsFunctionsApp = true;
                SiteKind = "functionapp";
                SiteType = "function";

                IsIsolatedFunctionsApp = FunctionsWorkerRuntime?.EndsWith("-isolated", StringComparison.OrdinalIgnoreCase) == true;
                PlatformStrategy.ShouldSkipClientSpan = ShouldSkipClientSpanWithinFunctions;
            }
            else
            {
                IsFunctionsApp = false;
                SiteKind = "app";
                SiteType = "app";
            }

            DebugModeEnabled = config.WithKeys(ConfigurationKeys.DebugEnabled).AsBool(false);
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

        public bool IsFunctionsApp { get; }

        public string? WebsiteSku { get; }

        public string? FunctionsExtensionVersion { get; }

        public string? FunctionsWorkerRuntime { get; }

        public bool IsIsolatedFunctionsApp { get; }

        public string InstanceName { get; }

        public string InstanceId { get; }

        public string OperatingSystem { get; }

        private static bool ShouldSkipClientSpanWithinFunctions(Scope? scope)
        {
            // Ignore isolated client spans within azure functions
            return scope == null;
        }

        private static string? CompileResourceId(string? subscriptionId, string? siteName, string? resourceGroup)
        {
            if (subscriptionId == null)
            {
                Log.Warning("Could not successfully retrieve the subscription ID from variable: {Variable}", PlatformKeys.AzureAppService.WebsiteOwnerNameKey);
                return null;
            }

            if (siteName == null)
            {
                Log.Warning("Could not successfully retrieve the deployment ID from variable: {Variable}", PlatformKeys.AzureAppService.SiteNameKey);
                return null;
            }

            if (resourceGroup == null)
            {
                Log.Warning("Could not successfully retrieve the resource group name from variable: {Variable}", PlatformKeys.AzureAppService.ResourceGroupKey);
                return null;
            }

            return $"/subscriptions/{subscriptionId}/resourcegroups/{resourceGroup}/providers/microsoft.web/sites/{siteName}".ToLowerInvariant();
        }

        private static string? GetSubscriptionId(IConfigurationSource source, IConfigurationTelemetry telemetry)
        {
            var websiteOwner = new ConfigurationBuilder(source, telemetry)
                              .WithKeys(PlatformKeys.AzureAppService.WebsiteOwnerNameKey)
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

        /// <summary>
        /// Returns <c>true</c> if the app is running in Azure App Services.
        /// Checks for the presence of "WEBSITE_SITE_NAME" in the configuration.
        /// </summary>
        public static bool IsRunningInAzureAppServices(IConfigurationSource source, IConfigurationTelemetry telemetry)
        {
            var siteName = new ConfigurationBuilder(source, telemetry)
                           .WithKeys(PlatformKeys.AzureAppService.SiteNameKey)
                           .AsString();

            return !string.IsNullOrEmpty(siteName);
        }

        /// <summary>
        /// Returns <c>true</c> if the app is running in Azure Functions.
        /// Checks for the presence of "WEBSITE_SITE_NAME", "FUNCTIONS_WORKER_RUNTIME",
        /// and "FUNCTIONS_EXTENSION_VERSION" in the configuration.
        /// </summary>
        public static bool IsRunningInAzureFunctions(IConfigurationSource source, IConfigurationTelemetry telemetry)
        {
            var siteName = new ConfigurationBuilder(source, telemetry)
                           .WithKeys(PlatformKeys.AzureAppService.SiteNameKey)
                           .AsString();

            // "dotnet", "dotnet-isolated"
            var workerRuntime = new ConfigurationBuilder(source, telemetry)
                           .WithKeys(PlatformKeys.AzureFunctions.FunctionsWorkerRuntime)
                           .AsString();

            // "~4", "~1"
            var extensionVersion = new ConfigurationBuilder(source, telemetry)
                           .WithKeys(PlatformKeys.AzureFunctions.FunctionsExtensionVersion)
                           .AsString();

            return !string.IsNullOrEmpty(siteName) &&
                   !string.IsNullOrEmpty(workerRuntime) &&
                   !string.IsNullOrEmpty(extensionVersion);
        }

        /// <summary>
        /// Returns <c>true</c> if the app is instrumented using the Azure App Services Site Extension.
        /// Checks for the presence of "DD_AZURE_APP_SERVICES=1".
        /// </summary>
        public static bool IsUsingAzureAppServicesSiteExtension(IConfigurationSource source, IConfigurationTelemetry telemetry)
        {
            return new ConfigurationBuilder(source, telemetry)
                   .WithKeys(ConfigurationKeys.AzureAppService.AzureAppServicesContextKey)
                   .AsString() == "1";
        }
    }
}
