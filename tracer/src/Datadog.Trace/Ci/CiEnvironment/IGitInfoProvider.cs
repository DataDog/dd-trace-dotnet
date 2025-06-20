// <copyright file="IGitInfoProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Datadog.Trace.Ci.CiEnvironment;

/// <summary>
/// Git info provider interface
/// </summary>
internal interface IGitInfoProvider
{
    bool TryGetFrom(string folder, [NotNullWhen(true)] out IGitInfo? gitInfo);

    bool TryGetFrom(FileSystemInfo gitDirectory, [NotNullWhen(true)] out IGitInfo? gitInfo);
}
