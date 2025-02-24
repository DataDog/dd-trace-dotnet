// <copyright file="SpanPointersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Specialized;
using System.Reflection;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.Shared;

public class SpanPointersTests
{
    [Theory]
    [InlineData("some-bucket", "some-key.data", "ab12ef34", "e721375466d4116ab551213fdea08413")]
    [InlineData("some-bucket", "some-key.你好", "ab12ef34", "d1333a04b9928ab462b5c6cadfa401f4")]
    [InlineData("some-bucket", "some-key.data", "ab12ef34-5", "2b90dffc37ebc7bc610152c3dc72af9f")]
    public void GeneratePointerHash_ShouldGenerateValidHash(string bucket, string key, string eTag, string expectedHash)
    {
        var method = typeof(SpanPointers).GetMethod("GeneratePointerHash", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("GeneratePointerHash method should exist");

        var result = method!.Invoke(null, [new[] { bucket, key, eTag }]) as string;

        result.Should().NotBeNull("Hash result should not be null");
        result.Should().Be(expectedHash, "Hash should match expected value");
    }

    [Theory]
    [InlineData("\"abc123\"", "abc123")]
    [InlineData("abc123", "abc123")]
    [InlineData("\"\"", "")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void StripQuotes_ShouldHandleVariousInputs(string input, string expected)
    {
        var method = typeof(SpanPointers).GetMethod("StripQuotes", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("StripQuotes method should exist");

        var result = method!.Invoke(null, [input]) as string;

        result.Should().Be(expected);
    }

    [Fact]
    public void AddS3SpanPointer_ShouldAddCorrectSpanLink()
    {
        var tracer = GetTracer();
        var scope = AwsS3Common.CreateScope(tracer, "PutObject", out var tags);
        var span = scope!.Span;
        const string bucket = "test-bucket";
        const string key = "test-key";
        const string eTag = "\"test-etag\"";
        SpanPointers.AddS3SpanPointer(span, bucket, key, eTag);

        span.SpanLinks.Should().HaveCount(1, "Should have exactly one span link");
        var link = span.SpanLinks[0];

        link.Context.Should().Equal(SpanContext.ZeroContext);
        link.Attributes.Should().Contain(x => x.Key == "ptr.kind" && x.Value == "aws.s3.object");
        link.Attributes.Should().Contain(x => x.Key == "ptr.dir" && x.Value == "d");
        link.Attributes.Should().Contain(x => x.Key == "link.kind" && x.Value == "span-pointer");
        link.Attributes.Should().Contain(x => x.Key == "ptr.hash" && x.Value == "b7b8ca30a2b7a33d8412d7ca62bcad36");
    }

    private static Tracer GetTracer()
    {
        var collection = new NameValueCollection { { ConfigurationKeys.MetadataSchemaVersion, "v1" } };
        IConfigurationSource source = new NameValueConfigurationSource(collection);
        var settings = new TracerSettings(source);
        var writerMock = new Mock<IAgentWriter>();
        var samplerMock = new Mock<ITraceSampler>();

        return new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
    }
}
