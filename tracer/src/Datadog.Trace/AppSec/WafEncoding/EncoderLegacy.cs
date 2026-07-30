// <copyright file="EncoderLegacy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.WafEncoding;

/// <summary>
/// Encoder that delegates the construction of every WAF object to libddwaf itself.
///
/// Since libddwaf 2.0 there are no standalone objects to assemble: a container is created with a
/// capacity and then <c>ddwaf_object_insert</c> / <c>ddwaf_object_insert_key</c> hand back a pointer
/// to the slot to fill in. Encoding is therefore a top down walk that writes into the destination
/// slot given by the parent, rather than a bottom up one that returns objects by value.
/// </summary>
internal sealed class EncoderLegacy : IEncoder
{
    /// <summary>
    /// Worst case UTF-8 size of a string that abides by the safety limits, which is the size the
    /// per-thread conversion buffer is kept at.
    /// </summary>
    private const int MaxBytesForMaxStringLength = (WafConstants.MaxStringLength * 4) + 1;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EncoderLegacy));

    [ThreadStatic]
    private static byte[]? _utf8Buffer;

    private readonly WafLibraryInvoker _wafLibraryInvoker;

    public EncoderLegacy(WafLibraryInvoker wafLibraryInvoker)
    {
        _wafLibraryInvoker = wafLibraryInvoker;
    }

    private static string TruncateLongString(string s) => s.Length > WafConstants.MaxStringLength ? s.Substring(0, WafConstants.MaxStringLength) : s;

    public unsafe IEncodeResult Encode<TInstance>(TInstance? o, int remainingDepth = WafConstants.MaxContainerDepth, bool applySafetyLimits = true)
    {
        // The root is the only object we own outright: everything below it lives inside a container
        // allocated by libddwaf. It starts out invalid so that disposing is safe even if encoding
        // bails out before writing anything.
        DdwafObjectStruct root = default;
        EncodeInternal(o, &root, remainingDepth, applySafetyLimits, _wafLibraryInvoker);
        return new EncodeResult(root, _wafLibraryInvoker);
    }

    /// <summary>
    /// Clamps a container's element count to what a WAF object can address. Both the size and the
    /// capacity of arrays and maps are 16 bit, and libddwaf does not guard against overflowing them:
    /// its grow helper saturates at 65535 and the following insert writes past the end of the buffer.
    /// </summary>
    private static ushort ClampCapacity(int count)
    {
        if (count <= DdwafObjectStruct.MaxContainerCapacity)
        {
            return (ushort)count;
        }

        TelemetryFactory.Metrics.RecordCountInputTruncated(MetricTags.TruncationReason.ListOrMapTooLarge);
        Log.Warning<int, int>("Container holds {Count} entries, more than the {MaxContainerCapacity} a WAF object can address, it will be truncated", count, DdwafObjectStruct.MaxContainerCapacity);
        return (ushort)DdwafObjectStruct.MaxContainerCapacity;
    }

    private static unsafe void EncodeUnknownType(object? o, DdwafObjectStruct* dest, WafLibraryInvoker wafLibraryInvoker)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Couldn't encode object of unknown type {Type}, falling back to ToString", o?.GetType());
        }

        var s = o?.ToString() ?? string.Empty;
        CreateNativeString(s, dest, applyLimits: true, wafLibraryInvoker);
    }

    private static unsafe void EncodeInternal<T>(T o, DdwafObjectStruct* dest, int remainingDepth, bool applyLimits, WafLibraryInvoker wafLibraryInvoker)
    {
        object? args = o;
        switch (args)
        {
            case null:
                wafLibraryInvoker.ObjectSetNull(dest);
                break;
            case string s:
                CreateNativeString(s, dest, applyLimits, wafLibraryInvoker);
                break;
            case JValue jv:
                EncodeInternal(jv.Value, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case int i:
                wafLibraryInvoker.ObjectSetSigned(dest, i);
                break;
            case uint i:
                wafLibraryInvoker.ObjectSetUnsigned(dest, i);
                break;
            case long i:
                wafLibraryInvoker.ObjectSetSigned(dest, i);
                break;
            case ulong i:
                wafLibraryInvoker.ObjectSetUnsigned(dest, i);
                break;
            case float i:
                wafLibraryInvoker.ObjectSetFloat(dest, i);
                break;
            case double i:
                wafLibraryInvoker.ObjectSetFloat(dest, i);
                break;
            case decimal i:
                wafLibraryInvoker.ObjectSetFloat(dest, (double)i);
                break;
            case bool b:
                wafLibraryInvoker.ObjectSetBool(dest, b);
                break;
            case IEnumerable<KeyValuePair<string, JToken>> objDict:
                EncodeDictionary(objDict, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IEnumerable<KeyValuePair<string, int>> objDict:
                EncodeDictionary(objDict, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IEnumerable<KeyValuePair<string, uint>> objDict:
                EncodeDictionary(objDict, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IEnumerable<KeyValuePair<string, long>> objDict:
                EncodeDictionary(objDict, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IEnumerable<KeyValuePair<string, float>> objDict:
                EncodeDictionary(objDict, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IEnumerable<KeyValuePair<string, double>> objDict:
                EncodeDictionary(objDict, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IEnumerable<KeyValuePair<string, decimal>> objDict:
                EncodeDictionary(objDict, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IEnumerable<KeyValuePair<string, string>> objDict:
                EncodeDictionary(objDict, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IEnumerable<KeyValuePair<string, object>> objDict:
                EncodeDictionary(objDict, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IEnumerable<KeyValuePair<string, bool>> objDict:
                EncodeDictionary(objDict, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IEnumerable<KeyValuePair<string, List<string>>> objDict:
                EncodeDictionary(objDict, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IEnumerable<KeyValuePair<string, ArrayList>> objDict:
                EncodeDictionary(objDict, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IEnumerable<KeyValuePair<string, string[]>> objDict:
                EncodeDictionary(objDict, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IEnumerable<KeyValuePair<string, List<double>>> objDict:
                EncodeDictionary(objDict, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IEnumerable<KeyValuePair<string, double[]>> objDict:
                EncodeDictionary(objDict, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IList<JToken> objs:
                EncodeList(objs, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IList<string> objs:
                EncodeList(objs, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IList<object> objs:
                EncodeList(objs, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IList<int> objs:
                EncodeList(objs, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IList<float> objs:
                EncodeList(objs, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IList<uint> objs:
                EncodeList(objs, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IList<long> objs:
                EncodeList(objs, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IList<ulong> objs:
                EncodeList(objs, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IList<double> objs:
                EncodeList(objs, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case IList<decimal> objs:
                EncodeList(objs, dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            case ArrayList objs:
                EncodeList(objs.ToArray(), dest, remainingDepth, applyLimits, wafLibraryInvoker);
                break;
            default:
                EncodeUnknownType(args, dest, wafLibraryInvoker);
                break;
        }
    }

    private static unsafe void EncodeList<T>(IEnumerable<T> objEnumerator, DdwafObjectStruct* dest, int remainingDepth, bool applyLimits, WafLibraryInvoker wafLibraryInvoker)
    {
        if (applyLimits && remainingDepth-- <= 0)
        {
            TelemetryFactory.Metrics.RecordCountInputTruncated(MetricTags.TruncationReason.ObjectTooDeep);
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("EncodeList: object graph too deep, truncating nesting {Items}", string.Join(", ", objEnumerator));
            }

            wafLibraryInvoker.ObjectSetArray(dest, 0);
            return;
        }

        var count = objEnumerator is IList<T> objs ? objs.Count : objEnumerator.Count();
        if (applyLimits && count > WafConstants.MaxContainerSize)
        {
            TelemetryFactory.Metrics.RecordCountInputTruncated(MetricTags.TruncationReason.ListOrMapTooLarge);
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug<int, int>("EncodeList: list too long, it will be truncated, count: {Count}, MaxMapOrArrayLength {MaxMapOrArrayLength}", count, WafConstants.MaxContainerSize);
            }

            objEnumerator = objEnumerator.Take(WafConstants.MaxContainerSize);
            count = WafConstants.MaxContainerSize;
        }

        // sizing the array up front means no insert ever has to grow it, which is both cheaper and
        // the only way to stay clear of libddwaf's 16 bit overflow when growing
        var capacity = ClampCapacity(count);
        if (capacity < count)
        {
            objEnumerator = objEnumerator.Take(capacity);
        }

        wafLibraryInvoker.ObjectSetArray(dest, capacity);

        foreach (var o in objEnumerator)
        {
            var slot = wafLibraryInvoker.ObjectInsert(dest);
            if (slot == null)
            {
                Log.Warning("EncodeList: couldn't insert an element in the WAF array, the list will be incomplete");
                break;
            }

            EncodeInternal(o, slot, remainingDepth, applyLimits, wafLibraryInvoker);
        }
    }

    private static unsafe void EncodeDictionary<T>(IEnumerable<KeyValuePair<string, T>> objDictEnumerator, DdwafObjectStruct* dest, int remainingDepth, bool applyLimits, WafLibraryInvoker wafLibraryInvoker)
    {
        if (applyLimits && remainingDepth-- <= 0)
        {
            TelemetryFactory.Metrics.RecordCountInputTruncated(MetricTags.TruncationReason.ObjectTooDeep);
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("EncodeDictionary: object graph too deep, truncating nesting {Items}", string.Join(", ", objDictEnumerator.Select(x => $"{x.Key}, {x.Value}")));
            }

            wafLibraryInvoker.ObjectSetMap(dest, 0);
            return;
        }

        var count = objDictEnumerator is IDictionary<string, T> objDict ? objDict.Count : objDictEnumerator.Count();

        if (applyLimits && count > WafConstants.MaxContainerSize)
        {
            TelemetryFactory.Metrics.RecordCountInputTruncated(MetricTags.TruncationReason.ListOrMapTooLarge);
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug<int, int>("EncodeDictionary: list too long, it will be truncated, count: {Count}, MaxMapOrArrayLength {MaxMapOrArrayLength}", count, WafConstants.MaxContainerSize);
            }

            objDictEnumerator = objDictEnumerator.Take(WafConstants.MaxContainerSize);
            count = WafConstants.MaxContainerSize;
        }

        var capacity = ClampCapacity(count);
        if (capacity < count)
        {
            objDictEnumerator = objDictEnumerator.Take(capacity);
        }

        wafLibraryInvoker.ObjectSetMap(dest, capacity);

        foreach (var o in objDictEnumerator)
        {
            var name = o.Key;
            if (!StringUtil.IsNullOrEmpty(name))
            {
                var keyBytes = ToUtf8(name, out var keyLength);
                var slot = wafLibraryInvoker.ObjectInsertKey(dest, keyBytes, keyLength);
                if (slot == null)
                {
                    Log.Warning("EncodeDictionary: couldn't insert a key in the WAF map, the map will be incomplete");
                    break;
                }

                EncodeInternal(o.Value, slot, remainingDepth, applyLimits, wafLibraryInvoker);
            }
            else
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("EncodeDictionary: ignoring dictionary member with null name");
                }
            }
        }
    }

    private static unsafe void CreateNativeString(string s, DdwafObjectStruct* dest, bool applyLimits, WafLibraryInvoker wafLibraryInvoker)
    {
        var encodeString =
            applyLimits
                ? TruncateLongString(s)
                : s;

        // libddwaf takes an explicit byte count, so the string has to be converted to UTF-8 here
        // rather than left to the default (ANSI) marshalling, whose byte count wouldn't match the
        // character count we'd be passing along with it.
        var bytes = ToUtf8(encodeString, out var length);
        wafLibraryInvoker.ObjectSetString(dest, bytes, length);
    }

    /// <summary>
    /// Converts <paramref name="s"/> to UTF-8 into a buffer that is only valid until the next call on
    /// the same thread. libddwaf copies the bytes of the keys and strings it is handed, so nothing has
    /// to outlive the native call that consumes them and one buffer per thread is enough. Encoding a
    /// whole tree therefore costs no managed allocation, which matters because every string and every
    /// map key of every request goes through here.
    /// </summary>
    /// <param name="s">the string to convert</param>
    /// <param name="length">the number of bytes written, which is what libddwaf expects as the length</param>
    /// <returns>the buffer holding the bytes, of which only the first <paramref name="length"/> are meaningful</returns>
    private static byte[] ToUtf8(string s, out uint length)
    {
        if (StringEncoding.UTF8.GetMaxByteCount(s.Length) > MaxBytesForMaxStringLength)
        {
            // only reachable with the safety limits off, i.e. when encoding a ruleset at initialisation:
            // convert it on its own rather than keeping an arbitrarily large buffer alive per thread
            var oneOff = StringEncoding.UTF8.GetBytes(s);
            length = (uint)oneOff.Length;
            return oneOff;
        }

        var buffer = _utf8Buffer ??= new byte[MaxBytesForMaxStringLength];
        length = (uint)StringEncoding.UTF8.GetBytes(s, 0, s.Length, buffer, 0);
        return buffer;
    }

    public static string FormatArgs(object o)
    {
        var sb = StringBuilderCache.Acquire();
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
                float i => sb.Append(i),
                long i => sb.Append(i),
                uint i => sb.Append(i),
                ulong i => sb.Append(i),
                double i => sb.Append(i),
                IEnumerable<KeyValuePair<string, JToken>> objDict => FormatDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), sb),
                IEnumerable<KeyValuePair<string, string>> objDict => FormatDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), sb),
                IEnumerable<KeyValuePair<string, List<string>>> objDict => FormatDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), sb),
                // dont remove IEnumerable<KeyValuePair<string, string[]>>, it is used for logging cookies which are this type in debug mode
                IEnumerable<KeyValuePair<string, string[]>> objDict => FormatDictionary(objDict.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)), sb),
                IEnumerable<KeyValuePair<string, object>> objDict => FormatDictionary(objDict, sb),
                IList<JToken> objs => FormatList(objs, sb),
                IList<string> objs => FormatList(objs, sb),
                // this becomes ugly but this should change once PR improving marshalling of the waf is merged
                IList<long> objs => FormatList(objs, sb),
                IList<ulong> objs => FormatList(objs, sb),
                IList<int> objs => FormatList(objs, sb),
                IList<uint> objs => FormatList(objs, sb),
                IList<double> objs => FormatList(objs, sb),
                IList<decimal> objs => FormatList(objs, sb),
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

    private sealed class EncodeResult : IEncodeResult
    {
        private readonly WafLibraryInvoker _wafLibraryInvoker;
        private DdwafObjectStruct _resultDdwafObject;

        internal EncodeResult(DdwafObjectStruct obj, WafLibraryInvoker wafLibraryInvoker)
        {
            _resultDdwafObject = obj;
            _wafLibraryInvoker = wafLibraryInvoker;
        }

        public DdwafObjectStruct ResultDdwafObject => _resultDdwafObject;

        public bool Truncated => false;

        // the whole tree was built with the default allocator, so that is the one that must free it
        public void Dispose() => _wafLibraryInvoker.ObjectDestroy(ref _resultDdwafObject);
    }
}
