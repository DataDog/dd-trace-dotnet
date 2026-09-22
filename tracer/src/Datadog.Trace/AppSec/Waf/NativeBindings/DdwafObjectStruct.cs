// <copyright file="DdwafObjectStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    /// <summary>
    /// Mirrors the <c>ddwaf_object</c> union from libddwaf 2.x. It is a 16 byte tagged union: the
    /// type lives in the first byte and the remaining 15 bytes are interpreted differently per type.
    ///
    /// <code>
    /// offset | field
    /// -------+----------------------------------------------------
    ///      0 | type                         (uint8, all types)
    ///      1 | b8.val                       (bool)
    ///      1 | sstr.size                    (uint8, small string)
    ///      2 | sstr.data[14]                (small string payload)
    ///      2 | array.size / map.size        (uint16)
    ///      4 | array.capacity/map.capacity  (uint16)
    ///      4 | str.size                     (uint32)
    ///      8 | str.ptr / array.ptr /map.ptr (pointer)
    ///      8 | i64.val / u64.val / f64.val  (8 bytes)
    /// </code>
    ///
    /// Note that a map points at an array of <see cref="DdwafObjectKvStruct"/> (32 bytes each) rather
    /// than at objects: since 2.x, keys are no longer stored inside the object itself.
    ///
    /// The layout is asserted at runtime by <c>DdwafObjectLayoutTests</c>; keep the two in sync.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ObjectSize)]
    internal struct DdwafObjectStruct
    {
        /// <summary>
        /// Size in bytes of a single <c>ddwaf_object</c>, i.e. the stride of an array's buffer.
        /// </summary>
        internal const int ObjectSize = 16;

        /// <summary>
        /// Number of bytes of small string payload that can be inlined in an object.
        /// </summary>
        internal const int SmallStringCapacity = 14;

        /// <summary>
        /// Hard limit on the number of elements a container can hold, because both the size and the
        /// capacity of arrays and maps are 16 bit. libddwaf itself does not guard against overflowing
        /// it (its grow helper saturates at this value and then writes past the end of the buffer), so
        /// callers building objects must clamp to it.
        /// </summary>
        internal const int MaxContainerCapacity = ushort.MaxValue;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DdwafObjectStruct>();

        [FieldOffset(0)]
        public DDWAF_OBJ_TYPE Type;

        /// <summary>
        /// <c>b8.val</c>. Don't use a non blittable type (bool) as we use unsafe pointer writes to
        /// marshal/unmarshal for faster performances.
        /// </summary>
        [FieldOffset(1)]
        public byte BoolByte;

        /// <summary>
        /// <c>sstr.size</c>, the length of an inlined small string (0 to <see cref="SmallStringCapacity"/> bytes).
        /// </summary>
        [FieldOffset(1)]
        public byte SmallStringSize;

        /// <summary>
        /// First byte of <c>sstr.data</c>. Only its address is ever used, to read or write the small
        /// string payload inlined in the object.
        /// </summary>
        [FieldOffset(2)]
        public byte SmallStringData;

        /// <summary>
        /// <c>array.size</c> / <c>map.size</c>: the number of elements currently in the container.
        /// </summary>
        [FieldOffset(2)]
        public ushort Size;

        /// <summary>
        /// <c>array.capacity</c> / <c>map.capacity</c>: the number of elements the buffer can hold.
        /// When building objects by hand this must be set, not just <see cref="Size"/>, otherwise
        /// libddwaf miscalculates the buffer size when growing or destroying the container.
        /// </summary>
        [FieldOffset(4)]
        public ushort Capacity;

        /// <summary>
        /// <c>str.size</c>: the length in bytes of a (non small) string.
        /// </summary>
        [FieldOffset(4)]
        public uint StringLength;

        /// <summary>
        /// <c>str.ptr</c> / <c>array.ptr</c> / <c>map.ptr</c>.
        /// </summary>
        [FieldOffset(8)]
        public IntPtr Pointer;

        [FieldOffset(8)]
        public ulong UintValue;

        [FieldOffset(8)]
        public long IntValue;

        [FieldOffset(8)]
        public double DoubleValue;

        public bool BoolValue => Type == DDWAF_OBJ_TYPE.DDWAF_OBJ_BOOL && BoolByte != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal object? Decode()
        {
            object? res = Type switch
            {
                DDWAF_OBJ_TYPE.DDWAF_OBJ_STRING => DecodeString(),
                DDWAF_OBJ_TYPE.DDWAF_OBJ_LITERAL_STRING => DecodeString(),
                DDWAF_OBJ_TYPE.DDWAF_OBJ_SMALL_STRING => DecodeString(),
                DDWAF_OBJ_TYPE.DDWAF_OBJ_SIGNED => IntValue,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_UNSIGNED => UintValue,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_BOOL => BoolValue,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_FLOAT => DoubleValue,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_ARRAY => DecodeArray<object>(),
                DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP => DecodeMap(),
                _ => null
            };

            return res;
        }

        /// <summary>
        /// Decodes any of the three string representations. Strings coming out of the WAF are UTF-8
        /// and are *not* NUL terminated, so the explicit length must always be used.
        /// </summary>
        /// <returns>the decoded string, or null if this object is not a string</returns>
        internal unsafe string? DecodeString()
        {
            switch (Type)
            {
                case DDWAF_OBJ_TYPE.DDWAF_OBJ_SMALL_STRING:
                    // the payload is inlined in the object itself, there is no indirection to follow
                    fixed (byte* smallStringPtr = &SmallStringData)
                    {
                        return StringEncoding.UTF8.GetString(smallStringPtr, SmallStringSize);
                    }

                case DDWAF_OBJ_TYPE.DDWAF_OBJ_STRING:
                case DDWAF_OBJ_TYPE.DDWAF_OBJ_LITERAL_STRING:
                    if (Pointer == IntPtr.Zero)
                    {
                        return null;
                    }

                    return StringEncoding.UTF8.GetString((byte*)Pointer, (int)StringLength);

                default:
                    return null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal List<string> DecodeStringArray()
        {
            return DecodeArray<string>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal List<object> DecodeObjectArray()
        {
            return DecodeArray<object>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe Dictionary<string, object?> DecodeMap()
        {
            var nbEntries = Size;
            var res = new Dictionary<string, object?>(nbEntries);
            if (nbEntries > 0)
            {
                if (Type != DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP)
                {
                    Log.Warning("Expecting type {DDWAF_OBJ_MAP} to decode waf errors and instead got a {Type} ", nameof(DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP), Type);
                }
                else if (Pointer != IntPtr.Zero)
                {
                    var entries = (DdwafObjectKvStruct*)Pointer;
                    for (var i = 0; i < nbEntries; i++)
                    {
                        var key = entries[i].Key.DecodeString();
                        if (key is null)
                        {
                            // keys are always strings in practice, skip anything unexpected
                            continue;
                        }

                        res[key] = entries[i].Value.Decode();
                    }
                }
            }

            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe List<T> DecodeArray<T>()
        {
            var nbEntries = Size;
            var res = new List<T>(nbEntries);
            if (nbEntries > 0)
            {
                if (Type != DDWAF_OBJ_TYPE.DDWAF_OBJ_ARRAY)
                {
                    Log.Warning("Expecting type {DDWAF_OBJ_ARRAY} to decode waf errors and instead got a {Type} ", nameof(DDWAF_OBJ_TYPE.DDWAF_OBJ_ARRAY), Type);
                }
                else if (Pointer != IntPtr.Zero)
                {
                    var items = (DdwafObjectStruct*)Pointer;
                    for (var i = 0; i < nbEntries; i++)
                    {
                        var value = (T?)items[i].Decode();
                        if (value != null)
                        {
                            res.Add(value);
                        }
                    }
                }
            }

            return res;
        }
    }
}
