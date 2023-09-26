// <copyright file="EnvironmentConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.dd_dotnet;

internal class EnvironmentConfigurationSource : IConfigurationSource
{
    public string? GetString(string key)
    {
        return System.Environment.GetEnvironmentVariable(key);
    }
}
