// <copyright file="DdwafObjectKvStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Runtime.InteropServices;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    /// <summary>
    /// Mirrors the <c>ddwaf_object_kv</c> struct from libddwaf 2.x: a key/value pair of two
    /// <see cref="DdwafObjectStruct"/>. This is the element type of a map's buffer, replacing the
    /// <c>parameterName</c> field that objects themselves carried in libddwaf 1.x.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = KvSize)]
    internal struct DdwafObjectKvStruct
    {
        /// <summary>
        /// Size in bytes of a single <c>ddwaf_object_kv</c>, i.e. the stride of a map's buffer.
        /// </summary>
        internal const int KvSize = 2 * DdwafObjectStruct.ObjectSize;

        [FieldOffset(0)]
        public DdwafObjectStruct Key;

        [FieldOffset(DdwafObjectStruct.ObjectSize)]
        public DdwafObjectStruct Value;
    }
}
