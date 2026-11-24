// <copyright file="ConcurrentDictionaryExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;
using Datadog.Trace.ExtensionMethods;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ExtensionMethods;

// On .NET Framework these exercise the polyfills in ConcurrentDictionaryExtensions; on .NET Core they bind to the
// built-in overloads, which checks the polyfills behave the same way
public class ConcurrentDictionaryExtensionsTests
{
    [Fact]
    public void GetOrAdd_MissingKey_AddsFactoryValue()
    {
        var dictionary = new ConcurrentDictionary<string, int>();

        var value = dictionary.GetOrAdd("key", static (key, arg) => key.Length + arg, 10);

        value.Should().Be(13);
        dictionary.Should().ContainKey("key").WhoseValue.Should().Be(13);
    }

    [Fact]
    public void GetOrAdd_ExistingKey_ReturnsExistingValueWithoutCallingFactory()
    {
        var dictionary = new ConcurrentDictionary<string, int> { ["key"] = 1 };
        var calls = new int[1];

        var value = dictionary.GetOrAdd(
            "key",
            static (_, callCount) =>
            {
                callCount[0]++;
                return 2;
            },
            calls);

        value.Should().Be(1);
        calls[0].Should().Be(0);
    }

    [Fact]
    public void AddOrUpdate_MissingKey_AddsAddFactoryValue()
    {
        var dictionary = new ConcurrentDictionary<string, int>();

        var value = dictionary.AddOrUpdate("key", static (_, arg) => arg, static (_, existing, arg) => existing + arg, 5);

        value.Should().Be(5);
        dictionary["key"].Should().Be(5);
    }

    [Fact]
    public void AddOrUpdate_ExistingKey_StoresUpdateFactoryValue()
    {
        var dictionary = new ConcurrentDictionary<string, int> { ["key"] = 1 };

        var value = dictionary.AddOrUpdate("key", static (_, arg) => arg, static (_, existing, arg) => existing + arg, 5);

        value.Should().Be(6);
        dictionary["key"].Should().Be(6);
    }
}
