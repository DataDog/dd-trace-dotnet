// <copyright file="LibraryConfigResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog.HandsOffConfiguration.InteropStructs;

[StructLayout(LayoutKind.Sequential)]
internal struct LibraryConfigResult
{
    public ResultTag Tag;

    public ResultUnion Result;

    // Manual union overlay
    [StructLayout(LayoutKind.Explicit)]
    public struct ResultUnion
    {
        [FieldOffset(0)]
        public LibraryConfigsWithLogs Ok;

        [FieldOffset(0)]
        public Error Error;
    }
}
