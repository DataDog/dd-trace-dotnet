// <copyright file="DeprecatedKeys.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1649 // File name must match first type defined in file

#nullable enable

using System.Runtime.CompilerServices;
using Datadog.Trace.Configuration.ConfigurationSources.Registry;

namespace Datadog.Trace.Configuration.ConfigurationSources;

internal readonly struct ConfigKeyProfilerLogPath : IConfigKey
{
#pragma warning disable CS0618 // Type or member is obsolete
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetKey() => ConfigurationKeys.ProfilerLogPath;
#pragma warning restore CS0618 // Type or member is obsolete
}
