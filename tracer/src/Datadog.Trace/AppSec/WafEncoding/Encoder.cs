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
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.WafEncoding
{
    internal sealed class Encoder : IEncoder
    {
        private const int MaxBytesForMaxStringLength = (WafConstants.MaxStringLength * 4) + 1;

        /// <summary>
        /// Stride of an array's buffer: an array points at contiguous <c>ddwaf_object</c>.
        /// </summary>
        private const int ObjectStructSize = DdwafObjectStruct.ObjectSize;

        /// <summary>
        /// Stride of a map's buffer: since libddwaf 2.x a map points at contiguous <c>ddwaf_object_kv</c>,
        /// not at objects, because keys are no longer stored inside the object itself.
        /// </summary>
        private const int KvStructSize = DdwafObjectKvStruct.KvSize;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Encoder));
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

        internal static void Dispose()
        {
            try
            {
                if (_pool is { IsDisposed: false })
                {
                    _pool.Dispose();
                    _pool = null;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "WafEncoder Crashed on shutdown.");
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
            var sb = StringBuilderCache.Acquire();
            FormatArgsInternal(o, sb);
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        public IEncodeResult Encode<TInstance>(TInstance? o, int remainingDepth = WafConstants.MaxContainerDepth, bool applySafetyLimits = true)
        {
            var context = new EncoderContext(applySafetyLimits, Pool, new List<IntPtr>());
            var result = Encode(ref context, remainingDepth, o);
            return new EncodeResult(context.Buffers, context.Pool, ref result, context.Truncated);
        }

        // -----------------------------------
        internal DdwafObjectStruct Encode<TInstance>(TInstance? o, List<IntPtr> argToFree, int remainingDepth = WafConstants.MaxContainerDepth, bool applySafetyLimits = true, UnmanagedMemoryPool? pool = null)
        {
            var context = new EncoderContext(applySafetyLimits, pool ?? Pool, argToFree);
            return Encode(ref context, remainingDepth, o);
        }

        private static unsafe DdwafObjectStruct Encode<TInstance>(ref EncoderContext context, int remainingDepth, TInstance? o)
        {
            DdwafObjectStruct ddwafObjectStruct;

            switch (o)
            {
                case string str:
                    ddwafObjectStruct = GetStringObject(ref context, str, context.ApplySafetyLimits);
                    break;
                case JValue jValue:
                    ddwafObjectStruct = Encode(ref context, remainingDepth, jValue.Value);
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
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_FLOAT, DoubleValue = (double)d };
                    break;
                case double d:
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_FLOAT, DoubleValue = d };
                    break;
                case float d:
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_FLOAT, DoubleValue = d };
                    break;
                case bool b:
                    ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_BOOL, BoolByte = b ? (byte)1 : (byte)0 };
                    break;
                case IEnumerable<KeyValuePair<string, object>> objDict:
                {
                    var collectionDict = objDict as ICollection<KeyValuePair<string, object>> ?? objDict.ToList();
                    ddwafObjectStruct = ProcessKeyValuePairs(ref context, remainingDepth, collectionDict, collectionDict.Count, &GetKey1, &GetValue1);
                    static string GetKey1(KeyValuePair<string, object> item) => item.Key;
                    static object GetValue1(KeyValuePair<string, object> item) => item.Value;
                    break;
                }

                case IEnumerable<KeyValuePair<string, bool>> objDict:
                {
                    var collectionDict = objDict as ICollection<KeyValuePair<string, bool>> ?? objDict.ToList();
                    ddwafObjectStruct = ProcessKeyValuePairs(ref context, remainingDepth, collectionDict, collectionDict.Count, &GetKey1, &GetValue1);
                    static string GetKey1(KeyValuePair<string, bool> item) => item.Key;
                    static object GetValue1(KeyValuePair<string, bool> item) => item.Value;
                    break;
                }

                case IEnumerable<KeyValuePair<string, string>> objDict:
                {
                    var collectionDict = objDict as ICollection<KeyValuePair<string, string>> ?? objDict.ToList();
                    ddwafObjectStruct = ProcessKeyValuePairs(ref context, remainingDepth, collectionDict, collectionDict.Count, &GetKey2, &GetValue2);
                    static string GetKey2(KeyValuePair<string, string> item) => item.Key;
                    static object GetValue2(KeyValuePair<string, string> item) => item.Value;
                    break;
                }

                case IEnumerable<KeyValuePair<string, JToken>> objDict:
                {
                    var collectionDict = objDict as ICollection<KeyValuePair<string, JToken>> ?? objDict.ToList();
                    ddwafObjectStruct = ProcessKeyValuePairs(ref context, remainingDepth, collectionDict, collectionDict.Count, &GetKey3, &GetValue3);
                    static string GetKey3(KeyValuePair<string, JToken> item) => item.Key;
                    static object GetValue3(KeyValuePair<string, JToken> item) => item.Value;
                    break;
                }

                case IEnumerable<KeyValuePair<string, string[]>> objDict:
                {
                    var collectionDict = objDict as ICollection<KeyValuePair<string, string[]>> ?? objDict.ToList();
                    var count = collectionDict.Count;
                    ddwafObjectStruct = ProcessKeyValuePairs(ref context, remainingDepth, collectionDict, collectionDict.Count, &GetKey4, &GetValue4);
                    static string GetKey4(KeyValuePair<string, string[]> item) => item.Key;
                    static object GetValue4(KeyValuePair<string, string[]> item) => item.Value;
                    break;
                }

                case IEnumerable<KeyValuePair<string, List<string>>> objDict:
                {
                    var collectionDict = objDict as ICollection<KeyValuePair<string, List<string>>> ?? objDict.ToList();
                    ddwafObjectStruct = ProcessKeyValuePairs(ref context, remainingDepth, collectionDict, collectionDict.Count, &GetKey5, &GetValue5);
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

                    ddwafObjectStruct = GetStringObject(ref context, string.Empty, context.ApplySafetyLimits);
                    break;
            }

            return ddwafObjectStruct;
        }

        /// <summary>
        /// Caps a container's element count to what a <c>ddwaf_object</c> can address. Both the size and
        /// the capacity of arrays and maps are 16 bit, and libddwaf does not guard against overflowing
        /// them, so going over the limit would make it write past the end of the buffer.
        /// </summary>
        private static int ClampContainerCount(ref EncoderContext context, int count)
        {
            if (count <= DdwafObjectStruct.MaxContainerCapacity)
            {
                return count;
            }

            context.Truncated = true;
            TelemetryFactory.Metrics.RecordCountInputTruncated(MetricTags.TruncationReason.ListOrMapTooLarge);
            Log.Warning<int, int>("Container holds {Count} entries, more than the {MaxContainerCapacity} a WAF object can address, it will be truncated", count, DdwafObjectStruct.MaxContainerCapacity);
            return DdwafObjectStruct.MaxContainerCapacity;
        }

        private static unsafe DdwafObjectStruct ProcessIEnumerable(ref EncoderContext context, int remainingDepth, IEnumerable enumerable)
        {
            var ddwafObjectStruct = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_ARRAY };

            if (context.ApplySafetyLimits && remainingDepth-- <= 0)
            {
                context.Truncated = true;
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
                    context.Truncated = true;
                    TelemetryFactory.Metrics.RecordCountInputTruncated(MetricTags.TruncationReason.ListOrMapTooLarge);
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug<int>("EncodeList: list too long, it will be truncated, MaxMapOrArrayLength {MaxMapOrArrayLength}", WafConstants.MaxContainerSize);
                    }
                }

                var childrenCount = !context.ApplySafetyLimits || count < WafConstants.MaxContainerSize ? count : WafConstants.MaxContainerSize;
                childrenCount = ClampContainerCount(ref context, childrenCount);
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

                ddwafObjectStruct.Pointer = childrenData;
                ddwafObjectStruct.Size = (ushort)childrenCount;
                ddwafObjectStruct.Capacity = (ushort)childrenCount;
                context.Buffers.Add(childrenData);
            }
            else
            {
#pragma warning disable CA1851 // Possible multiple enumeration of collections - This _should_ be fixed, unless we verify that only non-IEnumerable types are provided
                var childrenCount = 0;
                // Let's enumerate first.
                foreach (var val in enumerable)
                {
                    childrenCount++;
                    if (context.ApplySafetyLimits && childrenCount == WafConstants.MaxContainerSize)
                    {
                        context.Truncated = true;
                        TelemetryFactory.Metrics.RecordCountInputTruncated(MetricTags.TruncationReason.ListOrMapTooLarge);
                        if (Log.IsEnabled(LogEventLevel.Debug))
                        {
                            Log.Debug<int>("EncodeList: list too long, it will be truncated, MaxMapOrArrayLength {MaxMapOrArrayLength}", WafConstants.MaxContainerSize);
                        }

                        break;
                    }
                }

                childrenCount = ClampContainerCount(ref context, childrenCount);

                if (childrenCount > 0)
                {
                    var childrenFromPool = ObjectStructSize * childrenCount < MaxBytesForMaxStringLength;
                    var childrenData = childrenFromPool ? context.Pool.Rent() : Marshal.AllocCoTaskMem(ObjectStructSize * childrenCount);
                    var itemData = childrenData;
                    var idx = 0;
                    foreach (var val in enumerable)
                    {
                        if (idx >= childrenCount)
                        {
                            break;
                        }

                        *(DdwafObjectStruct*)itemData = Encode(ref context, remainingDepth, val);
                        itemData += ObjectStructSize;
                        idx++;
                    }

                    ddwafObjectStruct.Pointer = childrenData;
                    ddwafObjectStruct.Size = (ushort)childrenCount;
                    ddwafObjectStruct.Capacity = (ushort)childrenCount;
                    context.Buffers.Add(childrenData);
                }
#pragma warning restore CA1851 // Possible multiple enumeration of collections
            }

            return ddwafObjectStruct;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void EnumerateAndEncode<T>(ref EncoderContext context, int remainingDepth, IList<T> lstInstance, IntPtr childrenData, int childrenCount)
        {
            var itemData = childrenData;
            for (var idx = 0; idx < childrenCount; idx++)
            {
                *(DdwafObjectStruct*)itemData = Encode(ref context, remainingDepth, lstInstance[idx]);
                itemData += ObjectStructSize;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void EnumerateAndEncodeIList(ref EncoderContext context, int remainingDepth, IList lstInstance, IntPtr childrenData, int childrenCount)
        {
            var itemData = childrenData;
            for (var idx = 0; idx < childrenCount; idx++)
            {
                *(DdwafObjectStruct*)itemData = Encode(ref context, remainingDepth, lstInstance[idx]);
                itemData += ObjectStructSize;
            }
        }

        private static unsafe DdwafObjectStruct ProcessKeyValuePairs<TKey, TValue>(ref EncoderContext context, int remainingDepth, IEnumerable<KeyValuePair<TKey, TValue>> enumerableDic, int count, delegate*<KeyValuePair<TKey, TValue>, string?> getKey, delegate*<KeyValuePair<TKey, TValue>, object?> getValue)
            where TKey : notnull
        {
            var ddWafObjectMap = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP };

            if (context.ApplySafetyLimits)
            {
                if (remainingDepth-- <= 0)
                {
                    string GetItemsAsString()
                    {
                        var sb = StringBuilderCache.Acquire();
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

                    context.Truncated = true;
                    TelemetryFactory.Metrics.RecordCountInputTruncated(MetricTags.TruncationReason.ObjectTooDeep);
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug("EncodeDictionary: object graph too deep, truncating nesting {Items}", GetItemsAsString());
                    }

                    return ddWafObjectMap;
                }

                if (count > WafConstants.MaxContainerSize)
                {
                    context.Truncated = true;
                    TelemetryFactory.Metrics.RecordCountInputTruncated(MetricTags.TruncationReason.ListOrMapTooLarge);
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug<int>("EncodeList: list too long, it will be truncated, MaxMapOrArrayLength {MaxMapOrArrayLength}", WafConstants.MaxContainerSize);
                    }
                }
            }

            var childrenCount = !context.ApplySafetyLimits || count < WafConstants.MaxContainerSize ? count : WafConstants.MaxContainerSize;
            childrenCount = ClampContainerCount(ref context, childrenCount);

            // a map's buffer is made of ddwaf_object_kv, not of ddwaf_object, hence the bigger stride
            var childrenFromPool = KvStructSize * childrenCount < MaxBytesForMaxStringLength;
            var childrenData = childrenFromPool ? context.Pool.Rent() : Marshal.AllocCoTaskMem(KvStructSize * childrenCount);

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
                        ref childrenCount);
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
                        ref childrenCount);
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
                        ref childrenCount);
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
                        ref childrenCount);
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
                        ref childrenCount);
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
                        ref childrenCount);
                }
            }
            else
            {
#pragma warning disable CA1851 // Possible multiple enumeration of collections - This _should_ be fixed, unless we verify that only non-IEnumerable types are provided
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

                    var entry = (DdwafObjectKvStruct*)itemData;
                    entry->Key = GetStringObject(ref context, elementKey!, false);
                    entry->Value = Encode(ref context, remainingDepth, getValue(element));
                    itemData += KvStructSize;
                }
