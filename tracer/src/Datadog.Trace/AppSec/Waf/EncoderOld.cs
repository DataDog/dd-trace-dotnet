// <copyright file="EncoderOld.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Waf
{
    internal static class EncoderOld
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EncoderOld));
        private static readonly int ObjectStructSize = Marshal.SizeOf(typeof(DdwafObjectStruct));

        public static ObjTypeOld DecodeArgsType(DDWAF_OBJ_TYPE t)
        {
            return t switch
            {
                DDWAF_OBJ_TYPE.DDWAF_OBJ_INVALID => ObjTypeOld.Invalid,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_SIGNED => ObjTypeOld.SignedNumber,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_UNSIGNED => ObjTypeOld.UnsignedNumber,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_STRING => ObjTypeOld.String,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_ARRAY => ObjTypeOld.Array,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP => ObjTypeOld.Map,
                _ => throw new Exception($"Invalid DDWAF_INPUT_TYPE {t}")
            };
        }

        private static string TruncateLongString(string s) => s.Length > WafConstants.MaxStringLength ? s.Substring(0, WafConstants.MaxStringLength) : s;

        public static ObjOld Encode(object o, WafLibraryInvoker wafLibraryInvoker, List<ObjOld>? argCache, bool applySafetyLimits = true) => EncodeInternal(o, argCache, WafConstants.MaxContainerDepth, applySafetyLimits, wafLibraryInvoker);

        public static object Decode(ObjOld o) => InnerDecode(o.InnerStruct);

        public static object InnerDecode(DdwafObjectStruct o)
        {
            switch (DecodeArgsType(o.Type))
            {
                case ObjTypeOld.Invalid:
                    return new object();
                case ObjTypeOld.SignedNumber:
                    return o.IntValue;
                case ObjTypeOld.UnsignedNumber:
                    return o.UintValue;
                case ObjTypeOld.String:
                    return Marshal.PtrToStringAnsi(o.Array) ?? string.Empty;
                case ObjTypeOld.Array:
                    var arr = new object[o.NbEntries];
                    for (var i = 0; i < arr.Length; i++)
                    {
                        var nextObj = Marshal.PtrToStructure(o.Array + (i * ObjectStructSize), typeof(DdwafObjectStruct));
                        if (nextObj != null)
                        {
                            var next = (DdwafObjectStruct)nextObj;
                            arr[i] = InnerDecode(next);
                        }
                    }

                    return arr;
                case ObjTypeOld.Map:
                    var entries = (int)o.NbEntries;
                    var map = new Dictionary<string, object>(entries);
                    for (int i = 0; i < entries; i++)
                    {
                        var nextObj = Marshal.PtrToStructure(o.Array + (i * ObjectStructSize), typeof(DdwafObjectStruct));
                        if (nextObj != null)
                        {
                            var next = (DdwafObjectStruct)nextObj;
                            var key = Marshal.PtrToStringAnsi(next.ParameterName, (int)next.ParameterNameLength) ?? string.Empty;
                            map[key] = InnerDecode(next);
                            var nextO = InnerDecode(next);
                            map[key] = nextO;
                        }
                    }

                    return map;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static ObjOld EncodeUnknownType(object o, WafLibraryInvoker wafLibraryInvoker)
        {
            Log.Warning("Couldn't encode object of unknown type {Type}, falling back to ToString", o.GetType());

            var s = o.ToString() ?? string.Empty;

            return CreateNativeString(s, applyLimits: true, wafLibraryInvoker);
        }

        private static ObjOld EncodeInternal(object o, List<ObjOld>? argCache, int remainingDepth, bool applyLimits, WafLibraryInvoker wafLibraryInvoker)
        {
            var value =
                o switch
                {
                    null => CreateNativeString(string.Empty, applyLimits, wafLibraryInvoker),
                    string s => CreateNativeString(s, applyLimits, wafLibraryInvoker),
                    JValue jv => CreateNativeString(jv.Value?.ToString() ?? string.Empty, applyLimits, wafLibraryInvoker),
                    int i => CreateNativeString(i.ToString(), applyLimits, wafLibraryInvoker),
                    long i => CreateNativeString(i.ToString(), applyLimits, wafLibraryInvoker),
                    uint i => CreateNativeString(i.ToString(), applyLimits, wafLibraryInvoker),
                    ulong i => CreateNativeString(i.ToString(), applyLimits, wafLibraryInvoker),
                    bool b => CreateNativeBool(b, wafLibraryInvoker),
                    IEnumerable<KeyValuePair<string, JToken>> objDict => EncodeDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), argCache, remainingDepth, applyLimits, wafLibraryInvoker),
                    IEnumerable<KeyValuePair<string, string>> objDict => EncodeDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), argCache, remainingDepth, applyLimits, wafLibraryInvoker),
                    IEnumerable<KeyValuePair<string, List<string>>> objDict => EncodeDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), argCache, remainingDepth, applyLimits, wafLibraryInvoker),
                    IEnumerable<KeyValuePair<string, string[]>> objDict => EncodeDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), argCache, remainingDepth, applyLimits, wafLibraryInvoker),
                    IEnumerable<KeyValuePair<string, bool>> objDict => EncodeDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), argCache, remainingDepth, applyLimits, wafLibraryInvoker),
                    IEnumerable<KeyValuePair<string, object>> objDict => EncodeDictionary(objDict, argCache, remainingDepth, applyLimits, wafLibraryInvoker),
                    IList<JToken> objs => EncodeList(objs.Select(x => (object)x), argCache, remainingDepth, applyLimits, wafLibraryInvoker),
                    IList<string> objs => EncodeList(objs.Select(x => (object)x), argCache, remainingDepth, applyLimits, wafLibraryInvoker),
                    IList<object> objs => EncodeList(objs, argCache, remainingDepth, applyLimits, wafLibraryInvoker),
                    _ => EncodeUnknownType(o, wafLibraryInvoker),
                };

            argCache?.Add(value);

            return value;
        }

        private static ObjOld EncodeList(IEnumerable<object> objEnumerator, List<ObjOld>? argCache, int remainingDepth, bool applyLimits, WafLibraryInvoker wafLibraryInvoker)
        {
            var arrNat = wafLibraryInvoker.ObjectArray();

            if (applyLimits && remainingDepth-- <= 0)
            {
                Log.Warning("EncodeList: object graph too deep, truncating nesting {Items}", string.Join(", ", objEnumerator));
                return new ObjOld(arrNat);
            }

            var count = objEnumerator is IList<object> objs ? objs.Count : objEnumerator.Count();
            if (applyLimits && count > WafConstants.MaxContainerSize)
            {
                Log.Warning<int, int>("EncodeList: list too long, it will be truncated, count: {Count}, MaxMapOrArrayLength {MaxMapOrArrayLength}", count, WafConstants.MaxContainerSize);
                objEnumerator = objEnumerator.Take(WafConstants.MaxContainerSize);
            }

            foreach (var o in objEnumerator)
            {
                var value = EncodeInternal(o, argCache, remainingDepth, applyLimits, wafLibraryInvoker);
                wafLibraryInvoker.ObjectArrayAdd(arrNat, value.RawPtr);
            }

            return new ObjOld(arrNat);
        }

        private static ObjOld EncodeDictionary(IEnumerable<KeyValuePair<string, object>> objDictEnumerator, List<ObjOld>? argCache, int remainingDepth, bool applyLimits, WafLibraryInvoker wafLibraryInvoker)
        {
            var mapNat = wafLibraryInvoker.ObjectMap();

            if (applyLimits && remainingDepth-- <= 0)
            {
                Log.Warning("EncodeDictionary: object graph too deep, truncating nesting {Items}", string.Join(", ", objDictEnumerator.Select(x => $"{x.Key}, {x.Value}")));
                return new ObjOld(mapNat);
            }

            var count = objDictEnumerator is IDictionary<string, object> objDict ? objDict.Count : objDictEnumerator.Count();

            if (applyLimits && count > WafConstants.MaxContainerSize)
            {
                Log.Warning<int, int>("EncodeDictionary: list too long, it will be truncated, count: {Count}, MaxMapOrArrayLength {MaxMapOrArrayLength}", count, WafConstants.MaxContainerSize);
                objDictEnumerator = objDictEnumerator.Take(WafConstants.MaxContainerSize);
            }

            foreach (var o in objDictEnumerator)
            {
                var name = o.Key;
                if (name != null)
                {
                    var value = EncodeInternal(o.Value, argCache, remainingDepth, applyLimits, wafLibraryInvoker);
                    wafLibraryInvoker.ObjectMapAdd(mapNat, name, Convert.ToUInt64(name.Length), value.RawPtr);
                }
                else
                {
                    Log.Warning("EncodeDictionary: ignoring dictionary member with null name");
                }
            }

            return new ObjOld(mapNat);
        }

        private static ObjOld CreateNativeString(string s, bool applyLimits, WafLibraryInvoker wafLibraryInvoker)
        {
            var encodeString =
                applyLimits
                    ? TruncateLongString(s)
                    : s;
            return new ObjOld(wafLibraryInvoker.ObjectStringLength(encodeString, Convert.ToUInt64(encodeString.Length)));
        }

        private static ObjOld CreateNativeBool(bool b, WafLibraryInvoker wafLibraryInvoker)
        {
            return new ObjOld(wafLibraryInvoker.ObjectBool(b));
        }
    }
}
