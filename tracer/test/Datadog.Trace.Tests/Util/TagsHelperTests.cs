// <copyright file="TagsHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

#nullable enable

namespace Datadog.Trace.Tests.Util;

/// <summary>
/// Taken from https://github.com/DataDog/dd-trace-java/blob/069ada67c23fd6735fb6722a8d57b9d3e40edc87/internal-api/src/test/java/datadog/trace/util/TagsHelperTest.java
/// </summary>
public class TagsHelperTests
{
    [Fact]
    public void ValidTagDoesNotChange()
    {
        var keyValue = "key:value";
        var validChars = "abc..xyz1234567890-_./:";
        TagsHelper.Sanitize(keyValue).Should().Be(keyValue);
        TagsHelper.Sanitize(validChars).Should().Be(validChars);
    }

    [Fact]
    public void NullIsSupported()
    {
        TagsHelper.Sanitize(tag: null).Should().Be(expected: null);
    }

    [Fact]
    public void UpperCase()
    {
        TagsHelper.Sanitize("key:VALUE").Should().Be("key:value");
    }

    [Fact]
    public void TrimSpaces()
    {
        TagsHelper.Sanitize("    service-name  ").Should().Be("service-name");
        TagsHelper.Sanitize("    service name  ").Should().Be("service_name");
    }

    [Fact]
    public void InvalidCharsConvertedToUnderscore()
    {
        TagsHelper.Sanitize("my@email.com").Should().Be("my_email.com");
        TagsHelper.Sanitize("smile and \u1234").Should().Be("smile_and__");
    }

    [Fact]
    public void TagTrimmedToMaxLength()
    {
        StringBuilder tag = new StringBuilder(capacity: 401);
        for (var i = 0; i < 400; i++)
        {
            tag.Append("a");
        }

        var sanitized = TagsHelper.Sanitize(tag.ToString());
        sanitized!.Length.Should().Be(expected: 200);
        Encoding.UTF8.GetByteCount(sanitized).Should().Be(expected: 200);
    }

    [Fact]
    public void TagTrimmedToMaxLengthWorkWithUnicode()
    {
        var tag = new StringBuilder(capacity: 401);
        for (var i = 0; i < 400; i++)
        {
            tag.Append("\u1234");
        }

        var sanitized = TagsHelper.Sanitize(tag.ToString());
        sanitized.Should().NotBeNullOrEmpty();
        sanitized!.Length.Should().Be(expected: 200);
        Encoding.UTF8.GetByteCount(sanitized).Should().Be(expected: 200);
    }
}
