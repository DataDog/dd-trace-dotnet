// <copyright file="PlatformKeys.AzureAppService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
namespace Datadog.Trace.Configuration;

internal static partial class PlatformKeys
{
    internal static class AzureAppService
    {
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
        /// This is the key for AzureAppServicePerformanceCounters
        /// </summary>
        internal const string CountersKey = "WEBSITE_COUNTERS_CLR";

        /// <summary>
        /// The instance ID in Azure where the traced application is running.
        /// </summary>
        internal const string InstanceIdKey = "WEBSITE_INSTANCE_ID";

        /// <summary>
        /// The operating system in Azure where the traced application is running.
        /// </summary>
        internal const string OperatingSystemKey = "WEBSITE_OS";

        /// <summary>
        /// Used to identify consumption plan functions. Consumption plans will either not have this variable,
        /// or will have a value of "dynamic".
        /// </summary>
        public const string WebsiteSku = "WEBSITE_SKU";

        /// <summary>
        /// The name of the Azure Container App instance. Related to Container Apps
        /// </summary>
        internal const string ContainerAppName = "CONTAINER_APP_NAME";

        /// <summary>
        /// Used to detect if running in Azure App Service (presence of this variable).
        /// </summary>
        internal const string RunFromZipKey = "APPSVC_RUN_ZIP";

        /// <summary>
        /// Used to detect if running in Azure App Service (presence of this variable).
        /// </summary>
        internal const string AppServiceApplogsTraceEnabledKey = "WEBSITE_APPSERVICEAPPLOGS_TRACE_ENABLED";
    }
}
