// <copyright file="EncoderLegacy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Waf.NativeBindings;

internal class EncoderLegacy
{
    private readonly WafLibraryInvoker _wafLibraryInvoker;

    public EncoderLegacy(WafLibraryInvoker wafLibraryInvoker)
    {
        _wafLibraryInvoker = wafLibraryInvoker;
    }

    private static string TruncateLongString(string s) => s.Length > WafConstants.MaxStringLength ? s.Substring(0, WafConstants.MaxStringLength) : s;

    public DdwafObjectStruct Encode<TInstance>(TInstance? o, int remainingDepth = WafConstants.MaxContainerDepth, string? key = null, bool applySafetyLimits = true)
    {
        var result = EncodeInternal(o, remainingDepth, applySafetyLimits, _wafLibraryInvoker, parentObj: true);
        return result;
    }

    private static DdwafObjectStruct EncodeUnknownType(object? o, WafLibraryInvoker wafLibraryInvoker)
    {
        Console.WriteLine("Couldn't encode object of unknown type {Type}, falling back to ToString", o?.GetType());

        var s = o?.ToString() ?? string.Empty;
        return CreateNativeString(s, applyLimits: true, wafLibraryInvoker);
    }

    private static DdwafObjectStruct EncodeInternal<T>(T o, int remainingDepth, bool applyLimits, WafLibraryInvoker wafLibraryInvoker, bool parentObj = false)
    {
        object args = o!;
        var value =
            args switch
            {
                null => CreateNativeNull(wafLibraryInvoker),
                string s => CreateNativeString(s, applyLimits, wafLibraryInvoker),
                JValue jv => CreateNativeString(jv.Value?.ToString() ?? string.Empty, applyLimits, wafLibraryInvoker),
                int i => CreateNativeLong(i, wafLibraryInvoker),
                uint i => CreateNativeUlong(i, wafLibraryInvoker),
                long i => CreateNativeLong(i, wafLibraryInvoker),
                ulong i => CreateNativeUlong(i, wafLibraryInvoker),
                float i => CreateNativeDouble(i, wafLibraryInvoker),
                double i => CreateNativeDouble(i, wafLibraryInvoker),
                decimal i => CreateNativeDouble((double)i, wafLibraryInvoker),
                bool b => CreateNativeBool(b, wafLibraryInvoker),
                IEnumerable<KeyValuePair<string, JToken>> objDict => EncodeDictionary(objDict, remainingDepth, applyLimits, wafLibraryInvoker),
                IEnumerable<KeyValuePair<string, int>> objDict => EncodeDictionary(objDict, remainingDepth, applyLimits, wafLibraryInvoker),
                IEnumerable<KeyValuePair<string, uint>> objDict => EncodeDictionary(objDict, remainingDepth, applyLimits, wafLibraryInvoker),
                IEnumerable<KeyValuePair<string, long>> objDict => EncodeDictionary(objDict, remainingDepth, applyLimits, wafLibraryInvoker),
                IEnumerable<KeyValuePair<string, float>> objDict => EncodeDictionary(objDict, remainingDepth, applyLimits, wafLibraryInvoker),
                IEnumerable<KeyValuePair<string, double>> objDict => EncodeDictionary(objDict, remainingDepth, applyLimits, wafLibraryInvoker),
                IEnumerable<KeyValuePair<string, decimal>> objDict => EncodeDictionary(objDict, remainingDepth, applyLimits, wafLibraryInvoker),
                IEnumerable<KeyValuePair<string, string>> objDict => EncodeDictionary(objDict, remainingDepth, applyLimits, wafLibraryInvoker),
                IEnumerable<KeyValuePair<string, object>> objDict => EncodeDictionary(objDict, remainingDepth, applyLimits, wafLibraryInvoker),
                IEnumerable<KeyValuePair<string, bool>> objDict => EncodeDictionary(objDict, remainingDepth, applyLimits, wafLibraryInvoker),
                IEnumerable<KeyValuePair<string, List<string>>> objDict => EncodeDictionary(objDict, remainingDepth, applyLimits, wafLibraryInvoker),
                IEnumerable<KeyValuePair<string, ArrayList>> objDict => EncodeDictionary(objDict, remainingDepth, applyLimits, wafLibraryInvoker),
                IEnumerable<KeyValuePair<string, string[]>> objDict => EncodeDictionary(objDict, remainingDepth, applyLimits, wafLibraryInvoker),
                IEnumerable<KeyValuePair<string, List<double>>> objDict => EncodeDictionary(objDict, remainingDepth, applyLimits, wafLibraryInvoker),
                IEnumerable<KeyValuePair<string, double[]>> objDict => EncodeDictionary(objDict, remainingDepth, applyLimits, wafLibraryInvoker),
                IList<JToken> objs => EncodeList(objs, remainingDepth, applyLimits, wafLibraryInvoker),
                IList<string> objs => EncodeList(objs, remainingDepth, applyLimits, wafLibraryInvoker),
                IList<object> objs => EncodeList(objs, remainingDepth, applyLimits, wafLibraryInvoker),
                IList<int> objs => EncodeList(objs, remainingDepth, applyLimits, wafLibraryInvoker),
                IList<float> objs => EncodeList(objs, remainingDepth, applyLimits, wafLibraryInvoker),
                IList<uint> objs => EncodeList(objs, remainingDepth, applyLimits, wafLibraryInvoker),
                IList<long> objs => EncodeList(objs, remainingDepth, applyLimits, wafLibraryInvoker),
                IList<ulong> objs => EncodeList(objs, remainingDepth, applyLimits, wafLibraryInvoker),
                IList<double> objs => EncodeList(objs, remainingDepth, applyLimits, wafLibraryInvoker),
                IList<decimal> objs => EncodeList(objs, remainingDepth, applyLimits, wafLibraryInvoker),
                ArrayList objs => EncodeList(objs.ToArray(), remainingDepth, applyLimits, wafLibraryInvoker),
                _ => EncodeUnknownType(args, wafLibraryInvoker)
            };

        return value;
    }

