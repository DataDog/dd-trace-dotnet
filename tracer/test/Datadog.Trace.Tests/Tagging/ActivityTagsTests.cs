// <copyright file="ActivityTagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Tagging;

[Collection(nameof(ActivityTagsTests))]
public class ActivityTagsTests
{
    public enum TagKind
    {
        /// <summary>
        /// Tag should be found in meta
        /// </summary>
        Meta,

        /// <summary>
        /// Tag should be found in metrics
        /// </summary>
        Metric,
    }

    public static IEnumerable<object[]> TagData =>
        new List<object[]>()
        {
            // key, value, location
            new object[] { "char_val", 'c', TagKind.Meta },
            new object[] { "string_val", "val", TagKind.Meta },
            new object[] { "bool_val", true, TagKind.Meta },
            new object[] { "byte_val", (byte)5, TagKind.Metric },
            new object[] { "sbyte_val", (sbyte)-5, TagKind.Metric },
            new object[] { "short_val", (short)-5, TagKind.Metric },
            new object[] { "ushort_val", (ushort)5, TagKind.Metric },
            new object[] { "int_val", -5, TagKind.Metric },
            new object[] { "uint_val", 5U, TagKind.Metric },
            new object[] { "long_val", -5L, TagKind.Metric },
            new object[] { "ulong_val", 5UL, TagKind.Metric },
            new object[] { "float_val", -5.0f, TagKind.Metric },
            new object[] { "double_val", -5.0, TagKind.Metric },
            new object[] { "object_val", new(), TagKind.Meta }
        };

    public static IEnumerable<object[]> ArrayTagData =>
        new List<object[]>()
        {
            // key, value, location, expected_dot_notation_dict
            new object[] { "char[]_val", new[] { 'c', 'd' }, TagKind.Meta, new Dictionary<string, object> { { "char[]_val.0", "c" }, { "char[]_val.1", "d" } } },
            new object[] { "string[]_val", new[] { "val1", "val2" }, TagKind.Meta, new Dictionary<string, object> { { "string[]_val.0", "val1" }, { "string[]_val.1", "val2" } } },
            new object[] { "bool[]_val", new[] { true, false }, TagKind.Meta, new Dictionary<string, object> { { "bool[]_val.0", "true" }, { "bool[]_val.1", "false" } } },
            new object[] { "double[]_val", new[] { -5.0, 5.0 }, TagKind.Metric, new Dictionary<string, object> { { "double[]_val.0", -5.0 }, { "double[]_val.1", 5.0 } } },
            new object[]
            {
                "char[][]_val", new[]
                {
                    new object[] { 'a', 'b', 'c' },
                    new object[] { 'd', 'e', 'f' }
                }, TagKind.Meta, new Dictionary<string, object> { { "char[][]_val.0", """["a","b","c"]""" }, { "char[][]_val.1", """["d","e","f"]""" } }
            }
        };

    // TODO what about an array of object that contains string and numeric objects?

    [Theory]
    [MemberData(nameof(TagData))]
    public void Tags_ShouldBe_PlacedInMetricsOrMeta(string tagKey, object tagValue, TagKind expectedTagKind)
    {
        var activityMock = new Mock<IActivity5>();
        activityMock.Setup(x => x.Kind).Returns(ActivityKind.Producer);

        var tagObjects = new Dictionary<string, object> { { tagKey, tagValue } };

        activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

        var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
        OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

        switch (expectedTagKind)
        {
            case TagKind.Meta:
                span.GetTag(tagKey).Should().BeEquivalentTo(tagValue.ToString());
                break;
            case TagKind.Metric:
                span.GetMetric(tagKey).Should().Be(double.Parse(tagValue.ToString()!));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expectedTagKind), expectedTagKind, null);
        }
    }

    [Theory]
    [MemberData(nameof(ArrayTagData))]
    public void ArrayedTags_ShouldBe_PlacedInMeta(string tagKey, object tagValue, TagKind expectedTagKind, Dictionary<string, object> expectedTagValues)
    {
        var activityMock = new Mock<IActivity5>();
        activityMock.Setup(x => x.Kind).Returns(ActivityKind.Producer);

        var tagObjects = new Dictionary<string, object> { { tagKey, tagValue } };

        activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

        var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
        OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

        foreach (var keyValue in expectedTagValues)
        {
            switch (expectedTagKind)
            {
                case TagKind.Meta:
                    span.GetTag(keyValue.Key).Should().BeEquivalentTo(keyValue.Value.ToString());
                    break;
                case TagKind.Metric:
                    span.GetMetric(keyValue.Key).Should().Be(double.Parse(keyValue.Value.ToString()!));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(expectedTagKind), expectedTagKind, null);
            }
        }
    }
}
