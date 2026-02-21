// <copyright file="Azure.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.Serverless;

/// <summary>
/// Cached helpers for detecting Azure serverless platforms.
/// Delegates to <see cref="EnvironmentHelpers"/> for env-var reads.
/// Create a new instance for test isolation; use <see cref="Default"/> for production code.
/// </summary>
internal sealed class Azure
{
    private bool? _isAppServices;
    private bool? _isFunctions;
    private bool? _isUsingSiteExtension;
    private bool? _isFunctionsIsolated;
    private bool? _isRunningInFunctionsHost;

    private string? _workerRuntime;
    private bool _workerRuntimeCached;

    private string? _extensionVersion;
    private bool _extensionVersionCached;

    /// <summary>
    /// Gets the shared singleton instance. Cached after first evaluation.
    /// </summary>
    internal static Azure Default { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the current environment is Azure App Services
    /// by checking for the presence of "WEBSITE_SITE_NAME".
    /// Note that this is a superset of <see cref="IsFunctions"/>.
    /// The result is cached after the first evaluation.
    /// </summary>
    internal bool IsAppServices =>
        _isAppServices ??= EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.AzureAppService.SiteNameKey);

    /// <summary>
    /// Gets a value indicating whether the current environment is Azure Functions
    /// by checking for the presence of "WEBSITE_SITE_NAME", "FUNCTIONS_WORKER_RUNTIME", and "FUNCTIONS_EXTENSION_VERSION".
    /// Note that this is a subset of <see cref="IsAppServices"/>.
    /// The result is cached after the first evaluation.
    /// </summary>
    internal bool IsFunctions =>
        _isFunctions ??=
            IsAppServices &&
            EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.AzureFunctions.FunctionsWorkerRuntime) &&
            EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.AzureFunctions.FunctionsExtensionVersion);

    /// <summary>
    /// Gets a value indicating whether the current environment is using Azure App Services Site Extension
    /// by checking for the presence of "DD_AZURE_APP_SERVICES=1" and "DD_AAS_DOTNET_EXTENSION_VERSION".
    /// The result is cached after the first evaluation.
    /// </summary>
    internal bool IsUsingSiteExtension =>
        _isUsingSiteExtension ??=
            EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.AzureAppService.AzureAppServicesContextKey) == "1" &&
            EnvironmentHelpers.EnvironmentVariableExists(ConfigurationKeys.AzureAppService.SiteExtensionVersionKey);

    /// <summary>
    /// Gets a value indicating whether the current environment is an Azure Functions isolated worker process
    /// (as opposed to in-process functions) by checking that:
    ///
    /// - <see cref="IsFunctions"/> is <c>true</c>
    /// - "FUNCTIONS_WORKER_RUNTIME" is set to "dotnet-isolated"
    ///
    /// This will return true for both the host process and worker process in isolated functions.
    /// Use <see cref="IsRunningInFunctionsHost"/> to distinguish between host and worker.
    /// The result is cached after the first evaluation.
    /// </summary>
    internal bool IsFunctionsIsolated =>
        _isFunctionsIsolated ??=
            IsFunctions && string.Equals(WorkerRuntime, "dotnet-isolated", StringComparison.Ordinal);

    /// <summary>
    /// Gets the cached value of the "FUNCTIONS_WORKER_RUNTIME" environment variable.
    /// </summary>
    internal string? WorkerRuntime
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
    internal string? ExtensionVersion
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
    /// - <see cref="IsFunctionsIsolated"/> is <c>true</c>
    /// - we DO NOT see EITHER "--functions-worker-id" or "--workerId" on the command line as flags.
    /// The host and worker process will share the top two bullet points; however, only the worker process will have the flags.
    /// Note that this is a subset of <see cref="IsFunctions"/>.
    /// The result is cached after the first evaluation.
    /// </summary>
    internal bool IsRunningInFunctionsHost
    {
        get
        {
            if (_isRunningInFunctionsHost is { } cached)
            {
                return cached;
            }

            var cmd = Environment.CommandLine ?? string.Empty;

            // Heuristic to detect the worker process.
            // The worker process has these flags.
            // Example from log output:
            // "CommandLine": "Samples.AzureFunctions.V4Isolated.AspNetCore.dll --workerId <GUID> --functions-worker-id <GUID>"
            var result = IsFunctionsIsolated &&
                         cmd.IndexOf("--functions-worker-id", StringComparison.OrdinalIgnoreCase) < 0 &&
                         cmd.IndexOf("--workerId", StringComparison.OrdinalIgnoreCase) < 0;

            _isRunningInFunctionsHost = result;
            return result;
        }
    }
}
