// <copyright file="DdwafObjectStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Datadog.Trace.Vendors.Serilog;

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
        public IntPtr Array;

        [FieldOffset(16)]
        public ulong UintValue;

        [FieldOffset(16)]
        public long IntValue;
        // };

        [FieldOffset(24)]
        public ulong NbEntries;

        [FieldOffset(32)]
        public DDWAF_OBJ_TYPE Type;

        internal IReadOnlyDictionary<string, string[]> Decode()
        {
            var nbEntriesStart = (int)NbEntries;
            var errorsDic = new Dictionary<string, string[]>(nbEntriesStart);
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
                        var arrayPtr = new IntPtr(Array.ToInt64() + (structSize * i));
                        var array = (DdwafObjectStruct?)Marshal.PtrToStructure(arrayPtr, typeof(DdwafObjectStruct));
                        if (array is { } arrayValue)
                        {
                            var key = Marshal.PtrToStringAnsi(arrayValue.ParameterName, (int)arrayValue.ParameterNameLength);
                            var nbEntries = (int)arrayValue.NbEntries;
                            var ruleIds = new string[nbEntries];
                            for (var j = 0; j < nbEntries; j++)
                            {
                                var errorPtr = new IntPtr(arrayValue.Array.ToInt64() + (structSize * j));
                                var error = (DdwafObjectStruct?)Marshal.PtrToStructure(errorPtr, typeof(DdwafObjectStruct));
                                var ruleId = Marshal.PtrToStringAnsi(error!.Value.Array);
                                ruleIds[j] = ruleId!;
                            }

                            errorsDic.Add(key, ruleIds);
                        }
                    }
                }
            }

            return errorsDic;
        }
    }
}
