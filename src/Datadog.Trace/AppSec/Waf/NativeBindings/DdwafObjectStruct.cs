// <copyright file="DdwafObjectStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct DdwafObjectStruct
    {
        [FieldOffset(0)]
        public IntPtr ParameterName;

        [FieldOffset(8)]
        public ulong ParameterNameLength;

        // The equivalent union in LibDdwaf contains more cases, but they don't
        // all map well to .NET types. It shouldn't be necessary to manipulate them
        // they're just provided for debugging.
        // union
        // {
        [FieldOffset(16)]
        public IntPtr RawHandle;

        [FieldOffset(16)]
        public ulong UintValue;

        [FieldOffset(16)]
        public long IntValue;
        // };

        [FieldOffset(24)]
        public ulong NbEntries;

        [FieldOffset(32)]
        public DDWAF_OBJ_TYPE Type;
    }
}
