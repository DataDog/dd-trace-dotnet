// <copyright file="DictionaryExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ExtensionMethods;

public class DictionaryExtensionsTests
{
    [Fact]
    public void SequenceEqual_HandlesBothNull()
    {
        ReadOnlyDictionary<string, string> dict1 = null;
        dict1!.SequenceEqual(null).Should().BeTrue();
    }

    [Fact]
    public void SequenceEqual_HandlesOneNull()
    {
        ReadOnlyDictionary<string, string> dict1 = null;
        ReadOnlyDictionary<string, string> dict2 = ReadOnlyDictionary.Empty;
        dict1!.SequenceEqual(dict2).Should().BeFalse();
        dict2!.SequenceEqual(dict1).Should().BeFalse();
    }

    [Fact]
    public void SequenceEqual_HandlesEmpty()
    {
        ReadOnlyDictionary<string, string> dict1 = ReadOnlyDictionary.Empty;
        ReadOnlyDictionary<string, string> dict2 = ReadOnlyDictionary.Empty;
        dict1.SequenceEqual(dict2).Should().BeTrue();
        dict2.SequenceEqual(dict1).Should().BeTrue();
    }

    [Fact]
    public void SequenceEqual_HandlesDifferentSizes()
    {
        ReadOnlyDictionary<string, string> dict1 = new(new Dictionary<string, string> { { "key", "value" } });
        ReadOnlyDictionary<string, string> dict2 = ReadOnlyDictionary.Empty;
        dict1.SequenceEqual(dict2).Should().BeFalse();
        dict2.SequenceEqual(dict1).Should().BeFalse();
    }

    [Fact]
    public void SequenceEqual_HandlesDifferentKeys()
    {
        ReadOnlyDictionary<string, string> dict1 = new(new Dictionary<string, string> { { "key1", "value" } });
        ReadOnlyDictionary<string, string> dict2 = new(new Dictionary<string, string> { { "key2", "value" } });
        dict1.SequenceEqual(dict2).Should().BeFalse();
        dict2.SequenceEqual(dict1).Should().BeFalse();
    }

    [Theory]
    [InlineData("value2")]
    [InlineData("VALUE1")]
    [InlineData("value1 ")]
    [InlineData(" value1 ")]
    [InlineData("")]
    [InlineData(null)]
    public void SequenceEqual_HandlesDifferentValues(string value)
    {
        ReadOnlyDictionary<string, string> dict1 = new(new Dictionary<string, string> { { "key", "value1" } });
        ReadOnlyDictionary<string, string> dict2 = new(new Dictionary<string, string> { { "key", value } });
        dict1.SequenceEqual(dict2).Should().BeFalse();
        dict2.SequenceEqual(dict1).Should().BeFalse();
    }

    [Fact]
    public void SequenceEqual_UsesDictionaryKeyComparer()
    {
        ReadOnlyDictionary<string, string> dict1 = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "key", "value" } });
        ReadOnlyDictionary<string, string> dict2 = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "KEY", "value" } });
        dict1.SequenceEqual(dict2).Should().BeTrue();
        dict2.SequenceEqual(dict1).Should().BeTrue();
    }

    [Fact]
    public void SequenceEqual_UsesDictionaryWithDifferentComparersIsNotSupported()
    {
        ReadOnlyDictionary<string, string> dict1 = new(new Dictionary<string, string>(StringComparer.Ordinal) { { "key", "value" } });
        ReadOnlyDictionary<string, string> dict2 = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "KEY", "value" } });

        // reversing the order changes the result
        dict1.SequenceEqual(dict2).Should().BeTrue();
        dict2.SequenceEqual(dict1).Should().BeFalse();
    }

    [Theory]
    [InlineData(StringComparison.Ordinal, "value", true)]
    [InlineData(StringComparison.Ordinal, "VALUE", false)]
    [InlineData(StringComparison.OrdinalIgnoreCase, "VALUE", true)]
    public void SequenceEqual_UsesComparer(StringComparison comparison, string value, bool expected)
    {
        ReadOnlyDictionary<string, string> dict1 = new(new Dictionary<string, string> { { "key", "value" } });
        ReadOnlyDictionary<string, string> dict2 = new(new Dictionary<string, string> { { "key", value } });

        // reversing the order changes the result
        dict1.SequenceEqual(dict2, comparison).Should().Be(expected);
        dict2.SequenceEqual(dict1, comparison).Should().Be(expected);
    }
}
