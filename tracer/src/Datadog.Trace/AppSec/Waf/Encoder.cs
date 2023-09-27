// <copyright file="Encoder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Waf
{
    internal static class Encoder
    {
        private const int MaxBytesForMaxStringLength = (WafConstants.MaxStringLength * 4) + 1;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Encoder));
        private static readonly int ObjectStructSize = Marshal.SizeOf(typeof(DdwafObjectStruct));

        [ThreadStatic]
        private static UnmanagedMemoryPool? _pool;

        internal static UnmanagedMemoryPool Pool
        {
            get
            {
                if (_pool is { IsDisposed: false })
                {
                    return _pool;
                }

                var instance = new UnmanagedMemoryPool(MaxBytesForMaxStringLength, 1000);
                _pool = instance;
                return instance;
            }
        }

        public static EncodeResult Encode<TInstance>(TInstance? o, int remainingDepth = WafConstants.MaxContainerDepth, string? key = null, bool applySafetyLimits = true)
        {
            var lstPointers = new List<IntPtr>();
            var pool = Pool;
            return new EncodeResult(lstPointers, pool, Encode(o, lstPointers, remainingDepth, key, applySafetyLimits, pool: pool));
        }

        public static unsafe DdwafObjectStruct Encode<TInstance>(TInstance? o, List<IntPtr> argToFree, int remainingDepth = WafConstants.MaxContainerDepth, string? key = null, bool applySafetyLimits = true, UnmanagedMemoryPool? pool = null)
        {
            pool ??= Pool;

            DdwafObjectStruct ProcessKeyValuePairs<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> enumerableDic, int count, delegate*<KeyValuePair<TKey, TValue>, string?> getKey, delegate*<KeyValuePair<TKey, TValue>, object?> getValue)
                where TKey : notnull
            {
                var ddWafObjectMap = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP };
                if (!string.IsNullOrEmpty(key))
                {
                    var convertToUtf8 = ConvertToUtf8(key!, false);
                    ddWafObjectMap.ParameterName = convertToUtf8.Item1;
                    ddWafObjectMap.ParameterNameLength = (ulong)key!.Length;
                }

                if (applySafetyLimits)
                {
                    if (remainingDepth-- <= 0)
                    {
                        string GetItemsAsString()
                        {
                            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
                            foreach (var x in enumerableDic)
                            {
                                sb.Append($"{getKey(x)}, {getValue(x)}, ");
                            }

                            if (sb.Length > 0)
                            {
                                sb.Remove(sb.Length - 2, 2);
                            }

                            return StringBuilderCache.GetStringAndRelease(sb);
                        }

                        Log.Warning("EncodeDictionary: object graph too deep, truncating nesting {Items}", GetItemsAsString());
                        return ddWafObjectMap;
                    }

                    if (count > WafConstants.MaxContainerSize)
                    {
                        Log.Warning<int>("EncodeList: list too long, it will be truncated, MaxMapOrArrayLength {MaxMapOrArrayLength}", WafConstants.MaxContainerSize);
                    }
                }

                var childrenCount = !applySafetyLimits || count < WafConstants.MaxContainerSize ? count : WafConstants.MaxContainerSize;
                var childrenFromPool = ObjectStructSize * childrenCount < MaxBytesForMaxStringLength;
                var childrenData = childrenFromPool ? pool.Rent() : Marshal.AllocCoTaskMem(ObjectStructSize * childrenCount);

                if (enumerableDic is IDictionary)
                {
                    var typeKVP = typeof(KeyValuePair<TKey, TValue>);
                    if (typeKVP == typeof(KeyValuePair<string, string>))
                    {
                        EnumerateItems<string, string>();
                    }
                    else if (typeKVP == typeof(KeyValuePair<string, object>))
                    {
                        EnumerateItems<string, object>();
                    }
                    else if (typeKVP == typeof(KeyValuePair<string, string[]>))
                    {
                        EnumerateItems<string, string[]>();
                    }
                    else if (typeKVP == typeof(KeyValuePair<string, List<string>>))
                    {
                        EnumerateItems<string, List<string>>();
                    }
                    else if (typeKVP == typeof(KeyValuePair<string, JToken>))
                    {
                        EnumerateItems<string, JToken>();
                    }
                    else
                    {
                        EnumerateItems<TKey, TValue>();
                    }

                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    void EnumerateItems<TKeySource, TValueSource>()
                        where TKeySource : notnull
                    {
                        var itemData = childrenData;
                        foreach (var originalKeyValue in (Dictionary<TKeySource, TValueSource>)enumerableDic)
                        {
                            var keyValue = UnsafeHelper.As<KeyValuePair<TKeySource, TValueSource>, KeyValuePair<TKey, TValue>>(originalKeyValue);
                            var key = getKey(keyValue!);
                            if (string.IsNullOrEmpty(key))
                            {
                                childrenCount--;
                                Log.Warning("EncodeDictionary: ignoring dictionary member with null name");
                                continue;
                            }

                            *(DdwafObjectStruct*)itemData = Encode(getValue(keyValue!), argToFree, applySafetyLimits: applySafetyLimits, key: key, remainingDepth: remainingDepth, pool: pool);
                            itemData += ObjectStructSize;
                        }
                    }
                }
                else
                {
                    var itemData = childrenData;
                    foreach (var keyValue in enumerableDic)
                    {
                        var key = getKey(keyValue);
                        if (string.IsNullOrEmpty(key))
                        {
                            childrenCount--;
                            Log.Warning("EncodeDictionary: ignoring dictionary member with null name");
                            continue;
                        }

                        *(DdwafObjectStruct*)itemData = Encode(getValue(keyValue), argToFree, applySafetyLimits: applySafetyLimits, key: key, remainingDepth: remainingDepth, pool: pool);
                        itemData += ObjectStructSize;
                    }
                }

                ddWafObjectMap.Array = childrenData;
                ddWafObjectMap.NbEntries = (ulong)childrenCount;
                argToFree.Add(childrenData);
                return ddWafObjectMap;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            Tuple<IntPtr, int> ConvertToUtf8(string s, bool applySafety)
            {
                IntPtr unmanagedMemory;
                int writtenBytes;
                var length = s.Length;
                if (applySafety || length <= WafConstants.MaxStringLength)
                {
                    length = Math.Min(length, WafConstants.MaxStringLength);
                    unmanagedMemory = pool.Rent();
                    fixed (char* chrPtr = s)
                    {
                        writtenBytes = Encoding.UTF8.GetBytes(chrPtr, length, (byte*)unmanagedMemory, MaxBytesForMaxStringLength);
                    }
                }
                else
                {
                    var bytesCount = Encoding.UTF8.GetMaxByteCount(length) + 1;
                    unmanagedMemory = Marshal.AllocCoTaskMem(bytesCount);
                    fixed (char* chrPtr = s)
                    {
                        writtenBytes = Encoding.UTF8.GetBytes(chrPtr, length, (byte*)unmanagedMemory, bytesCount);
                    }
                }

                Marshal.WriteByte(unmanagedMemory, writtenBytes, (byte)'\0');
                argToFree.Add(unmanagedMemory);
                return new Tuple<IntPtr, int>(unmanagedMemory, length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            DdwafObjectStruct GetStringObject(string value)
            {
                var convertToUtf8 = ConvertToUtf8(value, applySafetyLimits);
                var ddWafObject = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_STRING, Array = convertToUtf8.Item1, NbEntries = (ulong)convertToUtf8.Item2 };
                return ddWafObject;
            }

            DdwafObjectStruct ddwafObjectStruct;

            switch (o)
            {
                case string str:
                {
                    ddwafObjectStruct = GetStringObject(str);
                    break;
                }

                case JValue:
                {
                    ddwafObjectStruct = GetStringObject(o?.ToString() ?? string.Empty);
                    break;
                }

                case null:
                {
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_NULL };
                    break;
                }

                case ulong u:
                {
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_UNSIGNED, UintValue = u };
                    break;
                }

                case uint u:
                {
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_UNSIGNED, UintValue = u };
                    break;
                }

                case int i:
                {
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_SIGNED, IntValue = i };
                    break;
                }

                case long u:
                {
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_SIGNED, IntValue = u };
                    break;
                }

                case decimal d:
                {
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_DOUBLE, DoubleValue = (double)d };
                    break;
                }

                case double d:
                {
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_DOUBLE, DoubleValue = d };
                    break;
                }

                case float d:
                {
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_DOUBLE, DoubleValue = d };
                    break;
                }

                case bool b:
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_BOOL, ByteValue = b ? (byte)1 : (byte)0 };
                    break;

                case IEnumerable<KeyValuePair<string, object>> objDict:
                {
                    var count = objDict is ICollection<KeyValuePair<string, object>> dct ? dct.Count : objDict.Count();
                    ddwafObjectStruct = ProcessKeyValuePairs(objDict, count, &GetKey1, &GetValue1);
                    static string GetKey1(KeyValuePair<string, object> item) => item.Key;
                    static object GetValue1(KeyValuePair<string, object> item) => item.Value;
                    break;
                }

                case IEnumerable<KeyValuePair<string, bool>> objDict:
                {
                    var count = objDict is ICollection<KeyValuePair<string, bool>> dct ? dct.Count : objDict.Count();
                    ddwafObjectStruct = ProcessKeyValuePairs(objDict, count, &GetKey1, &GetValue1);
                    static string GetKey1(KeyValuePair<string, bool> item) => item.Key;
                    static object GetValue1(KeyValuePair<string, bool> item) => item.Value;
                    break;
                }

                case IEnumerable<KeyValuePair<string, string>> objDict:
                {
                    var count = objDict is ICollection<KeyValuePair<string, string>> dct ? dct.Count : objDict.Count();
                    ddwafObjectStruct = ProcessKeyValuePairs(objDict, count, &GetKey2, &GetValue2);
                    static string GetKey2(KeyValuePair<string, string> item) => item.Key;
                    static object GetValue2(KeyValuePair<string, string> item) => item.Value;
                    break;
                }

                case IEnumerable<KeyValuePair<string, JToken>> objDict:
                {
                    var count = objDict is ICollection<KeyValuePair<string, JToken>> dct ? dct.Count : objDict.Count();
                    ddwafObjectStruct = ProcessKeyValuePairs(objDict, count, &GetKey3, &GetValue3);
                    static string GetKey3(KeyValuePair<string, JToken> item) => item.Key;
                    static object GetValue3(KeyValuePair<string, JToken> item) => item.Value;
                    break;
                }

                case IEnumerable<KeyValuePair<string, string[]>> objDict:
                {
                    var count = objDict is ICollection<KeyValuePair<string, string[]>> dct ? dct.Count : objDict.Count();
                    ddwafObjectStruct = ProcessKeyValuePairs(objDict, count, &GetKey4, &GetValue4);
                    static string GetKey4(KeyValuePair<string, string[]> item) => item.Key;
                    static object GetValue4(KeyValuePair<string, string[]> item) => item.Value;
                    break;
                }

                case IEnumerable<KeyValuePair<string, List<string>>> objDict:
                {
                    var count = objDict is ICollection<KeyValuePair<string, List<string>>> dct ? dct.Count : objDict.Count();
                    ddwafObjectStruct = ProcessKeyValuePairs(objDict, count, &GetKey5, &GetValue5);
                    static string GetKey5(KeyValuePair<string, List<string>> item) => item.Key;
                    static object GetValue5(KeyValuePair<string, List<string>> item) => item.Value;
                    break;
                }

                case IEnumerable enumerable:
                {
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_ARRAY };

                    if (applySafetyLimits && remainingDepth-- <= 0)
                    {
                        Log.Warning("EncodeList: object graph too deep, truncating nesting {Items}", string.Join(", ", enumerable));
                        break;
                    }

                    if (enumerable is IList { Count: var count } listInstance)
                    {
                        if (applySafetyLimits && count > WafConstants.MaxContainerSize)
                        {
                            Log.Warning<int>("EncodeList: list too long, it will be truncated, MaxMapOrArrayLength {MaxMapOrArrayLength}", WafConstants.MaxContainerSize);
                        }

                        var childrenCount = !applySafetyLimits || count < WafConstants.MaxContainerSize ? count : WafConstants.MaxContainerSize;
                        var childrenFromPool = ObjectStructSize * childrenCount < MaxBytesForMaxStringLength;
                        var childrenData = childrenFromPool ? pool.Rent() : Marshal.AllocCoTaskMem(ObjectStructSize * childrenCount);

                        // Avoid boxing of known values types from the switch above
                        switch (listInstance)
                        {
                            case IList<bool> boolCollection:
                                EnumerateAndEncode(boolCollection);
                                break;
                            case IList<decimal> intCollection:
                                EnumerateAndEncode(intCollection);
                                break;
                            case IList<double> intCollection:
                                EnumerateAndEncode(intCollection);
                                break;
                            case IList<float> intCollection:
                                EnumerateAndEncode(intCollection);
                                break;
                            case IList<int> intCollection:
                                EnumerateAndEncode(intCollection);
                                break;
                            case IList<uint> uintCollection:
                                EnumerateAndEncode(uintCollection);
                                break;
                            case IList<long> longCollection:
                                EnumerateAndEncode(longCollection);
                                break;
                            case IList<ulong> ulongCollection:
                                EnumerateAndEncode(ulongCollection);
                                break;
                            default:
                                EnumerateAndEncodeIList(listInstance);
                                break;
                        }

                        ddwafObjectStruct.Array = childrenData;
                        ddwafObjectStruct.NbEntries = (ulong)childrenCount;
                        argToFree.Add(childrenData);

                        [MethodImpl(MethodImplOptions.AggressiveInlining)]
                        void EnumerateAndEncode<T>(IList<T> lstInstance)
                        {
                            var itemData = childrenData;
                            for (var idx = 0; idx < count; idx++)
                            {
                                *(DdwafObjectStruct*)itemData = Encode(lstInstance[idx], argToFree, applySafetyLimits: applySafetyLimits, remainingDepth: remainingDepth, pool: pool);
                                itemData += ObjectStructSize;
                            }
                        }

                        [MethodImpl(MethodImplOptions.AggressiveInlining)]
                        void EnumerateAndEncodeIList(IList lstInstance)
                        {
                            var itemData = childrenData;
                            for (var idx = 0; idx < count; idx++)
                            {
                                *(DdwafObjectStruct*)itemData = Encode(lstInstance[idx], argToFree, applySafetyLimits: applySafetyLimits, remainingDepth: remainingDepth, pool: pool);
                                itemData += ObjectStructSize;
                            }
                        }
                    }
                    else
                    {
                        var childrenCount = 0;
                        // Let's enumerate first.
                        foreach (var val in enumerable)
                        {
                            childrenCount++;
                            if (applySafetyLimits && childrenCount == WafConstants.MaxContainerSize)
                            {
                                Log.Warning<int>("EncodeList: list too long, it will be truncated, MaxMapOrArrayLength {MaxMapOrArrayLength}", WafConstants.MaxContainerSize);
                                break;
                            }
                        }

                        if (childrenCount > 0)
                        {
                            var childrenFromPool = ObjectStructSize * childrenCount < MaxBytesForMaxStringLength;
                            var childrenData = childrenFromPool ? pool.Rent() : Marshal.AllocCoTaskMem(ObjectStructSize * childrenCount);
                            var itemData = childrenData;
                            var idx = 0;
                            foreach (var val in enumerable)
                            {
                                if (idx > childrenCount)
                                {
                                    break;
                                }

                                *(DdwafObjectStruct*)itemData = Encode(val, argToFree, applySafetyLimits: applySafetyLimits, remainingDepth: remainingDepth, pool: pool);
                                itemData += ObjectStructSize;
                                idx++;
                            }

                            ddwafObjectStruct.Array = childrenData;
                            ddwafObjectStruct.NbEntries = (ulong)childrenCount;
                            argToFree.Add(childrenData);
                        }
                    }

                    break;
                }

                default:
                    Log.Warning("Couldn't encode object of unknown type {Type}, falling back to ToString", o.GetType());
                    ddwafObjectStruct = GetStringObject(string.Empty);
                    break;
            }

            if (!string.IsNullOrEmpty(key))
            {
                ddwafObjectStruct.ParameterName = ConvertToUtf8(key!, false).Item1;
                ddwafObjectStruct.ParameterNameLength = (ulong)key!.Length;
            }

            return ddwafObjectStruct;
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
                    double i => sb.Append(i),
                    bool i => sb.Append(i),
                    IEnumerable<KeyValuePair<string, JToken>> objDict => FormatDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), sb),
                    IEnumerable<KeyValuePair<string, string>> objDict => FormatDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), sb),
                    IEnumerable<KeyValuePair<string, List<string>>> objDict => FormatDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), sb),
                    // dont remove IEnumerable<KeyValuePair<string, string[]>>, it is used for logging cookies which are this type in debug mode
                    IEnumerable<KeyValuePair<string, string[]>> objDict => FormatDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), sb),
                    IEnumerable<KeyValuePair<string, object>> objDict => FormatDictionary(objDict, sb),
                    IEnumerable<KeyValuePair<string, bool>> objDict => FormatDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), sb),
                    IList<JToken> objs => FormatList(objs, sb),
                    IList<string> objs => FormatList(objs, sb),
                    // this becomes ugly but this should change once PR improving marshalling of the waf is merged
                    IList<long> objs => FormatList(objs, sb),
                    IList<ulong> objs => FormatList(objs, sb),
                    IList<int> objs => FormatList(objs, sb),
                    IList<uint> objs => FormatList(objs, sb),
                    IList<double> objs => FormatList(objs, sb),
                    IList<decimal> objs => FormatList(objs, sb),
                    IList<bool> objs => FormatList(objs, sb),
                    IList<float> objs => FormatList(objs, sb),
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

        private static StringBuilder FormatList<T>(IEnumerable<T> objs, StringBuilder sb)
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

        public readonly ref struct EncodeResult
        {
            private readonly List<IntPtr> _pointers;
            private readonly UnmanagedMemoryPool _pool;
            public readonly DdwafObjectStruct Result;

            internal EncodeResult(List<IntPtr> pointers, UnmanagedMemoryPool pool, DdwafObjectStruct result)
            {
                _pointers = pointers;
                _pool = pool;
                Result = result;
            }

            public void Dispose()
            {
                _pool.Return(_pointers);
                _pointers.Clear();
            }
        }
    }
}
