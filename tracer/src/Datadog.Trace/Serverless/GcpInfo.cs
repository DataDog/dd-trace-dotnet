// <copyright file="GcpInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.Serverless;

/// <summary>
/// Cached helpers for detecting Google Cloud serverless platforms.
/// Delegates to <see cref="EnvironmentHelpers"/> for env-var reads.
/// Create a new instance for test isolation; use <see cref="Instance"/> for production code.
/// </summary>
internal sealed class GcpInfo
{
    private bool? _isCloudFunction;

    /// <summary>
    /// Gets the shared singleton instance. Cached after first evaluation.
    /// </summary>
    internal static GcpInfo Instance { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the current environment is Google Cloud Functions
    /// by checking for the presence of either "K_SERVICE" and "FUNCTION_TARGET",
    /// or "FUNCTION_NAME" and "GCP_PROJECT".
    /// The result is cached after the first evaluation.
    /// </summary>
    internal bool IsCloudFunction =>
        _isCloudFunction ??=
            (EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.GcpFunction.FunctionNameKey) &&
             EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.GcpFunction.FunctionTargetKey)) ||
            (EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.GcpFunction.DeprecatedFunctionNameKey) &&
             EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.GcpFunction.DeprecatedProjectKey));
}
