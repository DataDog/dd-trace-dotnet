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

namespace Datadog.Trace.AppSec.Waf
{
    internal class Encoder
    {
        internal const int MaxStringLength = 4096;
        internal const int MaxObjectDepth = 10;
        internal const int MaxMapOrArrayLength = 150;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Encoder));

        public static Args Encode(object o)
        {
            return EncodeInternal(o, MaxObjectDepth);
        }

        public static ArgsType DecodeArgsType(PW_INPUT_TYPE t)
        {
            switch (t)
            {
                case PW_INPUT_TYPE.PWI_INVALID:
                    return ArgsType.Invalid;
                case PW_INPUT_TYPE.PWI_SIGNED_NUMBER:
                    return ArgsType.SignedNumber;
                case PW_INPUT_TYPE.PWI_UNSIGNED_NUMBER:
                    return ArgsType.UnsignedNumber;
                case PW_INPUT_TYPE.PWI_STRING:
                    return ArgsType.String;
                case PW_INPUT_TYPE.PWI_ARRAY:
                    return ArgsType.Array;
                case PW_INPUT_TYPE.PWI_MAP:
                    return ArgsType.Map;
                default:
                    throw new Exception($"Invalid PW_INPUT_TYPE {t}");
            }
        }

        public static ReturnCode DecodeReturnCode(PW_RET_CODE rc)
        {
            switch (rc)
            {
                case PW_RET_CODE.PW_ERR_INTERNAL:
                    return ReturnCode.ErrorInternal;
                case PW_RET_CODE.PW_ERR_TIMEOUT:
                    return ReturnCode.ErrorTimeout;
                case PW_RET_CODE.PW_ERR_INVALID_CALL:
                    return ReturnCode.ErrorInvalidCall;
                case PW_RET_CODE.PW_ERR_INVALID_RULE:
                    return ReturnCode.ErrorInvalidRule;
                case PW_RET_CODE.PW_ERR_INVALID_FLOW:
                    return ReturnCode.ErrorInvalidFlow;
                case PW_RET_CODE.PW_ERR_NORULE:
                    return ReturnCode.ErrorNorule;
                case PW_RET_CODE.PW_GOOD:
                    return ReturnCode.Good;
                case PW_RET_CODE.PW_MONITOR:
                    return ReturnCode.Monitor;
                case PW_RET_CODE.PW_BLOCK:
                    return ReturnCode.Block;
                default:
                    throw new Exception($"Unknown return code: {rc}");
            }
        }

        private static Args EncodeInternal(object o, int remainingDepth)
        {
            var value =
                o switch
                {
                    string s => CreateNativeString(s),
                    int i => new Args(Native.pw_createInt(i)),
                    long i => new Args(Native.pw_createInt(i)),
                    uint i => new Args(Native.pw_createUint(i)),
                    ulong i => new Args(Native.pw_createUint(i)),
                    IList<object> objs => EncodeList(objs, remainingDepth),
                    IEnumerable<KeyValuePair<string, object>> objDict => EncodeDictionary(objDict, remainingDepth),
                    IEnumerable<KeyValuePair<string, string>> objDict => EncodeDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), remainingDepth),
                    _ => throw new Exception($"Couldn't encode: {o}, type: {o.GetType()}")
                };
            return value;
        }

        private static Args EncodeList(IList<object> objs, int remainingDepth)
        {
            var arrNat = Native.pw_createArray();

            if (remainingDepth-- <= 0)
            {
                Log.Warning($"EncodeList: object graph too deep, truncating nesting");
                return new Args(arrNat);
            }

            IEnumerable<object> objEnumerator = objs;
            if (objs.Count > MaxMapOrArrayLength)
            {
                Log.Warning($"EncodeList: list too long, it will be truncated, objs.Count: {objs.Count}, MaxMapOrArrayLength {MaxMapOrArrayLength}");
                objEnumerator = objs.Take(MaxMapOrArrayLength);
            }

            foreach (var o in objEnumerator)
            {
                var value = EncodeInternal(o, remainingDepth);
                Native.pw_addArray(ref arrNat, value.RawArgs);
            }

            return new Args(arrNat);
        }

        private static Args EncodeDictionary(IEnumerable<KeyValuePair<string, object>> objDictEnumerator, int remainingDepth)
        {
            var mapNat = Native.pw_createMap();

            if (remainingDepth-- <= 0)
            {
                Log.Warning($"EncodeDictionary: object graph too deep, truncating nesting");
                return new Args(mapNat);
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
                    var value = EncodeInternal(o.Value, remainingDepth);
                    Native.pw_addMap(ref mapNat, name, Convert.ToUInt64(name.Length), value.RawArgs);
                }
                else
                {
                    Log.Warning($"EncodeDictionary: ignoring dictionary member with null name");
                }
            }

            return new Args(mapNat);
        }

        private static string TrunacteLongString(string s)
        {
            if (s.Length > MaxStringLength)
            {
                return s.Substring(0, MaxStringLength);
            }

            return s;
        }

        private static Args CreateNativeString(string s)
        {
            s = TrunacteLongString(s);
            return new Args(Native.pw_createStringWithLength(s, Convert.ToUInt64(s.Length)));
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