#pragma warning restore CA1851 // Possible multiple enumeration of collections
            }

            ddWafObjectMap.Pointer = childrenData;
            ddWafObjectMap.Size = (ushort)childrenCount;
            ddWafObjectMap.Capacity = (ushort)childrenCount;
            context.Buffers.Add(childrenData);
            return ddWafObjectMap;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void EnumerateIDictionaryItems<TKey, TValue>(ref EncoderContext context, int remainingDepth, IDictionary enumerableDic, delegate*<KeyValuePair<TKey, TValue>, string?> getKey, delegate*<KeyValuePair<TKey, TValue>, object?> getValue, IntPtr childrenData, ref int childrenCount)
            where TKey : notnull
        {
            var itemData = childrenData;
            var dic = (Dictionary<TKey, TValue>)enumerableDic;
            var maxChildrenCount = childrenCount;
            for (var i = 0; i < maxChildrenCount; i++)
            {
                var originalElement = dic.ElementAt(i);
                var element = Unsafe.As<KeyValuePair<TKey, TValue>, KeyValuePair<TKey, TValue>>(ref originalElement);
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

                var entry = (DdwafObjectKvStruct*)itemData;
                entry->Key = GetStringObject(ref context, elementKey!, false);
                entry->Value = Encode(ref context, remainingDepth, getValue(element!));
                itemData += KvStructSize;
            }
        }

        /// <summary>
        /// Writes <paramref name="s"/> as UTF-8 into a buffer owned by the encoder.
        /// </summary>
        /// <param name="context">the encoding context, which owns the buffers</param>
        /// <param name="s">the string to convert</param>
        /// <param name="applySafety">whether to truncate to <see cref="WafConstants.MaxStringLength"/> so the string fits a pooled block</param>
        /// <param name="writtenBytes">the number of bytes written, which is what libddwaf expects as the string length</param>
        /// <returns>a pointer to the UTF-8 bytes</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe IntPtr ConvertToUtf8(ref EncoderContext context, string s, bool applySafety, out int writtenBytes)
        {
            IntPtr unmanagedMemory;
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

            // libddwaf 2.x never assumes NUL termination, but keeping it is free and makes the buffers
            // safe to inspect with C string tooling while debugging
            Marshal.WriteByte(unmanagedMemory, writtenBytes, (byte)'\0');
            context.Buffers.Add(unmanagedMemory);
            return unmanagedMemory;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe DdwafObjectStruct GetStringObject(ref EncoderContext context, string value, bool applySafety)
        {
            var pointer = ConvertToUtf8(ref context, value, applySafety, out var writtenBytes);

            // always a heap string, never a small string: the WAF gets alloc = NULL on evaluation so it
            // never frees nor copies these buffers, and inlining short strings would only save pool churn
            return new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_STRING, Pointer = pointer, StringLength = (uint)writtenBytes };
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
            var canMoveNext = enumerator.MoveNext();
            while (canMoveNext)
            {
                FormatArgsInternal(enumerator.Current as object ?? "null", sb);
                canMoveNext = enumerator.MoveNext();
                if (canMoveNext)
                {
                    sb.Append(", ");
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
                Truncated = false;
            }

            public bool Truncated { get; set; }
        }

        public sealed class EncodeResult : IEncodeResult
        {
            private readonly List<IntPtr> _pointers;
            private readonly UnmanagedMemoryPool _innerPool;
            private DdwafObjectStruct _result;

            internal EncodeResult(List<IntPtr> pointers, UnmanagedMemoryPool pool, ref DdwafObjectStruct result, bool truncated)
            {
                _pointers = pointers;
                _innerPool = pool;
                _result = result;
                Truncated = truncated;
            }

            public DdwafObjectStruct ResultDdwafObject => _result;

            public bool Truncated { get; }

            public void Dispose()
            {
                _innerPool.Return(_pointers);
                _pointers.Clear();
            }
        }
    }
}
