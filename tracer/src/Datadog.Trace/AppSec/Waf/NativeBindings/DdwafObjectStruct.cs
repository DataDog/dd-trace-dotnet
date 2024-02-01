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

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct DdwafObjectStruct
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DdwafObjectStruct>();

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
        public IntPtr Array;

        [FieldOffset(16)]
        public ulong UintValue;

        [FieldOffset(16)]
        public long IntValue;

        /// <summary>
        /// Dont use non blittable type as we use unsafe to marshall/unmarshall for faster performances.
        /// </summary>
        [FieldOffset(16)]
        public byte ByteValue;

        [FieldOffset(16)]
        public double DoubleValue;
        // };

        [FieldOffset(24)]
        public ulong NbEntries;

        [FieldOffset(32)]
        public DDWAF_OBJ_TYPE Type;

        public bool BoolValue => Type == DDWAF_OBJ_TYPE.DDWAF_OBJ_BOOL && ByteValue == 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal object? Decode()
        {
            object? res = Type switch
            {
                DDWAF_OBJ_TYPE.DDWAF_OBJ_STRING => Marshal.PtrToStringAnsi(Array, (int)NbEntries),
                DDWAF_OBJ_TYPE.DDWAF_OBJ_SIGNED => IntValue,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_UNSIGNED => UintValue,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_BOOL => BoolValue,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_DOUBLE => DoubleValue,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_ARRAY => DecodeArray<object>(),
                DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP => DecodeMap(),
                _ => null
            };

            return res;
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
        internal Dictionary<string, object?> DecodeMap()
        {
            var nbEntriesStart = (int)NbEntries;
            var res = new Dictionary<string, object?>(nbEntriesStart);
            if (nbEntriesStart > 0)
            {
                if (Type != DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP)
                {
                    Log.Warning("Expecting type {DDWAF_OBJ_MAP} to decode waf errors and instead got a {Type} ", nameof(DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP), Type);
                }
                else
                {
                    var structSize = Marshal.SizeOf(typeof(DdwafObjectStruct));
                    for (var i = 0; i < nbEntriesStart; i++)
                    {
                        unsafe
                        {
                            var arrayPtr = new IntPtr(Array.ToInt64() + (structSize * i));
                            var array = (DdwafObjectStruct*)arrayPtr;
                            var key = Marshal.PtrToStringAnsi(array->ParameterName, (int)array->ParameterNameLength);
                            var value = array->Decode();
                            res.Add(key, value);
                        }
                    }
                }
            }

            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private List<T> DecodeArray<T>()
        {
            var nbEntriesStart = (int)NbEntries;
            var res = new List<T>(nbEntriesStart);
            if (nbEntriesStart > 0)
            {
                if (Type != DDWAF_OBJ_TYPE.DDWAF_OBJ_ARRAY)
                {
                    Log.Warning("Expecting type {DDWAF_OBJ_ARRAY} to decode waf errors and instead got a {Type} ", nameof(DDWAF_OBJ_TYPE.DDWAF_OBJ_ARRAY), Type);
                }
                else
                {
                    var structSize = Marshal.SizeOf(typeof(DdwafObjectStruct));
                    for (var i = 0; i < nbEntriesStart; i++)
                    {
                        unsafe
                        {
                            var arrayPtr = new IntPtr(Array.ToInt64() + (structSize * i));
                            var array = (DdwafObjectStruct*)arrayPtr;
                            var value = (T?)array->Decode();
                            if (value != null)
                            {
                                res.Add(value);
                            }
                        }
                    }
                }
            }

            return res;
        }
    }
}
