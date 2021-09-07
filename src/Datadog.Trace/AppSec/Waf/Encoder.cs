// <copyright file="Encoder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Encoder
    {
        internal const int MaxStringLength = 4096;
        internal const int MaxObjectDepth = 15;
        internal const int MaxMapOrArrayLength = 1500;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Encoder));

        public static Obj Encode(object o, List<Obj> argCache)
        {
            return EncodeInternal(o, argCache, MaxObjectDepth);
        }

        public static ObjType DecodeArgsType(DDWAF_OBJ_TYPE t)
        {
            switch (t)
            {
                case DDWAF_OBJ_TYPE.DDWAF_OBJ_INVALID:
                    return ObjType.Invalid;
                case DDWAF_OBJ_TYPE.DDWAF_OBJ_SIGNED:
                    return ObjType.SignedNumber;
                case DDWAF_OBJ_TYPE.DDWAF_OBJ_UNSIGNED:
                    return ObjType.UnsignedNumber;
                case DDWAF_OBJ_TYPE.DDWAF_OBJ_STRING:
                    return ObjType.String;
                case DDWAF_OBJ_TYPE.DDWAF_OBJ_ARRAY:
                    return ObjType.Array;
                case DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP:
                    return ObjType.Map;
                default:
                    throw new Exception($"Invalid DDWAF_INPUT_TYPE {t}");
            }
        }

        public static ReturnCode DecodeReturnCode(DDWAF_RET_CODE rc)
        {
            switch (rc)
            {
                case DDWAF_RET_CODE.DDWAF_ERR_INTERNAL:
                    return ReturnCode.ErrorInternal;
                case DDWAF_RET_CODE.DDWAF_ERR_TIMEOUT:
                    return ReturnCode.ErrorTimeout;
                case DDWAF_RET_CODE.DDWAF_ERR_INVALID_ARGUMENT:
                    return ReturnCode.ErrorInvalidArgument;
                case DDWAF_RET_CODE.DDWAF_ERR_INVALID_OBJECT:
                    return ReturnCode.ErrorInvalidObject;
                case DDWAF_RET_CODE.DDWAF_GOOD:
                    return ReturnCode.Good;
                case DDWAF_RET_CODE.DDWAF_MONITOR:
                    return ReturnCode.Monitor;
                case DDWAF_RET_CODE.DDWAF_BLOCK:
                    return ReturnCode.Block;
                default:
                    throw new Exception($"Unknown return code: {rc}");
            }
        }

        private static Obj EncodeInternal(object o, List<Obj> argCache, int remainingDepth)
        {
            Log.Debug($"Encoding: {o?.GetType()}");

            var value =
                o switch
                {
                    string s => CreateNativeString(s),
                    JValue jv => CreateNativeString(jv.Value.ToString()),
                    int i => new Obj(WafNative.ObjectSigned(i)),
                    long i => new Obj(WafNative.ObjectSigned(i)),
                    uint i => new Obj(WafNative.ObjectUnsigned(i)),
                    ulong i => new Obj(WafNative.ObjectUnsigned(i)),
                    IEnumerable<KeyValuePair<string, JToken>> objDict => EncodeDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), argCache, remainingDepth),
                    IEnumerable<KeyValuePair<string, string>> objDict => EncodeDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), argCache, remainingDepth),
                    IEnumerable<KeyValuePair<string, object>> objDict => EncodeDictionary(objDict, argCache, remainingDepth),
                    IList<JToken> objs => EncodeList(objs.Select(x => (object)x), argCache, remainingDepth),
                    IList<object> objs => EncodeList(objs, argCache, remainingDepth),
                    _ => throw new Exception($"Couldn't encode: {o}, type: {o.GetType()}")
                };

            argCache.Add(value);

            return value;
        }

        private static Obj EncodeList(IEnumerable<object> objEnumerator, List<Obj> argCache, int remainingDepth)
        {
            Log.Debug($"Encoding list: {objEnumerator?.GetType()}");

            var arrNat = WafNative.ObjectArray();

            if (remainingDepth-- <= 0)
            {
                Log.Warning($"EncodeList: object graph too deep, truncating nesting " + string.Join(", ", objEnumerator));
                return new Obj(arrNat);
            }

            var count = objEnumerator is IList<object> objs ? objs.Count : objEnumerator.Count();
            if (count > MaxMapOrArrayLength)
            {
                Log.Warning($"EncodeList: list too long, it will be truncated, count: {count}, MaxMapOrArrayLength {MaxMapOrArrayLength}");
                objEnumerator = objEnumerator.Take(MaxMapOrArrayLength);
            }

            foreach (var o in objEnumerator)
            {
                var value = EncodeInternal(o, argCache, remainingDepth);
                WafNative.ObjectArrayAdd(arrNat, value.RawPtr);
            }

            return new Obj(arrNat);
        }

        private static Obj EncodeDictionary(IEnumerable<KeyValuePair<string, object>> objDictEnumerator, List<Obj> argCache, int remainingDepth)
        {
            Log.Debug($"Encoding dictionary: {objDictEnumerator?.GetType()}");

            var mapNat = WafNative.ObjectMap();

            if (remainingDepth-- <= 0)
            {
                Log.Warning($"EncodeDictionary: object graph too deep, truncating nesting " + string.Join(", ", objDictEnumerator.Select(x => $"{x.Key}, {x.Value}")));
                return new Obj(mapNat);
            }

            var count = objDictEnumerator is IDictionary<string, object> objDict ? objDict.Count : objDictEnumerator.Count();

            if (count > MaxMapOrArrayLength)
            {
                Log.Warning($"EncodeDictionary: list too long, it will be truncated, count: {count}, MaxMapOrArrayLength {MaxMapOrArrayLength}");
                objDictEnumerator = objDictEnumerator.Take(MaxMapOrArrayLength);
            }

            foreach (var o in objDictEnumerator)
            {
                var name = o.Key;
                if (name != null)
                {
                    var value = EncodeInternal(o.Value, argCache, remainingDepth);
                    WafNative.ObjectMapAdd(mapNat, name, Convert.ToUInt64(name.Length), value.RawPtr);
                }
                else
                {
                    Log.Warning($"EncodeDictionary: ignoring dictionary member with null name");
                }
            }

            return new Obj(mapNat);
        }

        private static string TrunacteLongString(string s)
        {
            if (s.Length > MaxStringLength)
            {
                return s.Substring(0, MaxStringLength);
            }

            return s;
        }

        private static Obj CreateNativeString(string s)
        {
            s = TrunacteLongString(s);
            return new Obj(WafNative.ObjectStringLength(s, Convert.ToUInt64(s.Length)));
        }

        public static string FormatArgs(object o)
        {
            // zero capcity because we don't know the size in advance
            var sb = StringBuilderCache.Acquire(0);
            FormatArgsInternal(o, sb);
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        private static void FormatArgsInternal(object o, StringBuilder sb)
        {
            _ =
                o switch
                {
                    string s => sb.Append(s),
                    int i => sb.Append(i),
                    long i => sb.Append(i),
                    uint i => sb.Append(i),
                    ulong i => sb.Append(i),
                    IList<object> objs => FormatList(objs, sb),
                    IEnumerable<KeyValuePair<string, object>> objDict => FormatDictionary(objDict, sb),
                    IEnumerable<KeyValuePair<string, string>> objDict => FormatDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), sb),
                    _ => throw new Exception($"Couldn't encode: {o}, type: {o.GetType()}")
                };
        }

        private static StringBuilder FormatDictionary(IEnumerable<KeyValuePair<string, object>> objDict, StringBuilder sb)
        {
            sb.Append("{ ");
            using var enumerator = objDict.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                sb.Append(" }");
                return sb;
            }

            sb.Append(enumerator.Current.Key);
            sb.Append(": ");
            if (enumerator.Current.Value != null)
            {
                FormatArgsInternal(enumerator.Current.Value, sb);
            }

            while (enumerator.MoveNext())
            {
                sb.Append(", ");
                sb.Append(enumerator.Current.Key);
                sb.Append(": ");
                if (enumerator.Current.Value != null)
                {
                    FormatArgsInternal(enumerator.Current.Value, sb);
                }
            }

            sb.Append(" }");
            return sb;
        }

        private static StringBuilder FormatList(IList<object> objs, StringBuilder sb)
        {
            sb.Append("[ ");
            using var enumerator = objs.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                sb.Append(" ]");
                return sb;
            }

            if (enumerator.Current != null)
            {
                FormatArgsInternal(enumerator.Current, sb);
            }

            while (enumerator.MoveNext())
            {
                if (enumerator.Current != null)
                {
                    FormatArgsInternal(enumerator.Current, sb);
                }
            }

            sb.Append(" ]");
            return sb;
        }
    }
}
