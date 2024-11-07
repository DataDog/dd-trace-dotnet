// <copyright file="IGitInfoProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;

namespace Datadog.Trace.Ci.CiEnvironment;

/// <summary>
/// Git info provider interface
/// </summary>
internal interface IGitInfoProvider
{
    bool TryGetFrom(DirectoryInfo gitDirectory, out IGitInfo gitInfo);
}
