// <copyright file="PlatformKeys.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration;

internal static partial class PlatformKeys
{
    /// <summary>
    /// Built-in Windows environment variable that holds the system's unique name (hostname).
    /// Also, the instance name in Azure App Services where the instrumented application is running.
    /// </summary>
    internal const string ComputerNameKey = "COMPUTERNAME";
}
