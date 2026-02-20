// <copyright file="AzurePlatformDetection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.Serverless;

/// <summary>
/// Cached helpers for detecting Azure serverless platforms.
/// Delegates to <see cref="Datadog.Trace.Util.EnvironmentHelpers"/> for env-var reads.
/// </summary>
internal static class AzurePlatformDetection
{
    private static bool? _isAzureAppServices;
    private static bool? _isAzureFunctions;
    private static bool? _isUsingAzureAppServicesSiteExtension;
    private static bool? _isAzureFunctionsIsolated;
    private static bool? _isRunningInAzureFunctionsHost;

    private static string? _workerRuntime;
    private static bool _workerRuntimeCached;

    private static string? _extensionVersion;
    private static bool _extensionVersionCached;

    /// <summary>
    /// Gets a value indicating whether the current environment is Azure App Services
    /// by checking for the presence of "WEBSITE_SITE_NAME".
    /// Note that this is a superset of <see cref="IsAzureFunctions"/>.
    /// The result is cached after the first evaluation.
    /// </summary>
    internal static bool IsAzureAppServices =>
        _isAzureAppServices ??= EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.AzureAppService.SiteNameKey);

    /// <summary>
    /// Gets a value indicating whether the current environment is Azure Functions
    /// by checking for the presence of "WEBSITE_SITE_NAME", "FUNCTIONS_WORKER_RUNTIME", and "FUNCTIONS_EXTENSION_VERSION".
    /// Note that this is a subset of <see cref="IsAzureAppServices"/>.
    /// The result is cached after the first evaluation.
    /// </summary>
    internal static bool IsAzureFunctions =>
        _isAzureFunctions ??=
            IsAzureAppServices &&
            EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.AzureFunctions.FunctionsWorkerRuntime) &&
            EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.AzureFunctions.FunctionsExtensionVersion);

    /// <summary>
    /// Gets a value indicating whether the current environment is using Azure App Services Site Extension
    /// by checking for the presence of "DD_AZURE_APP_SERVICES=1" and "DD_AAS_DOTNET_EXTENSION_VERSION".
    /// The result is cached after the first evaluation.
    /// </summary>
    internal static bool IsUsingAzureAppServicesSiteExtension =>
        _isUsingAzureAppServicesSiteExtension ??=
            EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.AzureAppService.AzureAppServicesContextKey) == "1" &&
            EnvironmentHelpers.EnvironmentVariableExists(ConfigurationKeys.AzureAppService.SiteExtensionVersionKey);

    /// <summary>
    /// Gets a value indicating whether the current environment is an Azure Functions isolated worker process
    /// (as opposed to in-process functions) by checking that:
    ///
    /// - <see cref="IsAzureFunctions"/> is <c>true</c>
    /// - "FUNCTIONS_WORKER_RUNTIME" is set to "dotnet-isolated"
    ///
    /// This will return true for both the host process and worker process in isolated functions.
    /// Use <see cref="IsRunningInAzureFunctionsHost"/> to distinguish between host and worker.
    /// The result is cached after the first evaluation.
    /// </summary>
    internal static bool IsAzureFunctionsIsolated =>
        _isAzureFunctionsIsolated ??=
            IsAzureFunctions && string.Equals(WorkerRuntime, "dotnet-isolated", StringComparison.Ordinal);

    /// <summary>
    /// Gets the cached value of the "FUNCTIONS_WORKER_RUNTIME" environment variable.
    /// </summary>
    internal static string? WorkerRuntime
    {
        get
        {
            if (!_workerRuntimeCached)
            {
                _workerRuntime = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.AzureFunctions.FunctionsWorkerRuntime, defaultValue: string.Empty);
                _workerRuntimeCached = true;
            }

            return _workerRuntime;
        }
    }

    /// <summary>
    /// Gets the cached value of the "FUNCTIONS_EXTENSION_VERSION" environment variable.
    /// </summary>
    internal static string? ExtensionVersion
    {
        get
        {
            if (!_extensionVersionCached)
            {
                _extensionVersion = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.AzureFunctions.FunctionsExtensionVersion, defaultValue: string.Empty);
                _extensionVersionCached = true;
            }

            return _extensionVersion;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the current environment is the Azure Functions host process
    /// by checking that:
    /// - <see cref="IsAzureFunctionsIsolated"/> is <c>true</c>
    /// - we DO NOT see EITHER "--functions-worker-id" or "--workerId" on the command line as flags.
    /// The host and worker process will share the top two bullet points; however, only the worker process will have the flags.
    /// Note that this is a subset of <see cref="IsAzureFunctions"/>.
    /// The result is cached after the first evaluation.
    /// </summary>
    internal static bool IsRunningInAzureFunctionsHost
    {
        get
        {
            if (_isRunningInAzureFunctionsHost is { } cached)
            {
                return cached;
            }

            var cmd = Environment.CommandLine ?? string.Empty;

            // Heuristic to detect the worker process.
            // The worker process has these flags.
            // Example from log output:
            // "CommandLine": "Samples.AzureFunctions.V4Isolated.AspNetCore.dll --workerId <GUID> --functions-worker-id <GUID>"
            var result = IsAzureFunctionsIsolated &&
                         cmd.IndexOf("--functions-worker-id", StringComparison.OrdinalIgnoreCase) < 0 &&
                         cmd.IndexOf("--workerId", StringComparison.OrdinalIgnoreCase) < 0;

            _isRunningInAzureFunctionsHost = result;
            return result;
        }
    }

    /// <summary>
    /// Resets all cached values. For testing only.
    /// </summary>
    [TestingOnly]
    internal static void Reset()
    {
        _isAzureAppServices = null;
        _isAzureFunctions = null;
        _isUsingAzureAppServicesSiteExtension = null;
        _isAzureFunctionsIsolated = null;
        _isRunningInAzureFunctionsHost = null;
        _workerRuntime = null;
        _workerRuntimeCached = false;
        _extensionVersion = null;
        _extensionVersionCached = false;
    }
}
