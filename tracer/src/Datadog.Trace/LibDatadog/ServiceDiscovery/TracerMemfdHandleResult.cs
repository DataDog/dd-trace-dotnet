// <copyright file="TracerMemfdHandleResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog.ServiceDiscovery;

/// <summary>
/// **DO NOT USE THIS TYPE in x86** to map with Libdatadog. OK and Err fields needs a 4 offset instead.
/// Do not change the values of this enum unless you really need to update the interop mapping.
/// Libdatadog interop mapping of the generic type: https://github.com/DataDog/libdatadog/blob/60583218a8de6768f67d04fcd5bc6443f67f516b/ddcommon-ffi/src/result.rs#L44
/// Cf also ddog_Result_TracerMemfdHandle in common.h headers.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
internal struct TracerMemfdHandleResult
{
    [FieldOffset(0)]
    public ResultTag Tag;

    // beware that offset 8 is only valid on x64 and would cause a crash if read on x86.
    [FieldOffset(8)]
    public int TracerMemfdHandle;

    [FieldOffset(8)]
    public Error Error;
}
