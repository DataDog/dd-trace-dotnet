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
        // Checking serverless environment based on environment variables
        return EnvironmentHelpers.IsServerlessEnvironment();
    }
}
