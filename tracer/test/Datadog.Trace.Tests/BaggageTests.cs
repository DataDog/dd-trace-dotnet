// <copyright file="BaggageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

public class BaggageTests
{
    [Fact]
    public void Count_ReturnsZero_WhenBaggageIsEmpty()
    {
        var baggage = new Baggage();
        baggage.Count.Should().Be(0);
    }

    [Fact]
    public void Count_ReturnsCount_WhenBaggageHasItems()
    {
        var baggage = new Baggage(new Dictionary<string, string> { { "key1", "value1" } });
        baggage.Count.Should().Be(1);

        baggage.Set("key2", "value2");
        baggage.Count.Should().Be(2);
    }

    [Fact]
    public void Get_ReturnsNull_WhenItemDoesNotExist()
    {
        var baggage = new Baggage();
        var value = baggage.Get("nonexistent");
        value.Should().BeNull();
    }

    [Fact]
    public void Get_ReturnsValue_WhenItemExists()
    {
        var baggage = new Baggage(new Dictionary<string, string> { { "key", "value" } });
        var value = baggage.Get("key");
        value.Should().Be("value");
    }

    [Fact]
    public void Set_AddsNewItem_WhenItemDoesNotExist()
    {
        var baggage = new Baggage();
        baggage.Set("key", "value");

        var value = baggage.Get("key");
        value.Should().Be("value");
    }

    [Fact]
    public void Set_UpdatesItem_WhenItemExists()
    {
        var baggage = new Baggage(new Dictionary<string, string> { { "key", "value1" } });
        baggage.Set("key", "value2");

        var value = baggage.Get("key");
        value.Should().Be("value2");
    }

    [Fact]
    public void Remove_ReturnsFalse_WhenItemDoesNotExist()
    {
        var baggage = new Baggage();
        var result = baggage.Remove("nonexistent");
        result.Should().BeFalse();
    }

    [Fact]
    public void Remove_ReturnsTrue_WhenItemExists()
    {
        var baggage = new Baggage(new Dictionary<string, string> { { "key", "value" } });
        baggage.Get("key").Should().Be("value");

        var result = baggage.Remove("key");

        result.Should().BeTrue();
        baggage.Get("key").Should().BeNull();
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var baggage = new Baggage(new Dictionary<string, string> { { "key", "value" } });
        baggage.Get("key").Should().Be("value");
        baggage.Count.Should().Be(1);

        baggage.Clear();
        baggage.Count.Should().Be(0);
    }

    [Fact]
    public void Merge_AddsItemsToExistingBaggage()
    {
        var baggage1 = new Baggage(new Dictionary<string, string> { { "key1", "value1" } });
        var baggage2 = new Baggage(new Dictionary<string, string> { { "key2", "value2" } });

        baggage1.Merge(baggage2);

        baggage1.Get("key1").Should().Be("value1");
        baggage1.Get("key2").Should().Be("value2");
    }
}