    private static DdwafObjectStruct EncodeList<T>(IEnumerable<T> objEnumerator, int remainingDepth, bool applyLimits, WafLibraryInvoker wafLibraryInvoker)
    {
        var arrNat = wafLibraryInvoker.ObjectArray();

        if (applyLimits && remainingDepth-- <= 0)
        {
            Console.WriteLine("EncodeList: object graph too deep, truncating nesting {Items}", string.Join(", ", objEnumerator));

            return arrNat;
        }

        var count = objEnumerator is IList<object> objs ? objs.Count : objEnumerator.Count();
        if (applyLimits && count > WafConstants.MaxContainerSize)
        {
            Console.WriteLine("EncodeList: list too long, it will be truncated, count: {Count}, MaxMapOrArrayLength {MaxMapOrArrayLength}", count, WafConstants.MaxContainerSize);

            objEnumerator = objEnumerator.Take(WafConstants.MaxContainerSize);
        }

        foreach (var o in objEnumerator)
        {
            var value = EncodeInternal(o, remainingDepth, applyLimits, wafLibraryInvoker);
            wafLibraryInvoker.ObjectArrayAdd(ref arrNat, ref value);
        }

        return arrNat;
    }

    private static DdwafObjectStruct EncodeDictionary<T>(IEnumerable<KeyValuePair<string, T>> objDictEnumerator, int remainingDepth, bool applyLimits, WafLibraryInvoker wafLibraryInvoker)
    {
        var mapNat = wafLibraryInvoker.ObjectMap();

        if (applyLimits && remainingDepth-- <= 0)
        {
            Console.WriteLine("EncodeDictionary: object graph too deep, truncating nesting {Items}", string.Join(", ", objDictEnumerator.Select(x => $"{x.Key}, {x.Value}")));

            return mapNat;
        }

        var count = objDictEnumerator is IDictionary<string, object> objDict ? objDict.Count : objDictEnumerator.Count();

        if (applyLimits && count > WafConstants.MaxContainerSize)
        {
            Console.WriteLine("EncodeDictionary: list too long, it will be truncated, count: {Count}, MaxMapOrArrayLength {MaxMapOrArrayLength}", count, WafConstants.MaxContainerSize);

            objDictEnumerator = objDictEnumerator.Take(WafConstants.MaxContainerSize);
        }

        foreach (var o in objDictEnumerator)
        {
            var name = o.Key;
            if (name != null)
            {
                var value = EncodeInternal(o.Value, remainingDepth, applyLimits, wafLibraryInvoker);
                wafLibraryInvoker.ObjectMapAdd(ref mapNat, name, Convert.ToUInt64(name.Length), ref value);
            }
            else
            {
                Console.WriteLine("EncodeDictionary: ignoring dictionary member with null name");
            }
        }

        return mapNat;
    }

    private static DdwafObjectStruct CreateNativeString(string s, bool applyLimits, WafLibraryInvoker wafLibraryInvoker)
    {
        var encodeString =
            applyLimits
                ? TruncateLongString(s)
                : s;
        var objectStringLength = wafLibraryInvoker.ObjectStringLength(encodeString, Convert.ToUInt64(encodeString.Length));
        return objectStringLength;
    }

    private static DdwafObjectStruct CreateNativeBool(bool b, WafLibraryInvoker wafLibraryInvoker)
    {
        var ddwafObjectStruct = wafLibraryInvoker.ObjectBool(b);
        return ddwafObjectStruct;
    }

    private static DdwafObjectStruct CreateNativeLong(long value, WafLibraryInvoker wafLibraryInvoker)
    {
        var ddwafObjectStruct = wafLibraryInvoker.ObjectLong(value);
        return ddwafObjectStruct;
    }

    private static DdwafObjectStruct CreateNativeNull(WafLibraryInvoker wafLibraryInvoker)
    {
        var ddwafObjectStruct = wafLibraryInvoker.ObjectNull();
        return ddwafObjectStruct;
    }

    private static DdwafObjectStruct CreateNativeUlong(ulong value, WafLibraryInvoker wafLibraryInvoker)
    {
        var ddwafObjectStruct = wafLibraryInvoker.ObjectUlong(value);
        return ddwafObjectStruct;
    }

    private static DdwafObjectStruct CreateNativeDouble(double value, WafLibraryInvoker wafLibraryInvoker)
    {
        var ddwafObjectStruct = wafLibraryInvoker.ObjectDouble(value);
        return ddwafObjectStruct;
    }
}
