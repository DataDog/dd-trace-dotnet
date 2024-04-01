// <copyright file="EncoderUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.Security.Unit.Tests.Utils;
using FluentAssertions;
using Xunit;
using Encoder = Datadog.Trace.AppSec.WafEncoding.Encoder;

namespace Datadog.Trace.Security.Unit.Tests;

public class EncoderUnitTests : WafLibraryRequiredTest
{
    private readonly IEncoder _encoder;

    public EncoderUnitTests()
    {
        _encoder = new Encoder();
    }

    internal EncoderUnitTests(IEncoder encoder)
    {
        _encoder = encoder;
    }

    [SkippableFact]
    public void TestObject()
    {
        var arrayListStrings = new[] { "test", "test2", "test3" };
        var listStrings = new List<string> { "dog1", "dog2", "dog3" };
        var listDecimals = new List<decimal> { 1.1M, 2.2M, 3.4M };
        var listUlongs = new List<ulong> { 9_223_372_036_854, 9_223_372_036_852 };
        var listFloats = new List<float> { 1.1F, 2.2F, 3.4F };
        var dictionaryStrings = new Dictionary<string, string> { { "key1", "dog1" }, { "key2", "dog2" } };
        var target = new Dictionary<string, object>
        {
            { "ArrayList", new ArrayList(arrayListStrings) },
            { "ListStrings", listStrings },
            { "ListDecimals", listDecimals },
            { "ListUlongs", listUlongs },
            { "ListFloats", listFloats },
            { "Dictionary", dictionaryStrings },
        };
        using var intermediate = _encoder.Encode(
            target,
            applySafetyLimits: true);
        var result = intermediate.ResultDdwafObject.Decode();
        result.Should().NotBeNull();
        result.Should().BeOfType<Dictionary<string, object>>();
        var resultDic = (Dictionary<string, object>)result;
        resultDic.Should().NotBeNull().And.HaveCount(6);
        resultDic.Should().ContainKeys("ArrayList", "ListStrings", "ListDecimals", "ListUlongs", "ListFloats", "Dictionary");
        resultDic!["ArrayList"].Should().BeOfType<List<object>>();
        resultDic["ArrayList"].Should().BeEquivalentTo(arrayListStrings);
        resultDic["ListStrings"].Should().BeOfType<List<object>>();
        resultDic["ListStrings"].Should().BeEquivalentTo(listStrings);
        resultDic["ListDecimals"].Should().BeOfType<List<object>>();
        resultDic["ListDecimals"].Should().BeEquivalentTo(listDecimals);
        resultDic["ListUlongs"].Should().BeOfType<List<object>>();
        resultDic["ListUlongs"].Should().BeEquivalentTo(listUlongs);
        resultDic["ListFloats"].Should().BeOfType<List<object>>();
        resultDic["ListFloats"].Should().BeEquivalentTo(listFloats);
        resultDic["Dictionary"].Should().BeOfType<Dictionary<string, object>>();
        resultDic["Dictionary"].Should().BeEquivalentTo(dictionaryStrings);
    }

    [SkippableTheory]
    [InlineData(WafConstants.MaxStringLength - 1, WafConstants.MaxStringLength - 1)]
    [InlineData(WafConstants.MaxStringLength, WafConstants.MaxStringLength)]
    [InlineData(WafConstants.MaxStringLength + 10000, WafConstants.MaxStringLength)]
    public void TestStringLength(int length, int expectedLength)
    {
        var target = new string('c', length);

        using var intermediate = _encoder.Encode(target, applySafetyLimits: true);
        var result = intermediate.ResultDdwafObject.Decode() as string;

        Assert.NotNull(result);
        Assert.Equal(expectedLength, result.Length);
    }

    [SkippableTheory]
    [InlineData(WafConstants.MaxContainerSize - 1, WafConstants.MaxContainerSize - 1)]
    [InlineData(WafConstants.MaxContainerSize, WafConstants.MaxContainerSize)]
    [InlineData(WafConstants.MaxContainerSize + 10000, WafConstants.MaxContainerSize)]
    public void TestEnumerableLength(int length, int expectedLength)
    {
        var target = Enumerable.Repeat((object)"test", length).ToList();

        using var intermediate = _encoder.Encode(target, applySafetyLimits: true);
        var result = intermediate.ResultDdwafObject.Decode() as List<object>;

        Assert.NotNull(result);
        Assert.Equal(expectedLength, result.Count);
    }

