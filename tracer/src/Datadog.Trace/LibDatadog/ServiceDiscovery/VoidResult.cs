// <copyright file="VoidResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog.ServiceDiscovery;

/// <summary>
/// Libdatadog interop mapping of <c>ddog_VoidResult</c>:
/// https://github.com/DataDog/libdatadog/blob/ef6371243c020319e74e8531035f0a5a2fcb8858/libdd-common-ffi/src/result.rs#L47
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VoidResult
{
    public ResultTag Tag;

    public Error Error;
}
