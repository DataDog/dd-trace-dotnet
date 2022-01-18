// <copyright file="DatadogTagsHeaderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Tagging.PropagatedTags;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Tagging.PropagatedTags;

public class DatadogTagsHeaderTests
{
    [Fact]
    public void Serialize_0_Pairs()
    {
        var pairs = Array.Empty<KeyValuePair<string, string>>();
        var header = DatadogTagsHeader.Serialize(pairs);
        header.Should().Be(string.Empty);
    }

    [Fact]
    public void Serialize_3_Pairs()
    {
        var pairs = new KeyValuePair<string, string>[]
                    {
                        new("key1", "value1"),
                        new("key2", "value2"),
                        new("key3", "value3"),
                    };

        var header = DatadogTagsHeader.Serialize(pairs);
        header.Should().Be("key1=value1,key2=value2,key3=value3");
    }

    [Theory]
    [InlineData(null, "key1", null)]                                      // null headers
    [InlineData("", "key1", null)]                                        // empty headers
    [InlineData("key1=value1,key2=value2,key3=value3", "key1", "value1")] // beginning
    [InlineData("key1=value1,key2=value2,key3=value3", "key2", "value2")] // middle
    [InlineData("key1=value1,key2=value2,key3=value3", "key3", "value3")] // end
    [InlineData("key1=value1,ey1=value2", "ey1", "value2")]               // don't stop at "key1="
    [InlineData("key1=key2,key2=value2", "key2", "value2")]               // don't stop at "=key2"
    public void GetTagValue(string headers, string key, string expectedValue)
    {
        var value = DatadogTagsHeader.GetTagValue(headers, key);
        value.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(null, "key1", "newValue", "key1=newValue")]                                                                      // add new tag to null header
    [InlineData("", "key1", "newValue", "key1=newValue")]                                                                        // add new tag to empty header
    [InlineData("key1=value1,key2=value2,key3=value3", "key1", "newValue", "key1=value1|newValue,key2=value2,key3=value3")]      // append to first tag
    [InlineData("key1=value1,key2=value2,key3=value3", "key2", "newValue", "key1=value1,key2=value2|newValue,key3=value3")]      // append to middle tag
    [InlineData("key1=value1,key2=value2,key3=value3", "key3", "newValue", "key1=value1,key2=value2,key3=value3|newValue")]      // append to last tag
    [InlineData("key1=value1,key2=value2,key3=value3", "key4", "newValue", "key1=value1,key2=value2,key3=value3,key4=newValue")] // add new tag
    [InlineData("key1=value1,key2=value2,key3=value3", "ey3",  "newValue", "key1=value1,key2=value2,key3=value3,ey3=newValue")]  // add new tag, don't stop at "key3="
    [InlineData("key1=key2,key2=value2,key3=value3",   "key2", "newValue", "key1=key2,key2=value2|newValue,key3=value3")]        // add new tag, don't stop at "=key2"
    public void AppendTagValue_KeyValuePair(string existingHeader, string newKey, string newValue, string expectedHeader)
    {
        var newHeaderValue = DatadogTagsHeader.AppendTagValue(existingHeader, tagValueSeparator: '|', newKey, newValue);
        newHeaderValue.Should().Be(expectedHeader);
    }

    [Fact]
    public void AppendTagValue_UpstreamServices()
    {
        string header = string.Empty;

        // add first value (create the tag)
        var service1 = new UpstreamService("Service1", -1, 2, 0.95761);
        var tagValue1 = service1.ToString();
        header = DatadogTagsHeader.AppendTagValue(header, service1);
        header.Should().Be($"_dd.p.upstream_services={tagValue1}");

        // add second value (append to the existing tag)
        var service2 = new UpstreamService("Service2", 1, 3, 0.90769);
        var tagValue2 = service2.ToString();
        header = DatadogTagsHeader.AppendTagValue(header, service2);
        header.Should().Be($"_dd.p.upstream_services={tagValue1};{tagValue2}");
    }
}
