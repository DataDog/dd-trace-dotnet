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
    public void IsEmpty_ReturnsTrue_WhenBaggageIsEmpty()
    {
        var baggage = new Baggage();
        baggage.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_ReturnsFalse_WhenBaggageHasItems()
    {
        var baggage = new Baggage(new Dictionary<string, string> { { "key", "value" } });
        baggage.IsEmpty.Should().BeFalse();
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
    public void Set_RemovesItem_WhenValueIsNull()
    {
        var baggage = new Baggage(new Dictionary<string, string> { { "key", "value" } });
        baggage.Get("key").Should().Be("value");

        baggage.Set("key", null);
        var value = baggage.Get("key");

        value.Should().BeNull();
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

        baggage.Clear();

        baggage.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Merge_CreatesNewBaggage_WithMergedItems()
    {
        var baggage1 = new Baggage(new Dictionary<string, string> { { "key1", "value1" } });
        var baggage2 = new Baggage(new Dictionary<string, string> { { "key2", "value2" } });

        var mergedBaggage = Baggage.Merge(baggage1, baggage2);

        mergedBaggage.Get("key1").Should().Be("value1");
        mergedBaggage.Get("key2").Should().Be("value2");
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
