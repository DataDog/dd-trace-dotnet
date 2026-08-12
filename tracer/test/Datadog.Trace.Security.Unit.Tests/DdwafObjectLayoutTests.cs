// <copyright file="DdwafObjectLayoutTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.Security.Unit.Tests.Utils;
using FluentAssertions;
using Xunit;
using Encoder = Datadog.Trace.AppSec.WafEncoding.Encoder;

namespace Datadog.Trace.Security.Unit.Tests;

/// <summary>
/// Guards the hand written mirror of the libddwaf 2.x object model. A wrong field offset doesn't fail
/// to compile, it silently corrupts memory, so the layout is asserted here and then confirmed against
/// the real library by having libddwaf itself build objects that we read back.
/// </summary>
public class DdwafObjectLayoutTests : WafLibraryRequiredTest
{
    [Fact]
    public void ObjectIsSixteenBytes() => Marshal.SizeOf<DdwafObjectStruct>().Should().Be(16);

    [Fact]
    public void KeyValueIsTwoObjects() => Marshal.SizeOf<DdwafObjectKvStruct>().Should().Be(32);

    [Theory]
    [InlineData("Type", 0)]
    [InlineData("BoolByte", 1)]
    [InlineData("SmallStringSize", 1)]
    [InlineData("SmallStringData", 2)]
    [InlineData("Size", 2)]
    [InlineData("Capacity", 4)]
    [InlineData("StringLength", 4)]
    [InlineData("Pointer", 8)]
    [InlineData("UintValue", 8)]
    [InlineData("IntValue", 8)]
    [InlineData("DoubleValue", 8)]
    public void ObjectFieldsAreWhereTheNativeUnionPutsThem(string field, int expectedOffset)
        => Marshal.OffsetOf<DdwafObjectStruct>(field).ToInt64().Should().Be(expectedOffset);

    [Theory]
    [InlineData("Key", 0)]
    [InlineData("Value", 16)]
    public void KeyValueFieldsAreWhereTheNativeStructPutsThem(string field, int expectedOffset)
        => Marshal.OffsetOf<DdwafObjectKvStruct>(field).ToInt64().Should().Be(expectedOffset);

    /// <summary>
    /// Round trip in the direction we can check for free: libddwaf builds the object with its own
    /// setters, we decode it. If any offset were wrong this would read garbage rather than the values.
    /// </summary>
    [SkippableFact]
    public unsafe void NativelyBuiltObjectsDecodeBackToTheSameValues()
    {
        Skip.If(WafLibraryInvoker is null, "The WAF library couldn't be loaded");
        var invoker = WafLibraryInvoker!;

        var longString = new string('a', 40);
        var root = default(DdwafObjectStruct);
        try
        {
            var rootPtr = &root;
            invoker.ObjectSetMap(rootPtr, 8);
            SetString(invoker, InsertKey(invoker, rootPtr, "small"), "tiny");
            SetString(invoker, InsertKey(invoker, rootPtr, "smallUtf8"), "héllo→");
            SetString(invoker, InsertKey(invoker, rootPtr, "long"), longString);
            invoker.ObjectSetSigned(InsertKey(invoker, rootPtr, "signed"), -42);
            invoker.ObjectSetUnsigned(InsertKey(invoker, rootPtr, "unsigned"), 42);
            invoker.ObjectSetBool(InsertKey(invoker, rootPtr, "bool"), true);
            invoker.ObjectSetFloat(InsertKey(invoker, rootPtr, "float"), 1.5);

            var nested = InsertKey(invoker, rootPtr, "nested");
            invoker.ObjectSetArray(nested, 2);
            SetString(invoker, invoker.ObjectInsert(nested), "first");
            SetString(invoker, invoker.ObjectInsert(nested), longString);

            root.Type.Should().Be(DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP);
            root.Size.Should().Be(8);
            root.Capacity.Should().Be(8);

            // strings of up to 14 bytes are inlined in the object itself, longer ones are behind a
            // pointer: both must decode identically, which is why the decoder is UTF-8 for both
            ValueOf(root, "small").Type.Should().Be(DDWAF_OBJ_TYPE.DDWAF_OBJ_SMALL_STRING);
            ValueOf(root, "smallUtf8").Type.Should().Be(DDWAF_OBJ_TYPE.DDWAF_OBJ_SMALL_STRING);
            ValueOf(root, "long").Type.Should().Be(DDWAF_OBJ_TYPE.DDWAF_OBJ_STRING);
            ValueOf(root, "long").StringLength.Should().Be(40);

            var decoded = root.DecodeMap();
            decoded.Should().HaveCount(8);
            decoded["small"].Should().Be("tiny");
            decoded["smallUtf8"].Should().Be("héllo→");
            decoded["long"].Should().Be(longString);
            decoded["signed"].Should().Be(-42L);
            decoded["unsigned"].Should().Be(42UL);
            decoded["bool"].Should().Be(true);
            decoded["float"].Should().Be(1.5d);
            decoded["nested"].Should().BeEquivalentTo(new List<object> { "first", longString });
        }
        finally
        {
            invoker.ObjectDestroy(ref root, invoker.DefaultAllocator);
        }
    }

