// <copyright file="DefaultEnvironmentChecker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Serverless;

namespace Datadog.Trace.Debugger.Caching;

internal sealed class DefaultEnvironmentChecker : IEnvironmentChecker
{
    private DefaultEnvironmentChecker()
    {
        IsServerlessEnvironment = CheckServerlessEnvironment();
    }

    internal static DefaultEnvironmentChecker Instance { get; } = new();

    public bool IsServerlessEnvironment { get; }

    private static bool CheckServerlessEnvironment()
    {
        // Checking serverless environment based on environment variables
        return AwsInfo.Instance.IsLambda ||
               AzureInfo.Instance.IsFunction ||
               GcpInfo.Instance.IsCloudFunction;
    }
}
