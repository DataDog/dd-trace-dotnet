// <copyright file="BaggageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
        var baggage = new Baggage { { "key1", "value1" } };
        baggage.Count.Should().Be(1);

        baggage.AddOrReplace("key2", "value2");
        baggage.Count.Should().Be(2);
    }

    [Fact]
    public void TryGetValue_ReturnsTrue_WhenItemExists()
    {
        var baggage = new Baggage { { "key", "value" } };
        var exists = baggage.TryGetValue("key", out var value);

        exists.Should().BeTrue();
        value.Should().Be("value");
    }

    [Fact]
    public void TryGetValue_ReturnsFalse_WhenItemDoesNotExist()
    {
        var baggage = new Baggage { { "key", "value" } };
        var exists = baggage.TryGetValue("nonexistent", out var value);

        exists.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void GetValueOrDefault_ReturnsNull_WhenItemDoesNotExist()
    {
        var baggage = new Baggage();
        var value = baggage.GetValueOrDefault("nonexistent");
        value.Should().BeNull();
    }

    [Fact]
    public void GetValueOrDefault_ReturnsValue_WhenItemExists()
    {
        var baggage = new Baggage { { "key", "value" } };
        var value = baggage.GetValueOrDefault("key");
        value.Should().Be("value");
    }

    [Fact]
    public void Indexer_ReturnsValue_WhenItemExists()
    {
        var baggage = new Baggage { { "key", "value" } };
        var value = baggage["key"];
        value.Should().Be("value");
    }

    [Fact]
    public void Indexer_Throws_WhenItemDoesNotExist()
    {
        var baggage = new Baggage { { "key", "value" } };

        FluentActions.Invoking(() => baggage["nonexistent"])
                     .Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Add_AddsNewItem_WhenItemDoesNotExist()
    {
        var baggage = new Baggage();
        baggage.AddOrReplace("key", "value");

        var value = baggage.GetValueOrDefault("key");
        value.Should().Be("value");
    }

    [Fact]
    public void Add_Throws_WhenItemExists()
    {
        var baggage = new Baggage { { "key", "value1" } };

        FluentActions.Invoking(() => baggage.Add("key", "value2"))
                     .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddOrReplace_AddsNewItem_WhenItemDoesNotExist()
    {
        var baggage = new Baggage();
        baggage.AddOrReplace("key", "value");

        var value = baggage.GetValueOrDefault("key");
        value.Should().Be("value");
    }

    [Fact]
    public void AddOrReplace_UpdatesItem_WhenItemExists()
    {
        var baggage = new Baggage { { "key", "value1" } };
        baggage.AddOrReplace("key", "value2");

        var value = baggage.GetValueOrDefault("key");
        value.Should().Be("value2");
    }

    [Fact]
    public void Remove_ReturnsFalse_WhenItemDoesNotExist()
    {
        var baggage = new Baggage { { "key", "value" } };
        var result = baggage.Remove("nonexistent");
        result.Should().BeFalse();
    }

    [Fact]
    public void Remove_ReturnsTrue_WhenItemExists()
    {
        var baggage = new Baggage { { "key", "value" } };
        baggage.GetValueOrDefault("key").Should().Be("value");

        var result = baggage.Remove("key");

        result.Should().BeTrue();
        baggage.GetValueOrDefault("key").Should().BeNull();
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var baggage = new Baggage { { "key", "value" } };
        baggage.GetValueOrDefault("key").Should().Be("value");
        baggage.Count.Should().Be(1);

        baggage.Clear();
        baggage.Count.Should().Be(0);
    }

    [Fact]
    public void MergeInto_AddsAndReplacesItems()
    {
        var baggage1 = new Baggage { { "key1", "value1" } };

        var baggage2 = new Baggage
        {
            { "key1", "new value" }, // replace "key1"
            { "key2", "value2" }     // add "key2"
        };

        baggage2.MergeInto(baggage1);

        baggage1.GetValueOrDefault("key1").Should().Be("new value");
        baggage1.GetValueOrDefault("key2").Should().Be("value2");
    }

    [Fact]
    public void MergeInto_SameInstance()
    {
        var baggage = new Baggage { { "key1", "value1" } };

        baggage.MergeInto(baggage);
        baggage.Count.Should().Be(1);
        baggage.GetValueOrDefault("key1").Should().Be("value1");
    }
}