    [SkippableTheory]
    [InlineData(WafConstants.MaxContainerSize - 1, WafConstants.MaxContainerSize - 1)]
    [InlineData(WafConstants.MaxContainerSize, WafConstants.MaxContainerSize)]
    [InlineData(WafConstants.MaxContainerSize + 10000, WafConstants.MaxContainerSize)]
    public void TestArrayListLength(int length, int expectedLength)
    {
        var arrayList = new ArrayList();
        for (var i = 0; i < length; i++)
        {
            arrayList.Add("dog");
        }

        using var intermediate = _encoder.Encode(arrayList, applySafetyLimits: true);
        var result = intermediate.ResultDdwafObject.Decode() as List<object>;

        Assert.NotNull(result);
        Assert.Equal(expectedLength, result.Count);
    }

    [SkippableTheory]
    [InlineData(WafConstants.MaxContainerSize - 1, WafConstants.MaxContainerSize - 1)]
    [InlineData(WafConstants.MaxContainerSize, WafConstants.MaxContainerSize)]
    [InlineData(WafConstants.MaxContainerSize + 10000, WafConstants.MaxContainerSize)]
    public void TestMapLength(int length, int expectedLength)
    {
        var target = Enumerable.Range(0, length).ToDictionary(x => x.ToString(), _ => (object)"test");

        using var intermediate = _encoder.Encode(target, applySafetyLimits: true);
        var result = intermediate.ResultDdwafObject.Decode() as Dictionary<string, object>;

        Assert.NotNull(result);
        Assert.Equal(expectedLength, result.Count);
    }

    [SkippableTheory]
    [InlineData(WafConstants.MaxContainerSize - 1, WafConstants.MaxContainerSize - 1)]
    [InlineData(WafConstants.MaxContainerSize, WafConstants.MaxContainerSize)]
    [InlineData(WafConstants.MaxContainerSize + 10000, WafConstants.MaxContainerSize)]
    public void TestMapNoDictionaryLength(int length, int expectedLength)
    {
        var target = Enumerable.Range(0, length).Select(x => new KeyValuePair<string, object>(x.ToString(), (object)"test"));

        using var intermediate = _encoder.Encode(target, applySafetyLimits: true);
        var result = intermediate.ResultDdwafObject.Decode() as Dictionary<string, object>;

        Assert.NotNull(result);
        Assert.Equal(expectedLength, result.Count);
    }

    [SkippableTheory]
    [InlineData(WafConstants.MaxContainerDepth - 1, WafConstants.MaxContainerDepth - 1)]
    [InlineData(WafConstants.MaxContainerDepth, WafConstants.MaxContainerDepth)]
    [InlineData(WafConstants.MaxContainerDepth + 10000, WafConstants.MaxContainerDepth)]
    public void TestNestedListDepth(int length, int expectedLength)
    {
        var target = MakeNestedList(length);

        using var intermediate = _encoder.Encode(target, applySafetyLimits: true);
        var result = intermediate.ResultDdwafObject.Decode() as List<object>;

        Assert.NotNull(result);
        Assert.Equal(expectedLength, CountNestedListDepth(result));
    }

    [SkippableTheory]
    [InlineData(WafConstants.MaxContainerDepth - 1, WafConstants.MaxContainerDepth - 1)]
    [InlineData(WafConstants.MaxContainerDepth, WafConstants.MaxContainerDepth)]
    [InlineData(WafConstants.MaxContainerDepth + 10000, WafConstants.MaxContainerDepth)]
    public void TestMapListDepth(int length, int expectedLength)
    {
        var target = MakeNestedMap(length);

        using var intermediate = _encoder.Encode(target, applySafetyLimits: true);
        var result = intermediate.ResultDdwafObject.Decode() as Dictionary<string, object>;

        Assert.NotNull(result);
        Assert.Equal(expectedLength, CountNestedMapDepth(result));
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

        for (var i = 0; i < nestingDepth; i++)
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
