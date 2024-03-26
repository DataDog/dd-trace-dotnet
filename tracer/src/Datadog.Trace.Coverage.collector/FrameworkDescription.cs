// <copyright file="FrameworkDescription.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.InteropServices;

namespace Datadog.Trace;

/// <summary>
/// Shim for FrameworkDescription to satisfy imported references
/// </summary>
internal class FrameworkDescription
{
    public static FrameworkDescription Instance { get; } = new();

    public bool IsWindows() => RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
}
