// <copyright file="DefaultEnvironmentChecker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.Caching;

internal class DefaultEnvironmentChecker : IEnvironmentChecker
{
    private DefaultEnvironmentChecker()
    {
        IsServerlessEnvironment = CheckServerlessEnvironment();
    }

    internal static DefaultEnvironmentChecker Instance { get; } = new();

    public bool IsServerlessEnvironment { get; }

    private bool CheckServerlessEnvironment()
    {
        // First we are checking the tracer RCM, this will return true only in a non-serverless environment
        var isRcmAvailable = Tracer.Instance?.Settings?.IsRemoteConfigurationAvailable;
        if (isRcmAvailable.HasValue)
        {
            return !isRcmAvailable.Value;
        }

        // Checking serverless environment based on environment variables
        return !string.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")) ||
               !string.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME")) ||
               (!string.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable("FUNCTION_NAME")) &&
                EnvironmentHelpers.GetEnvironmentVariable("FUNCTION_SIGNATURE_TYPE") is "http" or "event");
    }
}
