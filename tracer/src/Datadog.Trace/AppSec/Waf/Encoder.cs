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
        private const int MaxStringLength = 4096;
        private const int MaxObjectDepth = 15;
        private const int MaxMapOrArrayLength = 1500;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Encoder));
        private readonly WafNative _wafNative;

        public Encoder(WafNative wafNative) => _wafNative = wafNative;

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

        public static ReturnCode DecodeReturnCode(DDWAF_RET_CODE rc) => rc switch
        {
            DDWAF_RET_CODE.DDWAF_ERR_INTERNAL => ReturnCode.ErrorInternal,
            DDWAF_RET_CODE.DDWAF_ERR_INVALID_ARGUMENT => ReturnCode.ErrorInvalidArgument,
            DDWAF_RET_CODE.DDWAF_ERR_INVALID_OBJECT => ReturnCode.ErrorInvalidObject,
            DDWAF_RET_CODE.DDWAF_GOOD => ReturnCode.Good,
            DDWAF_RET_CODE.DDWAF_MONITOR => ReturnCode.Monitor,
            DDWAF_RET_CODE.DDWAF_BLOCK => ReturnCode.Block,
            _ => throw new Exception($"Unknown return code: {rc}")
        };

        private static string TruncateLongString(string s) => s.Length > MaxStringLength ? s.Substring(0, MaxStringLength) : s;

        public Obj Encode(object o, List<Obj> argCache) => EncodeInternal(o, argCache, MaxObjectDepth);

        private Obj EncodeInternal(object o, List<Obj> argCache, int remainingDepth)
        {
            Log.Debug("Encoding type: {Type}", o?.GetType());

            var value =
                o switch
                {
                    null => CreateNativeString(string.Empty),
                    string s => CreateNativeString(s),
                    JValue jv => CreateNativeString(jv.Value?.ToString() ?? string.Empty),
                    int i => new Obj(_wafNative.ObjectSigned(i)),
                    long i => new Obj(_wafNative.ObjectSigned(i)),
                    uint i => new Obj(_wafNative.ObjectUnsigned(i)),
                    ulong i => new Obj(_wafNative.ObjectUnsigned(i)),
                    IEnumerable<KeyValuePair<string, JToken>> objDict => EncodeDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), argCache, remainingDepth),
                    IEnumerable<KeyValuePair<string, string>> objDict => EncodeDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), argCache, remainingDepth),
                    IEnumerable<KeyValuePair<string, List<string>>> objDict => EncodeDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), argCache, remainingDepth),
                    IEnumerable<KeyValuePair<string, string[]>> objDict => EncodeDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), argCache, remainingDepth),
                    IEnumerable<KeyValuePair<string, object>> objDict => EncodeDictionary(objDict, argCache, remainingDepth),
                    IList<JToken> objs => EncodeList(objs.Select(x => (object)x), argCache, remainingDepth),
                    IList<string> objs => EncodeList(objs.Select(x => (object)x), argCache, remainingDepth),
                    IList<object> objs => EncodeList(objs, argCache, remainingDepth),
                    _ => throw new Exception($"Couldn't encode type: {o?.GetType()}")
                };

            argCache.Add(value);

            return value;
        }

        private Obj EncodeList(IEnumerable<object> objEnumerator, List<Obj> argCache, int remainingDepth)
        {
            Log.Debug("Encoding list: {Type}", objEnumerator?.GetType());

            var arrNat = _wafNative.ObjectArray();

            if (remainingDepth-- <= 0)
            {
                Log.Warning("EncodeList: object graph too deep, truncating nesting {Items}", string.Join(", ", objEnumerator));
                return new Obj(arrNat);
            }

            var count = objEnumerator is IList<object> objs ? objs.Count : objEnumerator.Count();
            if (count > MaxMapOrArrayLength)
            {
                Log.Warning<int, int>("EncodeList: list too long, it will be truncated, count: {Count}, MaxMapOrArrayLength {MaxMapOrArrayLength}", count, MaxMapOrArrayLength);
                objEnumerator = objEnumerator.Take(MaxMapOrArrayLength);
            }

            foreach (var o in objEnumerator)
            {
                var value = EncodeInternal(o, argCache, remainingDepth);
                _wafNative.ObjectArrayAdd(arrNat, value.RawPtr);
            }

            return new Obj(arrNat);
        }

        private Obj EncodeDictionary(IEnumerable<KeyValuePair<string, object>> objDictEnumerator, List<Obj> argCache, int remainingDepth)
        {
            Log.Debug("Encoding dictionary: {Type}", objDictEnumerator?.GetType());

            var mapNat = _wafNative.ObjectMap();

            if (remainingDepth-- <= 0)
            {
                Log.Warning("EncodeDictionary: object graph too deep, truncating nesting {Items}", string.Join(", ", objDictEnumerator.Select(x => $"{x.Key}, {x.Value}")));
                return new Obj(mapNat);
            }

            var count = objDictEnumerator is IDictionary<string, object> objDict ? objDict.Count : objDictEnumerator.Count();

            if (count > MaxMapOrArrayLength)
            {
                Log.Warning<int, int>("EncodeDictionary: list too long, it will be truncated, count: {Count}, MaxMapOrArrayLength {MaxMapOrArrayLength}", count, MaxMapOrArrayLength);
                objDictEnumerator = objDictEnumerator.Take(MaxMapOrArrayLength);
            }

            foreach (var o in objDictEnumerator)
            {
                var name = o.Key;
                if (name != null)
                {
                    var value = EncodeInternal(o.Value, argCache, remainingDepth);
                    _wafNative.ObjectMapAdd(mapNat, name, Convert.ToUInt64(name.Length), value.RawPtr);
                }
                else
                {
                    Log.Warning("EncodeDictionary: ignoring dictionary member with null name");
                }
            }

            return new Obj(mapNat);
        }

        private Obj CreateNativeString(string s)
        {
            s = TruncateLongString(s);
            return new Obj(_wafNative.ObjectStringLength(s, Convert.ToUInt64(s.Length)));
        }

        public static string FormatArgs(object o)
        {
            // zero capacity because we don't know the size in advance
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
                    IEnumerable<KeyValuePair<string, JToken>> objDict => FormatDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), sb),
                    IEnumerable<KeyValuePair<string, string>> objDict => FormatDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), sb),
                    IEnumerable<KeyValuePair<string, List<string>>> objDict => FormatDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), sb),
                    IEnumerable<KeyValuePair<string, object>> objDict => FormatDictionary(objDict, sb),
                    IList<JToken> objs => FormatList(objs.Select(x => (object)x), sb),
                    IList<string> objs => FormatList(objs.Select(x => (object)x), sb),
                    IList<object> objs => FormatList(objs, sb),
                    _ => sb.Append($"Error: couldn't format type: {o?.GetType()}")
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

        private static StringBuilder FormatList(IEnumerable<object> objs, StringBuilder sb)
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
