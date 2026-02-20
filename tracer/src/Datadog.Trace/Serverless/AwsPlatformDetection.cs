// <copyright file="AwsPlatformDetection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.Serverless;

/// <summary>
/// Cached helpers for detecting AWS serverless platforms.
/// Delegates to <see cref="EnvironmentHelpers"/> for env-var reads.
/// </summary>
internal static class AwsPlatformDetection
{
    private static bool? _isAwsLambda;

    /// <summary>
    /// Gets a value indicating whether the current environment is AWS Lambda
    /// by checking for the presence of "AWS_LAMBDA_FUNCTION_NAME".
    /// The result is cached after the first evaluation.
    /// </summary>
    internal static bool IsAwsLambda =>
        _isAwsLambda ??= EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.Aws.LambdaFunctionName);

    /// <summary>
    /// Resets all cached values. For testing only.
    /// </summary>
    [TestingOnly]
    internal static void Reset()
    {
        _isAwsLambda = null;
    }
}
