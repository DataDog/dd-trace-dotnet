// <copyright file="PWArgs.cs" company="Datadog">
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
    internal struct PWArgs
    {
        [FieldOffset(0)]
        public IntPtr ParameterName;

        [FieldOffset(8)]
        public ulong ParameterNameLength;

        // union
        // {
        // TODO this needs to be marshalled to string, since an object type can't share the same spaces as value type
        [FieldOffset(16)]
        public IntPtr StringValue;

        [FieldOffset(16)]
        public ulong UintValue;

        [FieldOffset(16)]
        public long IntValue;

        [FieldOffset(16)]
        public IntPtr PWArgs32Array;

        [FieldOffset(16)]
        public IntPtr RawHandle;
        // };

        [FieldOffset(24)]
        public ulong NbEntries;

        [FieldOffset(32)]
        public PW_INPUT_TYPE Type;
    }
}
