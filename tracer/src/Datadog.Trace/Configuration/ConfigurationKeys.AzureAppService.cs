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
            /// Used to force the loader to start the trace agent (in case automatic instrumentation is disabled)
            /// </summary>
            public const string AasEnableCustomTracing = "DD_AAS_ENABLE_CUSTOM_TRACING";

            /// <summary>
            /// Used to force the loader to start dogstatsd (in case automatic instrumentation is disabled)
            /// </summary>
            public const string AasEnableCustomMetrics = "DD_AAS_ENABLE_CUSTOM_METRICS";
        }
    }
}
