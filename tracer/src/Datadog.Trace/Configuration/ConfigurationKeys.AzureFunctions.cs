// <copyright file="ConfigurationKeys.AzureFunctions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Configuration;

internal partial class ConfigurationKeys
{
    /// <summary>
    /// Reference: https://learn.microsoft.com/en-us/azure/azure-functions/functions-app-settings
    /// </summary>
    internal class AzureFunctions
    {
        /// <summary>
        /// The version of the functions runtime to use in this function app.
        /// Reference: https://docs.microsoft.com/en-us/azure/azure-functions/functions-app-settings#functions_extension_version
        /// </summary>
        internal const string FunctionsExtensionVersionKey = "FUNCTIONS_EXTENSION_VERSION";

        /// <summary>
        /// This variable is only present in Azure Functions.
        /// In this context, valid values are "dotnet" and "dotnet-isolated".
        /// Reference: https://learn.microsoft.com/en-us/azure/azure-functions/functions-app-settings#functions_worker_runtime
        /// </summary>
        internal const string FunctionsWorkerRuntimeKey = "FUNCTIONS_WORKER_RUNTIME";

        // "FUNCTIONS_WORKER_RUNTIME_VERSION=8.0",
        internal const string FunctionsWorkerRuntimeVersionKey = "FUNCTIONS_WORKER_RUNTIME_VERSION";

        // "FUNCTIONS_APPLICATION_DIRECTORY=/home/site/wwwroot",
        internal const string FunctionsApplicationDirectoryKey = "FUNCTIONS_APPLICATION_DIRECTORY";

        // "FUNCTIONS_WORKER_DIRECTORY=/azure-functions-host/workers/dotnet-isolated",
        internal const string FunctionsWorkerDirectoryKey = "FUNCTIONS_WORKER_DIRECTORY";
    }
}
