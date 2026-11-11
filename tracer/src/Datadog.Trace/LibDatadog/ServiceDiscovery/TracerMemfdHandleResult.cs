// <copyright file="TracerMemfdHandleResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog.ServiceDiscovery;

/// <summary>
/// Do not change the values of this enum unless you really need to update the interop mapping.
/// Libdatadog interop mapping of the generic type: https://github.com/DataDog/libdatadog/blob/ef6371243c020319e74e8531035f0a5a2fcb8858/libdd-common-ffi/src/result.rs#L47
/// Cf also ddog_Result_TracerMemfdHandle in common.h headers.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TracerMemfdHandleResult
{
    public ResultTag Tag;

    public ResultUnion Result;

    [StructLayout(LayoutKind.Explicit)]
    public struct ResultUnion
    {
        [FieldOffset(0)]
        public int TracerMemfdHandle;

        [FieldOffset(0)]
        public Error Error;
    }
}
