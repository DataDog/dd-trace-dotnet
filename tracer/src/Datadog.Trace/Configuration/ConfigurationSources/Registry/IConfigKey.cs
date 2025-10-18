// <copyright file="IConfigKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration.ConfigurationSources.Registry;

/// <summary>
/// Represents a configuration key with compile-time type safety.
/// Uses static abstract interface members (C# 11+) to avoid boxing allocations.
/// </summary>
public interface IConfigKey
{
    internal string GetKey();
}
