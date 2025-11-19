// <copyright file="PlatformKeys.AzureFunctions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration;

internal static partial class PlatformKeys
{
    /// <summary>
    /// Reference: https://learn.microsoft.com/en-us/azure/azure-functions/functions-app-settings
    /// </summary>
    internal static class AzureFunctions
    {
        /// <summary>
        /// The version of the Azure Functions runtime, e.g. "~1" or "~4".
        /// Reference: https://docs.microsoft.com/en-us/azure/azure-functions/functions-app-settings#functions_extension_version
        /// </summary>
        internal const string FunctionsExtensionVersion = "FUNCTIONS_EXTENSION_VERSION";

        /// <summary>
        /// The Azure Functions worker runtime, e.g. "dotnet" and "dotnet-isolated".
        /// Reference: https://learn.microsoft.com/en-us/azure/azure-functions/functions-app-settings#functions_worker_runtime
        /// </summary>
        internal const string FunctionsWorkerRuntime = "FUNCTIONS_WORKER_RUNTIME";

        /// <summary>
        /// The version of Azure Functions worker runtime, e.g. "8.0" for .NET 8.
        /// </summary>
        internal const string FunctionsWorkerRuntimeVersion = "FUNCTIONS_WORKER_RUNTIME_VERSION";

        /// <summary>
        /// The path to the Azure Functions application directory, e.g. "/home/site/wwwroot".
        /// </summary>
        internal const string FunctionsApplicationDirectory = "FUNCTIONS_APPLICATION_DIRECTORY";

        /// <summary>
        /// The path to the Azure Functions worker directory, e.g. "/azure-functions-host/workers/dotnet-isolated".
        /// </summary>
        internal const string FunctionsWorkerDirectory = "FUNCTIONS_WORKER_DIRECTORY";
    }
}
