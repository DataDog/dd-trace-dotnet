// <copyright file="AzureInfo.cs" company="Datadog">
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
/// Delegates to <see cref="Util.EnvironmentHelpers"/> for env-var reads.
/// Create a new instance for test isolation; use <see cref="Instance"/> for production code.
/// </summary>
internal sealed class AzureInfo
{
    private bool? _isAppService;
    private bool? _isFunction;
    private bool? _isUsingSiteExtension;
    private bool? _isIsolatedFunction;
    private bool? _isIsolatedFunctionHostProcess;
    private bool? _isIsolatedFunctionWorkerProcess;
    private bool _functionWorkerRuntimeCached;
    private bool _functionExtensionVersionCached;

    /// <summary>
    /// Gets the shared singleton instance. Cached after first evaluation.
    /// </summary>
    internal static AzureInfo Instance { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the current environment is Azure App Services
    /// by checking for the presence of "WEBSITE_SITE_NAME".
    /// Note that this is a superset of <see cref="IsFunction"/>.
    /// The result is cached after the first evaluation.
    /// </summary>
    internal bool IsAppService =>
        _isAppService ??= EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.AzureAppService.SiteNameKey);

    /// <summary>
    /// Gets a value indicating whether the current environment is Azure Functions
    /// by checking for the presence of "WEBSITE_SITE_NAME", "FUNCTIONS_WORKER_RUNTIME", and "FUNCTIONS_EXTENSION_VERSION".
    /// Note that this is a subset of <see cref="IsAppService"/>.
    /// The result is cached after the first evaluation.
    /// </summary>
    internal bool IsFunction =>
        _isFunction ??=
            IsAppService &&
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
    /// - <see cref="IsFunction"/> is <c>true</c>
    /// - "FUNCTIONS_WORKER_RUNTIME" is set to "dotnet-isolated"
    ///
    /// This will return true for both the host process and worker process in isolated functions.
    /// Use <see cref="IsIsolatedFunctionHostProcess"/> to distinguish between host and worker.
    /// The result is cached after the first evaluation.
    /// </summary>
    internal bool IsIsolatedFunction =>
        _isIsolatedFunction ??=
            IsFunction && string.Equals(FunctionWorkerRuntime, "dotnet-isolated", StringComparison.Ordinal);

    /// <summary>
    /// Gets the cached value of the "FUNCTIONS_WORKER_RUNTIME" environment variable.
    /// </summary>
    internal string? FunctionWorkerRuntime
    {
        get
        {
            if (!_functionWorkerRuntimeCached)
            {
                field = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.AzureFunctions.FunctionsWorkerRuntime, defaultValue: string.Empty);
                _functionWorkerRuntimeCached = true;
            }

            return field;
        }
    }

    /// <summary>
    /// Gets the cached value of the "FUNCTIONS_EXTENSION_VERSION" environment variable.
    /// </summary>
    internal string? FunctionExtensionVersion
    {
        get
        {
            if (!_functionExtensionVersionCached)
            {
                field = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.AzureFunctions.FunctionsExtensionVersion, defaultValue: string.Empty);
                _functionExtensionVersionCached = true;
            }

            return field;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the current environment is the Azure Functions host process
    /// by checking that:
    /// - <see cref="IsIsolatedFunction"/> is <c>true</c>
    /// - we do not see either "--functions-worker-id" or "--workerId" on the command line.
    /// Only the worker process will have these command-line parameters.
    /// Note that this is a subset of <see cref="IsFunction"/> and <see cref="IsIsolatedFunction"/>.
    /// The result is cached after the first evaluation.
    /// </summary>
    internal bool IsIsolatedFunctionHostProcess
    {
        get
        {
            if (_isIsolatedFunctionHostProcess is { } cached)
            {
                return cached;
            }

            var cmd = Environment.CommandLine ?? string.Empty;

            // Heuristic to detect the worker process.
            // The worker process has these flags.
            // Example from log output:
            // "CommandLine": "Samples.AzureFunctions.V4Isolated.AspNetCore.dll --workerId <GUID> --functions-worker-id <GUID>"
            var result = IsIsolatedFunction &&
                         cmd.IndexOf("--functions-worker-id", StringComparison.OrdinalIgnoreCase) < 0 &&
                         cmd.IndexOf("--workerId", StringComparison.OrdinalIgnoreCase) < 0;

            _isIsolatedFunctionHostProcess = result;
            return result;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the current environment is the Azure Functions worker process
    /// by checking that:
    /// - <see cref="IsIsolatedFunction"/> is <c>true</c>
    /// - we see either "--functions-worker-id" or "--workerId" on the command line.
    /// Only the worker process will have these command-line parameters.
    /// Note that this is a subset of <see cref="IsFunction"/> and <see cref="IsIsolatedFunction"/>.
    /// The result is cached after the first evaluation.
    /// </summary>
    internal bool IsIsolatedFunctionWorkerProcess
    {
        get
        {
            if (_isIsolatedFunctionWorkerProcess is { } cached)
            {
                return cached;
            }

            var result = IsIsolatedFunction && !IsIsolatedFunctionHostProcess;
            _isIsolatedFunctionWorkerProcess = result;
            return result;
        }
    }
}
