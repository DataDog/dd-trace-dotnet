// <copyright file="MessagePackStringCacheTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Agent.MessagePack;
using FluentAssertions;
using MessagePack;
using Xunit;

namespace Datadog.Trace.Tests.Agent.MessagePack;

public class MessagePackStringCacheTests : IDisposable
{
    public MessagePackStringCacheTests()
    {
        MessagePackStringCache.Clear();
    }

    public enum FuncType
    {
        /// <summary>
        /// MessagePackStringCache.GetEnvironmentBytes
        /// </summary>
        GetEnvironmentBytes,

        /// <summary>
        /// MessagePackStringCache.GetVersionBytes
        /// </summary>
        GetVersionBytes,

        /// <summary>
        /// MessagePackStringCache.GetOriginBytes
        /// </summary>
        GetOriginBytes,
    }

    public static TheoryData<string, FuncType> Data
        => new()
       {
           { "test-env", FuncType.GetEnvironmentBytes },
           { "test-version", FuncType.GetVersionBytes },
           { "test-origin", FuncType.GetOriginBytes }
       };

    public static Func<string, byte[]> GetFunc(FuncType type)
        => type switch
        {
            FuncType.GetEnvironmentBytes => MessagePackStringCache.GetEnvironmentBytes,
            FuncType.GetVersionBytes => MessagePackStringCache.GetVersionBytes,
            FuncType.GetOriginBytes => MessagePackStringCache.GetOriginBytes,
            _ => throw new Exception("Unknown type " + type),
        };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t")]
    public void ReturnsNull(string value)
    {
        MessagePackStringCache.GetEnvironmentBytes(value).Should().BeNull();
        MessagePackStringCache.GetVersionBytes(value).Should().BeNull();
        MessagePackStringCache.GetOriginBytes(value).Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(Data))]
    public void Clear(string value, FuncType funcType)
    {
        var func = GetFunc(funcType);
        var bytes1 = func(value);
        MessagePackStringCache.Clear();
        var bytes2 = func(value);

        // different references with same contents
        bytes2.Should().NotBeSameAs(bytes1);
        bytes2.Should().BeEquivalentTo(bytes1);
    }

    [Theory]
    [MemberData(nameof(Data))]
    public void Serialized(string value, FuncType funcType)
    {
        var func = GetFunc(funcType);
        var bytes = func(value);
        var deserializedString = MessagePackSerializer.Deserialize<string>(bytes);

        deserializedString.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Data))]
    public void Same_Value_Same_Thread(string value, FuncType funcType)
    {
        var func = GetFunc(funcType);
        var bytes1 = func(value);
        var bytes2 = func(value);

        // same reference
        bytes2.Should().BeSameAs(bytes1);
    }

    [Theory]
    [MemberData(nameof(Data))]
    public void Same_Value_Different_Threads(string value, FuncType funcType)
    {
        var func = GetFunc(funcType);
        var bytes1 = func(value);
        var bytes2 = (byte[])null;

        var thread = new Thread(() => { bytes2 = MessagePackStringCache.GetEnvironmentBytes(value); });
        thread.Start();
        thread.Join();

        // different references with same contents
        bytes2.Should().NotBeSameAs(bytes1);
        bytes2.Should().BeEquivalentTo(bytes1);
    }

    [Theory]
    [MemberData(nameof(Data))]
    public void Different_Values_In_Same_Thread(string value, FuncType funcType)
    {
        var func = GetFunc(funcType);
        var otherValue = value + "-1";

        var bytes1 = func(value);
        var bytes2 = func(otherValue);

        // different values
        bytes2.Should().NotBeEquivalentTo(bytes1);

        var deserializedString1 = MessagePackSerializer.Deserialize<string>(bytes1);
        deserializedString1.Should().Be(value);

        var deserializedString2 = MessagePackSerializer.Deserialize<string>(bytes2);
        deserializedString2.Should().Be(otherValue);
    }

    [Theory]
    [MemberData(nameof(Data))]
    public void Separate_Cache_Per_Thread(string value, FuncType funcType)
    {
        var func = GetFunc(funcType);
        var bytes1 = func(value);
        var bytes2 = (byte[])null;
        var bytes3 = (byte[])null;

        var thread = new Thread(
            () =>
            {
                bytes2 = MessagePackStringCache.GetEnvironmentBytes(value);
                bytes3 = MessagePackStringCache.GetEnvironmentBytes(value);
            });

        thread.Start();
        thread.Join();

        var bytes4 = func(value);

        // same references
        bytes4.Should().BeSameAs(bytes1);
        bytes3.Should().BeSameAs(bytes2);

        // different references with same contents
        bytes2.Should().NotBeSameAs(bytes1);
        bytes2.Should().BeEquivalentTo(bytes1);
    }

    public void Dispose()
    {
        MessagePackStringCache.Clear();
    }
}
