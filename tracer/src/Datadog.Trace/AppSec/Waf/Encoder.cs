// <copyright file="Encoder.cs" company="Datadog">
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
using IntPtr = System.IntPtr;

namespace Datadog.Trace.AppSec.Waf
{
    internal static class Encoder
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Encoder));
        private static readonly int ObjectStructSize = Marshal.SizeOf(typeof(DdwafObjectStruct));

        public static ObjType DecodeArgsType(DDWAF_OBJ_TYPE t)
        {
            return t switch
            {
                DDWAF_OBJ_TYPE.DDWAF_OBJ_INVALID => ObjType.Invalid,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_SIGNED => ObjType.SignedNumber,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_UNSIGNED => ObjType.UnsignedNumber,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_STRING => ObjType.String,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_ARRAY => ObjType.Array,
                DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP => ObjType.Map,
                _ => throw new Exception($"Invalid DDWAF_INPUT_TYPE {t}")
            };
        }

        public static ReturnCode DecodeReturnCode(DDWAF_RET_CODE rc) => rc switch
        {
            DDWAF_RET_CODE.DDWAF_ERR_INTERNAL => ReturnCode.ErrorInternal,
            DDWAF_RET_CODE.DDWAF_ERR_INVALID_ARGUMENT => ReturnCode.ErrorInvalidArgument,
            DDWAF_RET_CODE.DDWAF_ERR_INVALID_OBJECT => ReturnCode.ErrorInvalidObject,
            DDWAF_RET_CODE.DDWAF_OK => ReturnCode.Ok,
            DDWAF_RET_CODE.DDWAF_MATCH => ReturnCode.Match,
            DDWAF_RET_CODE.DDWAF_BLOCK => ReturnCode.Block,
            _ => throw new Exception($"Unknown return code: {rc}")
        };

        private static string TruncateLongString(string s) => s.Length > WafConstants.MaxStringLength ? s.Substring(0, WafConstants.MaxStringLength) : s;

        public static Obj Encode(object o, WafLibraryInvoker wafLibraryInvoker, List<Obj>? argCache = null, bool applySafetyLimits = true) => EncodeInternal(o, argCache, WafConstants.MaxContainerDepth, applySafetyLimits, wafLibraryInvoker);

        public static object Decode(Obj o) => InnerDecode(o.InnerStruct);

        public static object InnerDecode(DdwafObjectStruct o)
        {
            switch (DecodeArgsType(o.Type))
            {
                case ObjType.Invalid:
                    return new object();
                case ObjType.SignedNumber:
                    return o.IntValue;
                case ObjType.UnsignedNumber:
                    return o.UintValue;
                case ObjType.String:
                    return Marshal.PtrToStringAnsi(o.Array) ?? string.Empty;
                case ObjType.Array:
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
                case ObjType.Map:
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

        private static Obj EncodeUnknownType(object o, WafLibraryInvoker wafLibraryInvoker)
        {
            Log.Warning("Couldn't encode object of unknown type {Type}, falling back to ToString", o.GetType());

            var s = o.ToString() ?? string.Empty;

            return CreateNativeString(s, applyLimits: true, wafLibraryInvoker);
        }

        private static Obj EncodeInternal(object o, List<Obj>? argCache, int remainingDepth, bool applyLimits, WafLibraryInvoker wafLibraryInvoker)
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
                    IList<bool> objs => EncodeList(objs.Select(x => (object)x), argCache, remainingDepth, applyLimits, wafLibraryInvoker),
                    IList<object> objs => EncodeList(objs, argCache, remainingDepth, applyLimits, wafLibraryInvoker),
                    _ => EncodeUnknownType(o, wafLibraryInvoker),
                };

            argCache?.Add(value);

            return value;
        }

        public static DdwafObjectStruct Encode2(object? o, List<GCHandle> argToFree, int remainingDepth = WafConstants.MaxContainerDepth, string? key = null, bool applySafetyLimits = false)
        {
            DdwafObjectStruct ProcessKeyValuePairs(System.Collections.IEnumerable enumerableDic, Func<object, string?> getKey, Func<object, object?> getValue)
            {
                var ddWafObjectMap = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP };
                if (!string.IsNullOrEmpty(key))
                {
                    FillParamName(ref ddWafObjectMap, key!);
                }

                if (applySafetyLimits && remainingDepth-- <= 0)
                {
                    IEnumerable<string> GetItemsAsString()
                    {
                        foreach (var x in enumerableDic)
                        {
                            yield return $"{getKey(x!)}, {getValue(x!)}";
                        }
                    }

                    Log.Warning("EncodeDictionary: object graph too deep, truncating nesting {Items}", string.Join(", ", GetItemsAsString()));
                    return ddWafObjectMap;
                }

                var children = new List<DdwafObjectStruct>();
                foreach (var keyValue in enumerableDic)
                {
                    var key = getKey(keyValue!);
                    if (string.IsNullOrEmpty(key))
                    {
                        Log.Warning("EncodeDictionary: ignoring dictionary member with null name");
                        continue;
                    }

                    var result = Encode2(getValue(keyValue!), argToFree, applySafetyLimits: applySafetyLimits, key: key, remainingDepth: remainingDepth);
                    children.Add(result);
                    if (children.Count == WafConstants.MaxContainerSize)
                    {
                        Log.Warning<int>("EncodeList: list too long, it will be truncated, MaxMapOrArrayLength {MaxMapOrArrayLength}", WafConstants.MaxContainerSize);
                        break;
                    }
                }

                AddToArray(ref ddWafObjectMap, children.ToArray());

                return ddWafObjectMap;
            }

            IntPtr ConvertToUtf8(string s)
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                var pinnedArray = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                argToFree.Add(pinnedArray);
                var pointer = Marshal.UnsafeAddrOfPinnedArrayElement(bytes, 0);
                return pointer;
            }

            void AddToArray(ref DdwafObjectStruct map, params DdwafObjectStruct[] children)
            {
                if (children.Length == 0)
                {
                    return;
                }

                var structArray = children;
                var gcHandle = GCHandle.Alloc(structArray, GCHandleType.Pinned);
                argToFree.Add(gcHandle);
                var bufferPtr = Marshal.UnsafeAddrOfPinnedArrayElement(structArray, 0);
                map.Array = bufferPtr;
                map.NbEntries = (ulong)structArray.Length;
            }

            void FillParamName(ref DdwafObjectStruct ddwafObjectStruct, string paramName)
            {
                ddwafObjectStruct.ParameterName = ConvertToUtf8(paramName);
                ddwafObjectStruct.ParameterNameLength = (ulong)paramName.Length;
            }

            DdwafObjectStruct GetStringObject(string? keyForObject, string value)
            {
                var ddWafObject = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_STRING, Array = ConvertToUtf8(value), NbEntries = (ulong)value.Length };
                if (keyForObject != null)
                {
                    FillParamName(ref ddWafObject, keyForObject);
                }

                return ddWafObject;
            }

            DdwafObjectStruct ddwafObjectStruct;
            switch (o)
            {
                case string or int or uint or long:
                case ulong or JValue:
                case null:
                    var value = o?.ToString() ?? string.Empty;
                    var encodeString = applySafetyLimits ? TruncateLongString(value) : value;
                    ddwafObjectStruct = GetStringObject(key, encodeString);
                    break;

                case bool b:
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_BOOL, Boolean = b ? (byte)1 : (byte)0 };
                    if (key != null)
                    {
                        FillParamName(ref ddwafObjectStruct, key);
                    }

                    break;

                case IEnumerable<KeyValuePair<string, object>> objDict:
                    ddwafObjectStruct = ProcessKeyValuePairs(objDict, obj => ((KeyValuePair<string, object>)obj).Key, obj => ((KeyValuePair<string, object>)obj).Value);
                    break;

                case IEnumerable<KeyValuePair<string, string>> objDict:
                    ddwafObjectStruct = ProcessKeyValuePairs(objDict, obj => ((KeyValuePair<string, string>)obj).Key, obj => ((KeyValuePair<string, string>)obj).Value);
                    break;

                case IEnumerable<KeyValuePair<string, JToken>> objDict:
                    ddwafObjectStruct = ProcessKeyValuePairs(objDict, obj => ((JProperty)obj).Name, obj => ((JProperty)obj).Value);
                    break;

                case IEnumerable<KeyValuePair<string, string[]>> objDict:
                    ddwafObjectStruct = ProcessKeyValuePairs(objDict, obj => ((KeyValuePair<string, string[]>)obj).Key, obj => ((KeyValuePair<string, string[]>)obj).Value);
                    break;

                case IEnumerable<KeyValuePair<string, List<string>>> objDict:
                    ddwafObjectStruct = ProcessKeyValuePairs(objDict, obj => ((KeyValuePair<string, List<string>>)obj).Key, obj => ((KeyValuePair<string, List<string>>)obj).Value);
                    break;

                case System.Collections.IEnumerable list:
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_ARRAY };
                    if (!string.IsNullOrEmpty(key))
                    {
                        FillParamName(ref ddwafObjectStruct, key!);
                    }

                    if (applySafetyLimits && remainingDepth-- <= 0)
                    {
                        Log.Warning("EncodeList: object graph too deep, truncating nesting {Items}", string.Join(", ", list));
                        break;
                    }

                    var children = new List<DdwafObjectStruct>();
                    foreach (var val in list)
                    {
                        if (children.Count == WafConstants.MaxContainerSize)
                        {
                            Log.Warning<int>("EncodeList: list too long, it will be truncated, MaxMapOrArrayLength {MaxMapOrArrayLength}", WafConstants.MaxContainerSize);
                            break;
                        }

                        var result = Encode2(val, argToFree, applySafetyLimits: applySafetyLimits, remainingDepth: remainingDepth);
                        children.Add(result);
                    }

                    AddToArray(ref ddwafObjectStruct, children.ToArray());
                    break;

                default:
                    Log.Warning("Couldn't encode object of unknown type {Type}, falling back to ToString", o.GetType());
                    ddwafObjectStruct = GetStringObject(key, string.Empty);
                    break;
            }

            return ddwafObjectStruct;
        }

        public static unsafe DdwafObjectStruct Encode3(object? o, WafLibraryInvoker wafLibraryInvoker, List<GCHandle> gcHandles, int remainingDepth = WafConstants.MaxContainerDepth, string? key = null, bool applySafetyLimits = false, int stackLeftInBytes = 1024)
        {
            DdwafObjectStruct ProcessKeyValuePairs(System.Collections.IEnumerable objDict, Func<object, string> getKey, Func<object, object> getValue, int totalCount)
            {
                var ddWafObjectMap = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP };
                if (!string.IsNullOrEmpty(key))
                {
                    FillParamName(ref ddWafObjectMap, key!);
                }

                if (applySafetyLimits && remainingDepth-- <= 0)
                {
                    // Log.Warning("EncodeDictionary: object graph too deep, truncating nesting {Items}", string.Join(", ", objDict.GetEnumerator().Select(x => $"{getKey(x)}, {getValue(x)}")));
                }
                else
                {
                    var sizeNeeded = sizeof(DdwafObjectStruct) * totalCount;
                    var leftInBytes = stackLeftInBytes - sizeNeeded;
                    if (leftInBytes > 0)
                    {
                        DdwafObjectStruct* st = stackalloc DdwafObjectStruct[totalCount];
                        foreach (var keyValue in objDict)
                        {
                            var result = Encode3(getValue(keyValue!), wafLibraryInvoker, gcHandles, remainingDepth, getKey(keyValue!), stackLeftInBytes: leftInBytes);
                            st[(int)ddWafObjectMap.NbEntries++] = result;
                        }

                        ddWafObjectMap.Array = (IntPtr)st;
                    }
                    else
                    {
                        var ptr = (DdwafObjectStruct*)Marshal.AllocHGlobal(sizeNeeded);
                        foreach (var keyValue in objDict)
                        {
                            var result = Encode3(getValue(keyValue!), wafLibraryInvoker, gcHandles, remainingDepth, getKey(keyValue!), stackLeftInBytes: leftInBytes);
                            ptr[ddWafObjectMap.NbEntries++] = result;
                            ddWafObjectMap.Array = (IntPtr)ptr;
                        }
                    }
                }

                return ddWafObjectMap;
            }

            void FillParamName(ref DdwafObjectStruct ddwafObjectStruct, string paramName)
            {
                var bytes = Encoding.UTF8.GetBytes(paramName);
                fixed (byte* ptr = bytes)
                {
                    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    gcHandles.Add(handle);
                    ddwafObjectStruct.ParameterName = (IntPtr)ptr;
                }

                ddwafObjectStruct.ParameterNameLength = (ulong)paramName.Length;
            }

            DdwafObjectStruct GetStringObject(string? keyForObject, string value)
            {
                var ddWafObject = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_STRING, NbEntries = (ulong)value.Length };
                var bytes = Encoding.UTF8.GetBytes(value);
                fixed (byte* ptrStringValue = &bytes[0])
                {
                    GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    ddWafObject.StringValue = (IntPtr)ptrStringValue;
                }

                if (keyForObject != null)
                {
                    FillParamName(ref ddWafObject, keyForObject);
                }

                return ddWafObject;
            }

            DdwafObjectStruct result;
            switch (o)
            {
                case string or int or uint or long:
                case ulong or JValue:
                case null:
                    var value = o?.ToString() ?? string.Empty;
                    var encodeString = applySafetyLimits ? TruncateLongString(value) : value;
                    result = GetStringObject(key, encodeString);
                    break;

                case bool b:
                    var ddWafObject = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_BOOL, Boolean = b ? (byte)1 : (byte)0 };
                    if (key != null)
                    {
                        FillParamName(ref ddWafObject, key);
                    }

                    result = ddWafObject;
                    break;

                case IEnumerable<KeyValuePair<string, object>> objDict:
                    result = ProcessKeyValuePairs(objDict, obj => ((KeyValuePair<string, object>)obj).Key, obj => ((KeyValuePair<string, object>)obj).Value, objDict.Count());
                    break;

                case IEnumerable<KeyValuePair<string, string>> objDict:
                    result = ProcessKeyValuePairs(objDict, obj => ((KeyValuePair<string, string>)obj).Key, obj => ((KeyValuePair<string, string>)obj).Value, objDict.Count());
                    break;

                case IEnumerable<KeyValuePair<string, JToken>> objDict:
                    result = ProcessKeyValuePairs(objDict, obj => ((KeyValuePair<string, JToken>)obj).Key, obj => ((KeyValuePair<string, JToken>)obj).Value, objDict.Count());
                    break;

                case IEnumerable<KeyValuePair<string, string[]>> objDict:
                    result = ProcessKeyValuePairs(objDict, obj => ((KeyValuePair<string, string[]>)obj).Key, obj => ((KeyValuePair<string, string[]>)obj).Value, objDict.Count());
                    break;

                case IEnumerable<KeyValuePair<string, List<string>>> objDict:
                    result = ProcessKeyValuePairs(objDict, obj => ((KeyValuePair<string, List<string>>)obj).Key, obj => ((KeyValuePair<string, List<string>>)obj).Value, objDict.Count());
                    break;

                case System.Collections.IEnumerable list:
                    result = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_ARRAY };
                    if (!string.IsNullOrEmpty(key))
                    {
                        FillParamName(ref result, key!);
                    }

                    if (applySafetyLimits && remainingDepth-- <= 0)
                    {
                        Log.Warning("EncodeList: object graph too deep, truncating nesting {Items}", string.Join(", ", list));
                        break;
                    }

                    var tempList = new List<DdwafObjectStruct>();
                    foreach (var element in list)
                    {
                        var ddWafResult = Encode3(element, wafLibraryInvoker, gcHandles, remainingDepth);
                        tempList.Add(ddWafResult);
                    }

                    var ptr = Marshal.AllocHGlobal(tempList.Count * sizeof(DdwafObjectStruct));
                    foreach (var element in tempList)
                    {
                        ((DdwafObjectStruct*)ptr)[result.NbEntries++] = element;
                    }

                    result.Array = ptr;
                    break;

                default:
                    Log.Warning("Couldn't encode object of unknown type {Type}, falling back to ToString", o.GetType());
                    result = GetStringObject(key, string.Empty);
                    break;
            }

            return result;
        }

        private static Obj EncodeList(IEnumerable<object> objEnumerator, List<Obj>? argCache, int remainingDepth, bool applyLimits, WafLibraryInvoker wafLibraryInvoker)
        {
            var arrNat = wafLibraryInvoker.ObjectArray();

            if (applyLimits && remainingDepth-- <= 0)
            {
                Log.Warning("EncodeList: object graph too deep, truncating nesting {Items}", string.Join(", ", objEnumerator));
                return new Obj(arrNat);
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

            return new Obj(arrNat);
        }

        private static Obj EncodeDictionary(IEnumerable<KeyValuePair<string, object>> objDictEnumerator, List<Obj>? argCache, int remainingDepth, bool applyLimits, WafLibraryInvoker wafLibraryInvoker)
        {
            var mapNat = wafLibraryInvoker.ObjectMap();

            if (applyLimits && remainingDepth-- <= 0)
            {
                Log.Warning("EncodeDictionary: object graph too deep, truncating nesting {Items}", string.Join(", ", objDictEnumerator.Select(x => $"{x.Key}, {x.Value}")));
                return new Obj(mapNat);
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

            return new Obj(mapNat);
        }

        private static Obj CreateNativeString(string s, bool applyLimits, WafLibraryInvoker wafLibraryInvoker)
        {
            var encodeString =
                applyLimits
                    ? TruncateLongString(s)
                    : s;
            return new Obj(wafLibraryInvoker.ObjectStringLength(encodeString, Convert.ToUInt64(encodeString.Length)));
        }

        private static Obj CreateNativeBool(bool b, WafLibraryInvoker wafLibraryInvoker)
        {
            return new Obj(wafLibraryInvoker.ObjectBool(b));
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
                    // dont remove IEnumerable<KeyValuePair<string, string[]>>, it is used for logging cookies which are this type in debug mode
                    IEnumerable<KeyValuePair<string, string[]>> objDict => FormatDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), sb),
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
