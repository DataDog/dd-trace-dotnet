// <copyright file="GcpPlatformDetection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.Serverless;

/// <summary>
/// Cached helpers for detecting Google Cloud serverless platforms.
/// Delegates to <see cref="EnvironmentHelpers"/> for env-var reads.
/// </summary>
internal static class GcpPlatformDetection
{
    private static bool? _isGoogleCloudFunctions;

    /// <summary>
    /// Gets a value indicating whether the current environment is Google Cloud Functions
    /// by checking for the presence of either "K_SERVICE" and "FUNCTION_TARGET",
    /// or "FUNCTION_NAME" and "GCP_PROJECT".
    /// The result is cached after the first evaluation.
    /// </summary>
    internal static bool IsGoogleCloudFunctions =>
        _isGoogleCloudFunctions ??=
            (EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.GcpFunction.FunctionNameKey) &&
             EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.GcpFunction.FunctionTargetKey)) ||
            (EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.GcpFunction.DeprecatedFunctionNameKey) &&
             EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.GcpFunction.DeprecatedProjectKey));

    /// <summary>
    /// Resets all cached values. For testing only.
    /// </summary>
    [TestingOnly]
    internal static void Reset()
    {
        _isGoogleCloudFunctions = null;
    }
}
