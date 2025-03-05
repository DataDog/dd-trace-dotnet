// <copyright file="SpanPointersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Specialized;
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
    [InlineData("some-bucket-a", "some-key.data", "ab12ef34", "some-bucket-a|some-key.data|ab12ef34")]
    [InlineData("some-bucket-b", "some-key.data", "\"ab12ef34\"", "some-bucket-b|some-key.data|ab12ef34")] // eTag should be trimmed
    [InlineData("some-bucket-c", "some-key.你好", "é🐶", "some-bucket-c|some-key.你好|é🐶")] // Unicode
    public void ConcatenateComponents(string bucket, string key, string eTag, string expectedHash)
    {
        var components = SpanPointers.ConcatenateComponents(bucket, key, eTag);

        components.Should().Be(expectedHash);
    }

    [Theory]
    [InlineData("some-bucket", "some-key.data", "ab12ef34", "e721375466d4116ab551213fdea08413")]
    [InlineData("some-bucket", "some-key.data", "\"ab12ef34\"", "e721375466d4116ab551213fdea08413")] // eTag should be trimmed
    [InlineData("some-bucket", "some-key.你好", "ab12ef34", "d1333a04b9928ab462b5c6cadfa401f4")] // Unicode
    [InlineData("some-bucket", "some-key.data", "ab12ef34-5", "2b90dffc37ebc7bc610152c3dc72af9f")]
    public void GeneratePointerHash_ShouldGenerateValidHash(string bucket, string key, string eTag, string expectedHash)
    {
        var components = SpanPointers.ConcatenateComponents(bucket, key, eTag);
        var hash = SpanPointers.GeneratePointerHash(components);

        hash.Should().Be(expectedHash);
    }

    [Fact]
    public void AddS3SpanPointer_ShouldAddCorrectSpanLink()
    {
        var tracer = GetTracer();
        var scope = AwsS3Common.CreateScope(tracer, "PutObject", out _);
        var span = scope!.Span;
        const string bucket = "test-bucket";
        const string key = "test-key";
        const string eTag = "\"test-etag\"";
        SpanPointers.AddS3SpanPointer(span, bucket, key, eTag);

        span.SpanLinks.Should().ContainSingle("Should have exactly one span link");
        var link = span.SpanLinks[0];

        link.Context.Should().Equal(SpanContext.ZeroContext);

        // we can use Contain(key, value) because Attributes is "dictionary-like"
        link.Attributes.Should().Contain("ptr.kind", "aws.s3.object");
        link.Attributes.Should().Contain("ptr.dir", "d");
        link.Attributes.Should().Contain("link.kind", "span-pointer");
        link.Attributes.Should().Contain("ptr.hash", "b7b8ca30a2b7a33d8412d7ca62bcad36");
    }

    [Fact]
    public void AddS3SpanPointer_ShouldSkipMissingEtag()
    {
        var tracer = GetTracer();
        var scope = AwsS3Common.CreateScope(tracer, "PutObject", out _);
        var span = scope!.Span;
        const string bucket = "test-bucket";
        const string key = "test-key";
        const string? eTag = null;
        SpanPointers.AddS3SpanPointer(span, bucket, key, eTag);

        span.SpanLinks.Should().BeNull();
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
