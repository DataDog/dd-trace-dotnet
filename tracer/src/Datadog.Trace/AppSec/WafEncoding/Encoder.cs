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
using System.Xml.Linq;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;
using Datadog.Trace.VendoredMicrosoftCode.System.Runtime.CompilerServices.Unsafe;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.StatsdClient.Utils;

namespace Datadog.Trace.AppSec.WafEncoding
{
    internal class Encoder : IEncoder
    {
        private const int MaxBytesForMaxStringLength = (WafConstants.MaxStringLength * 4) + 1;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Encoder));
        private static readonly int ObjectStructSize = Marshal.SizeOf(typeof(DdwafObjectStruct));
        private static int _poolSize = 500;

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

                var instance = new UnmanagedMemoryPool(MaxBytesForMaxStringLength, _poolSize);
                _pool = instance;
                return instance;
            }
        }

        /// <summary>
        /// For testing purposes
        /// </summary>
        internal static void SetPoolSize(int poolSize)
        {
            _poolSize = poolSize;
        }

        public static string FormatArgs(object o)
        {
            // zero capacity because we don't know the size in advance
            var sb = StringBuilderCache.Acquire(0);
            FormatArgsInternal(o, sb);
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        public IEncodeResult Encode<TInstance>(TInstance? o, int remainingDepth = WafConstants.MaxContainerDepth, string? key = null, bool applySafetyLimits = true)
        {
            var context = new EncoderContext(applySafetyLimits, Pool, new List<IntPtr>());
            var result = Encode(ref context, remainingDepth, key, o);
            return new EncodeResult(context.Buffers, context.Pool, ref result);
        }

        // -----------------------------------
        internal DdwafObjectStruct Encode<TInstance>(TInstance? o, List<IntPtr> argToFree, int remainingDepth = WafConstants.MaxContainerDepth, string? key = null, bool applySafetyLimits = true, UnmanagedMemoryPool? pool = null)
        {
            var context = new EncoderContext(applySafetyLimits, pool ?? Pool, argToFree);
            return Encode(ref context, remainingDepth, key, o);
        }

        private static unsafe DdwafObjectStruct Encode<TInstance>(ref EncoderContext context, int remainingDepth, string? key, TInstance? o)
        {
            DdwafObjectStruct ddwafObjectStruct;

            switch (o)
            {
                case string str:
                    ddwafObjectStruct = GetStringObject(ref context, str);
                    break;
                case JValue:
                    ddwafObjectStruct = GetStringObject(ref context, o?.ToString() ?? string.Empty);
                    break;
                case null:
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_NULL };
                    break;
                case ulong u:
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_UNSIGNED, UintValue = u };
                    break;
                case uint u:
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_UNSIGNED, UintValue = u };
                    break;
                case int i:
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_SIGNED, IntValue = i };
                    break;
                case long u:
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_SIGNED, IntValue = u };
                    break;
                case decimal d:
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_DOUBLE, DoubleValue = (double)d };
                    break;
                case double d:
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_DOUBLE, DoubleValue = d };
                    break;
                case float d:
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_DOUBLE, DoubleValue = d };
                    break;
                case bool b:
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_BOOL, ByteValue = b ? (byte)1 : (byte)0 };
                    break;
                case IEnumerable<KeyValuePair<string, object>> objDict:
                {
                    var collectionDict = objDict as ICollection<KeyValuePair<string, object>> ?? objDict.ToList();
                    ddwafObjectStruct = ProcessKeyValuePairs(ref context, remainingDepth, key, collectionDict, collectionDict.Count, &GetKey1, &GetValue1);
                    static string GetKey1(KeyValuePair<string, object> item) => item.Key;
                    static object GetValue1(KeyValuePair<string, object> item) => item.Value;
                    break;
                }

                case IEnumerable<KeyValuePair<string, bool>> objDict:
                {
                    var collectionDict = objDict as ICollection<KeyValuePair<string, bool>> ?? objDict.ToList();
                    ddwafObjectStruct = ProcessKeyValuePairs(ref context, remainingDepth, key, collectionDict, collectionDict.Count, &GetKey1, &GetValue1);
                    static string GetKey1(KeyValuePair<string, bool> item) => item.Key;
                    static object GetValue1(KeyValuePair<string, bool> item) => item.Value;
                    break;
                }

                case IEnumerable<KeyValuePair<string, string>> objDict:
                {
                    var collectionDict = objDict as ICollection<KeyValuePair<string, string>> ?? objDict.ToList();
                    ddwafObjectStruct = ProcessKeyValuePairs(ref context, remainingDepth, key, collectionDict, collectionDict.Count, &GetKey2, &GetValue2);
                    static string GetKey2(KeyValuePair<string, string> item) => item.Key;
                    static object GetValue2(KeyValuePair<string, string> item) => item.Value;
                    break;
                }

                case IEnumerable<KeyValuePair<string, JToken>> objDict:
                {
                    var collectionDict = objDict as ICollection<KeyValuePair<string, JToken>> ?? objDict.ToList();
                    ddwafObjectStruct = ProcessKeyValuePairs(ref context, remainingDepth, key, collectionDict, collectionDict.Count, &GetKey3, &GetValue3);
                    static string GetKey3(KeyValuePair<string, JToken> item) => item.Key;
                    static object GetValue3(KeyValuePair<string, JToken> item) => item.Value;
                    break;
                }

                case IEnumerable<KeyValuePair<string, string[]>> objDict:
                {
                    var collectionDict = objDict as ICollection<KeyValuePair<string, string[]>> ?? objDict.ToList();
                    var count = collectionDict.Count;
                    ddwafObjectStruct = ProcessKeyValuePairs(ref context, remainingDepth, key, collectionDict, collectionDict.Count, &GetKey4, &GetValue4);
                    static string GetKey4(KeyValuePair<string, string[]> item) => item.Key;
                    static object GetValue4(KeyValuePair<string, string[]> item) => item.Value;
                    break;
                }

                case IEnumerable<KeyValuePair<string, List<string>>> objDict:
                {
                    var collectionDict = objDict as ICollection<KeyValuePair<string, List<string>>> ?? objDict.ToList();
                    ddwafObjectStruct = ProcessKeyValuePairs(ref context, remainingDepth, key, collectionDict, collectionDict.Count, &GetKey5, &GetValue5);
                    static string GetKey5(KeyValuePair<string, List<string>> item) => item.Key;
                    static object GetValue5(KeyValuePair<string, List<string>> item) => item.Value;
                    break;
                }

                case IEnumerable enumerable:
                {
                    ddwafObjectStruct = ProcessIEnumerable(ref context, remainingDepth, enumerable);
                    break;
                }

                default:
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Warning("Couldn't encode object of unknown type {Type}, falling back to ToString", o.GetType());
                    }

                    ddwafObjectStruct = GetStringObject(ref context, string.Empty);
                    break;
            }

            if (!string.IsNullOrEmpty(key))
            {
                ddwafObjectStruct.ParameterName = ConvertToUtf8(ref context, key!, false).Item1;
                ddwafObjectStruct.ParameterNameLength = (ulong)key!.Length;
            }

            return ddwafObjectStruct;
        }

        private static unsafe DdwafObjectStruct ProcessIEnumerable(ref EncoderContext context, int remainingDepth, IEnumerable enumerable)
        {
            var ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_ARRAY };

            if (context.ApplySafetyLimits && remainingDepth-- <= 0)
            {
                TelemetryFactory.Metrics.RecordCountInputTruncated(MetricTags.TruncationReason.ObjectTooDeep);
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("EncodeList: object graph too deep, truncating nesting {Items}", string.Join(", ", enumerable));
                }

                return ddwafObjectStruct;
            }

            if (enumerable is IList { Count: var count } listInstance)
            {
                if (context.ApplySafetyLimits && count > WafConstants.MaxContainerSize)
                {
                    TelemetryFactory.Metrics.RecordCountInputTruncated(MetricTags.TruncationReason.ListOrMapTooLarge);
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug<int>("EncodeList: list too long, it will be truncated, MaxMapOrArrayLength {MaxMapOrArrayLength}", WafConstants.MaxContainerSize);
                    }
                }

                var childrenCount = !context.ApplySafetyLimits || count < WafConstants.MaxContainerSize ? count : WafConstants.MaxContainerSize;
                var childrenFromPool = ObjectStructSize * childrenCount < MaxBytesForMaxStringLength;
                var childrenData = childrenFromPool ? context.Pool.Rent() : Marshal.AllocCoTaskMem(ObjectStructSize * childrenCount);

                // Avoid boxing of known values types from the switch above
                switch (listInstance)
                {
                    case IList<bool> boolCollection:
                        EnumerateAndEncode(ref context, remainingDepth, boolCollection, childrenData, childrenCount);
                        break;
                    case IList<decimal> intCollection:
                        EnumerateAndEncode(ref context, remainingDepth, intCollection, childrenData, childrenCount);
                        break;
                    case IList<double> intCollection:
                        EnumerateAndEncode(ref context, remainingDepth, intCollection, childrenData, childrenCount);
                        break;
                    case IList<float> intCollection:
                        EnumerateAndEncode(ref context, remainingDepth, intCollection, childrenData, childrenCount);
                        break;
                    case IList<int> intCollection:
                        EnumerateAndEncode(ref context, remainingDepth, intCollection, childrenData, childrenCount);
                        break;
                    case IList<uint> uintCollection:
                        EnumerateAndEncode(ref context, remainingDepth, uintCollection, childrenData, childrenCount);
                        break;
                    case IList<long> longCollection:
                        EnumerateAndEncode(ref context, remainingDepth, longCollection, childrenData, childrenCount);
                        break;
                    case IList<ulong> ulongCollection:
                        EnumerateAndEncode(ref context, remainingDepth, ulongCollection, childrenData, childrenCount);
                        break;
                    default:
                        EnumerateAndEncodeIList(ref context, remainingDepth, listInstance, childrenData, childrenCount);
                        break;
                }

                ddwafObjectStruct.Array = childrenData;
                ddwafObjectStruct.NbEntries = (ulong)childrenCount;
                context.Buffers.Add(childrenData);
            }
            else
            {
                var childrenCount = 0;
                // Let's enumerate first.
                foreach (var val in enumerable)
                {
                    childrenCount++;
                    if (context.ApplySafetyLimits && childrenCount == WafConstants.MaxContainerSize)
                    {
                        TelemetryFactory.Metrics.RecordCountInputTruncated(MetricTags.TruncationReason.ListOrMapTooLarge);
                        if (Log.IsEnabled(LogEventLevel.Debug))
                        {
                            Log.Debug<int>("EncodeList: list too long, it will be truncated, MaxMapOrArrayLength {MaxMapOrArrayLength}", WafConstants.MaxContainerSize);
                        }

                        break;
                    }
                }

                if (childrenCount > 0)
                {
                    var childrenFromPool = ObjectStructSize * childrenCount < MaxBytesForMaxStringLength;
                    var childrenData = childrenFromPool ? context.Pool.Rent() : Marshal.AllocCoTaskMem(ObjectStructSize * childrenCount);
                    var itemData = childrenData;
                    var idx = 0;
                    foreach (var val in enumerable)
                    {
                        if (idx > childrenCount)
                        {
                            break;
                        }

                        *(DdwafObjectStruct*)itemData = Encode(ref context, remainingDepth, null, val);
                        itemData += ObjectStructSize;
                        idx++;
                    }

                    ddwafObjectStruct.Array = childrenData;
                    ddwafObjectStruct.NbEntries = (ulong)childrenCount;
                    context.Buffers.Add(childrenData);
                }
            }

            return ddwafObjectStruct;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void EnumerateAndEncode<T>(ref EncoderContext context, int remainingDepth, IList<T> lstInstance, IntPtr childrenData, int childrenCount)
        {
            var itemData = childrenData;
            for (var idx = 0; idx < childrenCount; idx++)
            {
                *(DdwafObjectStruct*)itemData = Encode(ref context, remainingDepth, null, lstInstance[idx]);
                itemData += ObjectStructSize;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void EnumerateAndEncodeIList(ref EncoderContext context, int remainingDepth, IList lstInstance, IntPtr childrenData, int childrenCount)
        {
            var itemData = childrenData;
            for (var idx = 0; idx < childrenCount; idx++)
            {
                *(DdwafObjectStruct*)itemData = Encode(ref context, remainingDepth, null, lstInstance[idx]);
                itemData += ObjectStructSize;
            }
        }

        private static unsafe DdwafObjectStruct ProcessKeyValuePairs<TKey, TValue>(ref EncoderContext context, int remainingDepth, string? key, IEnumerable<KeyValuePair<TKey, TValue>> enumerableDic, int count, delegate*<KeyValuePair<TKey, TValue>, string?> getKey, delegate*<KeyValuePair<TKey, TValue>, object?> getValue)
            where TKey : notnull
        {
            var ddWafObjectMap = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP };
            if (!string.IsNullOrEmpty(key))
            {
                var convertToUtf8 = ConvertToUtf8(ref context, key!, false);
                ddWafObjectMap.ParameterName = convertToUtf8.Item1;
                ddWafObjectMap.ParameterNameLength = (ulong)key!.Length;
            }

            if (context.ApplySafetyLimits)
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

                    TelemetryFactory.Metrics.RecordCountInputTruncated(MetricTags.TruncationReason.ObjectTooDeep);
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug("EncodeDictionary: object graph too deep, truncating nesting {Items}", GetItemsAsString());
                    }

                    return ddWafObjectMap;
                }

                if (count > WafConstants.MaxContainerSize)
                {
                    TelemetryFactory.Metrics.RecordCountInputTruncated(MetricTags.TruncationReason.ListOrMapTooLarge);
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug<int>("EncodeList: list too long, it will be truncated, MaxMapOrArrayLength {MaxMapOrArrayLength}", WafConstants.MaxContainerSize);
                    }
                }
            }

            var childrenCount = !context.ApplySafetyLimits || count < WafConstants.MaxContainerSize ? count : WafConstants.MaxContainerSize;
            var childrenFromPool = ObjectStructSize * childrenCount < MaxBytesForMaxStringLength;
            var childrenData = childrenFromPool ? context.Pool.Rent() : Marshal.AllocCoTaskMem(ObjectStructSize * childrenCount);

            if (enumerableDic is IDictionary iDic)
            {
                var typeKVP = typeof(KeyValuePair<TKey, TValue>);
                if (typeKVP == typeof(KeyValuePair<string, string>))
                {
                    EnumerateIDictionaryItems<string, string>(
                        ref context,
                        remainingDepth,
                        iDic,
                        (delegate*<KeyValuePair<string, string>, string?>)getKey,
                        (delegate*<KeyValuePair<string, string>, object?>)getValue,
                        childrenData,
                        childrenCount);
                }
                else if (typeKVP == typeof(KeyValuePair<string, object>))
                {
                    EnumerateIDictionaryItems<string, object>(
                        ref context,
                        remainingDepth,
                        iDic,
                        (delegate*<KeyValuePair<string, object>, string?>)getKey,
                        (delegate*<KeyValuePair<string, object>, object?>)getValue,
                        childrenData,
                        childrenCount);
                }
                else if (typeKVP == typeof(KeyValuePair<string, string[]>))
                {
                    EnumerateIDictionaryItems<string, string[]>(
                        ref context,
                        remainingDepth,
                        iDic,
                        (delegate*<KeyValuePair<string, string[]>, string?>)getKey,
                        (delegate*<KeyValuePair<string, string[]>, object?>)getValue,
                        childrenData,
                        childrenCount);
                }
                else if (typeKVP == typeof(KeyValuePair<string, List<string>>))
                {
                    EnumerateIDictionaryItems<string, List<string>>(
                        ref context,
                        remainingDepth,
                        iDic,
                        (delegate*<KeyValuePair<string, List<string>>, string?>)getKey,
                        (delegate*<KeyValuePair<string, List<string>>, object?>)getValue,
                        childrenData,
                        childrenCount);
                }
                else if (typeKVP == typeof(KeyValuePair<string, JToken>))
                {
                    EnumerateIDictionaryItems<string, JToken>(
                        ref context,
                        remainingDepth,
                        iDic,
                        (delegate*<KeyValuePair<string, JToken>, string?>)getKey,
                        (delegate*<KeyValuePair<string, JToken>, object?>)getValue,
                        childrenData,
                        childrenCount);
                }
                else
                {
                    EnumerateIDictionaryItems<string, TValue>(
                        ref context,
                        remainingDepth,
                        iDic,
                        (delegate*<KeyValuePair<string, TValue>, string?>)getKey,
                        (delegate*<KeyValuePair<string, TValue>, object?>)getValue,
                        childrenData,
                        childrenCount);
                }
            }
            else
            {
                var itemData = childrenData;
                var maxChildrenCount = childrenCount;

                for (var i = 0; i < maxChildrenCount; i++)
                {
                    var element = enumerableDic.ElementAt(i);
                    var elementKey = getKey(element);
                    if (string.IsNullOrEmpty(elementKey))
                    {
                        childrenCount--;
                        if (Log.IsEnabled(LogEventLevel.Debug))
                        {
                            Log.Debug("EncodeDictionary: ignoring dictionary member with null name");
                        }

                        continue;
                    }

                    *(DdwafObjectStruct*)itemData = Encode(ref context, remainingDepth, elementKey, getValue(element));
                    itemData += ObjectStructSize;
                }
            }

            ddWafObjectMap.Array = childrenData;
            ddWafObjectMap.NbEntries = (ulong)childrenCount;
            context.Buffers.Add(childrenData);
            return ddWafObjectMap;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void EnumerateIDictionaryItems<TKey, TValue>(ref EncoderContext context, int remainingDepth, IDictionary enumerableDic, delegate*<KeyValuePair<TKey, TValue>, string?> getKey, delegate*<KeyValuePair<TKey, TValue>, object?> getValue, IntPtr childrenData, int childrenCount)
            where TKey : notnull
        {
            var itemData = childrenData;
            var dic = (Dictionary<TKey, TValue>)enumerableDic;
            var maxChildrenCount = childrenCount;
            for (var i = 0; i < maxChildrenCount; i++)
            {
                var originalElement = dic.ElementAt(i);
                var element = VendoredMicrosoftCode.System.Runtime.CompilerServices.Unsafe.Unsafe.As<KeyValuePair<TKey, TValue>, KeyValuePair<TKey, TValue>>(ref originalElement);
                var elementKey = getKey(element);
                if (string.IsNullOrEmpty(elementKey))
                {
                    childrenCount--;
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug("EncodeDictionary: ignoring dictionary member with null name");
                    }

                    continue;
                }

                *(DdwafObjectStruct*)itemData = Encode(ref context, remainingDepth, elementKey, getValue(element!));
                itemData += ObjectStructSize;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe Tuple<IntPtr, int> ConvertToUtf8(ref EncoderContext context, string s, bool applySafety)
        {
            IntPtr unmanagedMemory;
            int writtenBytes;
            var length = s.Length;
            if (applySafety || length <= WafConstants.MaxStringLength)
            {
                length = Math.Min(length, WafConstants.MaxStringLength);
                unmanagedMemory = context.Pool.Rent();
                fixed (char* chrPtr = s)
                {
                    writtenBytes = System.Text.Encoding.UTF8.GetBytes(chrPtr, length, (byte*)unmanagedMemory, MaxBytesForMaxStringLength);
                }
            }
            else
            {
                var bytesCount = System.Text.Encoding.UTF8.GetMaxByteCount(length) + 1;
                unmanagedMemory = Marshal.AllocCoTaskMem(bytesCount);
                fixed (char* chrPtr = s)
                {
                    writtenBytes = System.Text.Encoding.UTF8.GetBytes(chrPtr, length, (byte*)unmanagedMemory, bytesCount);
                }
            }

            Marshal.WriteByte(unmanagedMemory, writtenBytes, (byte)'\0');
            context.Buffers.Add(unmanagedMemory);
            return new Tuple<IntPtr, int>(unmanagedMemory, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe DdwafObjectStruct GetStringObject(ref EncoderContext context, string value)
        {
            var convertToUtf8 = ConvertToUtf8(ref context, value, context.ApplySafetyLimits);
            var ddWafObject = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_STRING, Array = convertToUtf8.Item1, NbEntries = (ulong)convertToUtf8.Item2 };
            return ddWafObject;
        }

        private static void FormatArgsInternal(object o, StringBuilder sb)
        {
            if (o is ArrayList arrayList)
            {
                var list = new List<object>();
                foreach (var item in arrayList)
                {
                    if (item is not null)
                    {
                        list.Add(item);
                    }
                }

                o = list;
            }

            _ =
                o switch
                {
                    string s => sb.Append(s),
                    int i => sb.Append(i),
                    long i => sb.Append(i),
                    uint i => sb.Append(i),
                    ulong i => sb.Append(i),
                    float i => sb.Append(i),
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

        private struct EncoderContext
        {
            public readonly bool ApplySafetyLimits;
            public readonly UnmanagedMemoryPool Pool;
            public readonly List<IntPtr> Buffers;

            public EncoderContext(bool applySafetyLimits, UnmanagedMemoryPool pool, List<IntPtr> buffers)
            {
                ApplySafetyLimits = applySafetyLimits;
                Pool = pool;
                Buffers = buffers;
            }
        }

        public class EncodeResult : IEncodeResult
        {
            private readonly List<IntPtr> _pointers;
            private readonly UnmanagedMemoryPool _innerPool;
            private DdwafObjectStruct _result;

            internal EncodeResult(List<IntPtr> pointers, UnmanagedMemoryPool pool, ref DdwafObjectStruct result)
            {
                _pointers = pointers;
                _innerPool = pool;
                _result = result;
            }

            public DdwafObjectStruct ResultDdwafObject => _result;

            public void Dispose()
            {
                _innerPool.Return(_pointers);
                _pointers.Clear();
            }
        }
    }
}