    /// <summary>
    /// The other direction can't be read back with native getters (we don't bind them), but the two
    /// encoders are an equivalent cross check: the legacy one is built by libddwaf's own setters while
    /// the unsafe one writes the structs by hand, so any layout mistake makes them disagree.
    /// </summary>
    [SkippableFact]
    public void BothEncodersProduceEquivalentObjects()
    {
        Skip.If(WafLibraryInvoker is null, "The WAF library couldn't be loaded");

        var target = new Dictionary<string, object>
        {
            { "small", "tiny" },
            { "smallUtf8", "héllo→" },
            { "long", new string('a', 40) },
            { "longUtf8", string.Concat(new string('é', 20), "→") },
            { "signed", -42 },
            { "unsigned", 42UL },
            { "bool", true },
            { "float", 1.5 },
            { "list", new List<object> { "first", "second", 3 } },
            { "map", new Dictionary<string, object> { { "innerKeyThatIsNotSmall", "innerValueThatIsNotSmall" } } },
        };

        using var handWritten = new Encoder().Encode(target, applySafetyLimits: true);
        using var nativelyBuilt = new EncoderLegacy(WafLibraryInvoker!).Encode(target, applySafetyLimits: true);

        var handWrittenDecoded = handWritten.ResultDdwafObject.Decode();
        var nativelyBuiltDecoded = nativelyBuilt.ResultDdwafObject.Decode();

        handWrittenDecoded.Should().BeEquivalentTo(nativelyBuiltDecoded);
    }

    private static unsafe DdwafObjectStruct* InsertKey(WafLibraryInvoker invoker, DdwafObjectStruct* map, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var slot = invoker.ObjectInsertKey(map, keyBytes, (uint)keyBytes.Length);
        if (slot is null)
        {
            throw new InvalidOperationException($"libddwaf refused to add the key {key}, is the map full?");
        }

        return slot;
    }

    private static unsafe void SetString(WafLibraryInvoker invoker, DdwafObjectStruct* slot, string value)
    {
        var valueBytes = Encoding.UTF8.GetBytes(value);
        invoker.ObjectSetString(slot, valueBytes, (uint)valueBytes.Length);
    }

    private static unsafe DdwafObjectStruct ValueOf(DdwafObjectStruct map, string key)
    {
        var entries = (DdwafObjectKvStruct*)map.Pointer;
        for (var i = 0; i < map.Size; i++)
        {
            if (entries[i].Key.DecodeString() == key)
            {
                return entries[i].Value;
            }
        }

        throw new KeyNotFoundException(key);
    }
}
