// <copyright file="Aws.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.Serverless;

/// <summary>
/// Cached helpers for detecting AWS serverless platforms.
/// Delegates to <see cref="EnvironmentHelpers"/> for env-var reads.
/// Create a new instance for test isolation; use <see cref="Default"/> for production code.
/// </summary>
internal sealed class Aws
{
    private bool? _isLambda;

    /// <summary>
    /// Gets the shared singleton instance. Cached after first evaluation.
    /// </summary>
    internal static Aws Default { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the current environment is AWS Lambda
    /// by checking for the presence of "AWS_LAMBDA_FUNCTION_NAME".
    /// The result is cached after the first evaluation.
    /// </summary>
    internal bool IsLambda =>
        _isLambda ??= EnvironmentHelpers.EnvironmentVariableExists(PlatformKeys.Aws.LambdaFunctionName);
}
