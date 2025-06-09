// <copyright file="TracerMemfdHandleResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog.ServiceDiscovery;

// **DO NOT USE THIS TYPE in x86** to map w ith Libdatadog. OK and Err fields needs a 4 offset instead.
[StructLayout(LayoutKind.Explicit)]
internal struct TracerMemfdHandleResult
{
    [FieldOffset(0)]
    public ResultTag Tag;

    // beware that offset 8 is only valid on x64 and would cause a crash if read on x86.
    [FieldOffset(8)]
    public TracerMemfdHandle Ok;

    [FieldOffset(8)]
    public Error Err;
}
