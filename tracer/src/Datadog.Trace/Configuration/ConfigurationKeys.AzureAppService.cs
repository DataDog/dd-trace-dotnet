// <copyright file="ConfigurationKeys.AzureAppService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Configuration
{
    internal partial class ConfigurationKeys
    {
        internal class AzureAppService
        {
            /// <summary>
            /// Configuration key which is used as a flag to tell us whether we are instrumenting an Azure App Service
            /// using the AAS Site Extension. This env var is set using the <c>applicationHost.xdt</c> file.
            /// </summary>
            internal const string AzureAppServicesContextKey = "DD_AZURE_APP_SERVICES";

            /// <summary>
            /// Configuration key which has the running version of the Azure App Services Site Extension.
            /// This env var is set in the <c>applicationHost.xdt</c> file.
            /// </summary>
            internal const string SiteExtensionVersionKey = "DD_AAS_DOTNET_EXTENSION_VERSION";

            /// <summary>
            /// Example: 8c500027-5f00-400e-8f00-60000000000f+apm-dotnet-EastUSwebspace
            /// Format: {subscriptionId}+{planResourceGroup}-{hostedInRegion}
            /// </summary>
            internal const string WebsiteOwnerNameKey = "WEBSITE_OWNER_NAME";

            /// <summary>
            /// This is the name of the resource group the site instance is assigned to.
            /// </summary>
            internal const string ResourceGroupKey = "WEBSITE_RESOURCE_GROUP";

            /// <summary>
            /// This is the unique name of the website instance within Azure App Services.
            /// Its presence is used to determine if we are running in Azure App Services.
            /// </summary>
            internal const string SiteNameKey = "WEBSITE_SITE_NAME";

            /// <summary>
            /// The instance name in Azure where the traced application is running.
            /// </summary>
            internal const string InstanceNameKey = "COMPUTERNAME";

            /// <summary>
            /// The instance ID in Azure where the traced application is running.
            /// </summary>
            internal const string InstanceIdKey = "WEBSITE_INSTANCE_ID";

            /// <summary>
            /// The operating system in Azure where the traced application is running.
            /// </summary>
            internal const string OperatingSystemKey = "WEBSITE_OS";

            /// <summary>
            /// Used to force the loader to start the trace agent (in case automatic instrumentation is disabled)
            /// </summary>
            public const string AasEnableCustomTracing = "DD_AAS_ENABLE_CUSTOM_TRACING";

            /// <summary>
            /// Used to force the loader to start dogstatsd (in case automatic instrumentation is disabled)
            /// </summary>
            public const string AasEnableCustomMetrics = "DD_AAS_ENABLE_CUSTOM_METRICS";

            /// <summary>
            /// Used to identify consumption plan functions. Consumption plans will either not have this variable,
            /// or will have a value of "dynamic".
            /// </summary>
            public const string WebsiteSKU = "WEBSITE_SKU";
        }
    }
}
