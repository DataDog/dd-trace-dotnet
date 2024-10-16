// <copyright file="DefaultEnvironmentChecker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;

namespace Datadog.Trace.Debugger.Caching;

internal class DefaultEnvironmentChecker : IEnvironmentChecker
{
    private static readonly Lazy<DefaultEnvironmentChecker> _instance = new Lazy<DefaultEnvironmentChecker>(() => new DefaultEnvironmentChecker(), LazyThreadSafetyMode.ExecutionAndPublication);
    private readonly bool _isServerlessEnvironment;

    private DefaultEnvironmentChecker()
    {
        _isServerlessEnvironment = CheckServerlessEnvironment();
    }

    public static DefaultEnvironmentChecker Instance => _instance.Value;

    public bool IsServerlessEnvironment()
    {
        return _isServerlessEnvironment;
    }

    private bool CheckServerlessEnvironment()
    {
        // First we are checking the tracer RCM, this will return true only in a non-serverless environment
        var isRcmAvailable = Tracer.Instance?.Settings?.IsRemoteConfigurationAvailable;
        if (isRcmAvailable.HasValue)
        {
            return !isRcmAvailable.Value;
        }

        // Checking serverless environment based on environment variables
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME")) ||
               (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTION_NAME")) &&
                Environment.GetEnvironmentVariable("FUNCTION_SIGNATURE_TYPE") is "http" or "event");
    }
}
