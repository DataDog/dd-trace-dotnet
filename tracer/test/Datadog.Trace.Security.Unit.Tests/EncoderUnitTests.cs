// <copyright file="EncoderUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Xunit;
using Encoder = Datadog.Trace.AppSec.Waf.Encoder;

namespace Datadog.Trace.Security.Unit.Tests;

public class EncoderUnitTests
{
    [SkippableTheory]
    [InlineData(WafConstants.MaxStringLength - 1, WafConstants.MaxStringLength - 1)]
    [InlineData(WafConstants.MaxStringLength, WafConstants.MaxStringLength)]
    [InlineData(WafConstants.MaxStringLength + 1, WafConstants.MaxStringLength)]
    public void TestStringLength(int length, int expectedLength)
    {
        var l = new List<GCHandle>();
        var target = new string('c', length);

        var intermediate = Encoder.Encode(target, argToFree: l, applySafetyLimits: true);
        var result = Encoder.Decode(intermediate) as string;

        Assert.NotNull(result);
        Assert.Equal(expectedLength, result.Length);

        Dispose(l);
    }

    [SkippableTheory]
    [InlineData(WafConstants.MaxContainerSize - 1, WafConstants.MaxContainerSize - 1)]
    [InlineData(WafConstants.MaxContainerSize, WafConstants.MaxContainerSize)]
    [InlineData(WafConstants.MaxContainerSize + 1, WafConstants.MaxContainerSize)]
    public void TestArrayLength(int length, int expectedLength)
    {
        var l = new List<GCHandle>();

        var target = Enumerable.Repeat((object)"test", length).ToList();

        var intermediate = Encoder.Encode(target, l, applySafetyLimits: true);
        var result = Encoder.Decode(intermediate) as object[];

        Assert.NotNull(result);
        Assert.Equal(expectedLength, result.Length);

        Dispose(l);
    }

    [SkippableTheory]
    [InlineData(WafConstants.MaxContainerSize - 1, WafConstants.MaxContainerSize - 1)]
    [InlineData(WafConstants.MaxContainerSize, WafConstants.MaxContainerSize)]
    [InlineData(WafConstants.MaxContainerSize + 1, WafConstants.MaxContainerSize)]
    public void TestMapLength(int length, int expectedLength)
    {
        var l = new List<GCHandle>();

        var target = Enumerable.Range(0, length).ToDictionary(x => x.ToString(), _ => (object)"test");

        var intermediate = Encoder.Encode(target, l, applySafetyLimits: true);
        var result = Encoder.Decode(intermediate) as Dictionary<string, object>;

        Assert.NotNull(result);
        Assert.Equal(expectedLength, result.Count);

        Dispose(l);
    }

    [SkippableTheory]
    [InlineData(WafConstants.MaxContainerDepth - 1, WafConstants.MaxContainerDepth - 1)]
    [InlineData(WafConstants.MaxContainerDepth, WafConstants.MaxContainerDepth)]
    [InlineData(WafConstants.MaxContainerDepth + 1, WafConstants.MaxContainerDepth)]
    public void TestNestedListDepth(int length, int expectedLength)
    {
        var l = new List<GCHandle>();

        var target = MakeNestedList(length);

        var intermediate = Encoder.Encode(target, l, applySafetyLimits: true);
        var result = Encoder.Decode(intermediate) as object[];

        Assert.NotNull(result);
        Assert.Equal(expectedLength, CountNestedListDepth(result));

        Dispose(l);
    }

    [SkippableTheory]
    [InlineData(WafConstants.MaxContainerDepth - 1, WafConstants.MaxContainerDepth - 1)]
    [InlineData(WafConstants.MaxContainerDepth, WafConstants.MaxContainerDepth)]
    [InlineData(WafConstants.MaxContainerDepth + 1, WafConstants.MaxContainerDepth)]
    public void TestMapListDepth(int length, int expectedLength)
    {
        var l = new List<GCHandle>();

        var target = MakeNestedMap(length);

        var intermediate = Encoder.Encode(target, l, applySafetyLimits: true);
        var result = Encoder.Decode(intermediate) as Dictionary<string, object>;

        Assert.NotNull(result);
        Assert.Equal(expectedLength, CountNestedMapDepth(result));

        Dispose(l);
    }

    private static void Dispose(List<GCHandle> l)
    {
        foreach (var gcHandle in l)
        {
            gcHandle.Free();
        }
    }

    private static List<object> MakeNestedList(int nestingDepth)
    {
        var root = new List<object>();
        var list = root;

        for (var i = 0; i < nestingDepth; i++)
        {
            var nextList = new List<object>();
            list.Add(nextList);
            list = nextList;
        }

        return root;
    }

    private static int CountNestedListDepth(IList<object> list, int count = 0)
    {
        if (list.Count > 0)
        {
            // we know our tests lists are always nested in the first item
            count = CountNestedListDepth((IList<object>)list[0], count + 1);
        }

        return count;
    }

    private static Dictionary<string, object> MakeNestedMap(int nestingDepth)
    {
        var root = new Dictionary<string, object>();
        var map = root;

        for (int i = 0; i < nestingDepth; i++)
        {
            var nextMap = new Dictionary<string, object>();
            map.Add("item", nextMap);
            map = nextMap;
        }

        return root;
    }

    private static int CountNestedMapDepth(Dictionary<string, object> map, int count = 0)
    {
        if (map.Count > 0)
        {
            // we know our tests lists are always nested in the under the item key
            count = CountNestedMapDepth((Dictionary<string, object>)map["item"], count + 1);
        }

        return count;
    }
}
