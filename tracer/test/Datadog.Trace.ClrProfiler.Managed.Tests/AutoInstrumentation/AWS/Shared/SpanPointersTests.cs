// <copyright file="SpanPointersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Specialized;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.DynamoDb;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers.TestTracer;
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
    public void ConcatenateComponents_S3(string bucket, string key, string eTag, string expectedString)
    {
        var components = SpanPointers.ConcatenateComponents(bucket, key, eTag);

        components.Should().Be(expectedString);
    }

    [Theory]
    [InlineData("some-table-a", "some-key.data", "ab12ef34", "", "", "some-table-a|some-key.data|ab12ef34||")] // 1 primary key
    [InlineData("some-table-b", "some-key.你好", "é🐶", "", "", "some-table-b|some-key.你好|é🐶||")] // Unicode
    [InlineData("some-table-c", "some-key", "abc", "another-key", "def", "some-table-c|some-key|abc|another-key|def")] // 2 primary keys
    public void ConcatenateComponents_DynamoDb(string tableName, string key1, string value1, string key2, string value2, string expectedString)
    {
        var components = SpanPointers.ConcatenateComponents(tableName, key1, value1, key2, value2);

        components.Should().Be(expectedString);
    }

    [Theory]
    [InlineData("some-bucket", "some-key.data", "ab12ef34", "e721375466d4116ab551213fdea08413")]
    [InlineData("some-bucket", "some-key.data", "\"ab12ef34\"", "e721375466d4116ab551213fdea08413")] // eTag should be trimmed
    [InlineData("some-bucket", "some-key.你好", "ab12ef34", "d1333a04b9928ab462b5c6cadfa401f4")] // Unicode
    [InlineData("some-bucket", "some-key.data", "ab12ef34-5", "2b90dffc37ebc7bc610152c3dc72af9f")]
    public void GeneratePointerHash_S3_ShouldGenerateValidHash(string bucket, string key, string eTag, string expectedHash)
    {
        var components = SpanPointers.ConcatenateComponents(bucket, key, eTag);
        var hash = SpanPointers.GeneratePointerHash(components);

        hash.Should().Be(expectedHash);
    }

    [Theory]
    [InlineData("some-table", "some-key", "some-value", "", "", "7f1aee721472bcb48701d45c7c7f7821")] // 1 string primary key
    [InlineData("some-table", "some-key", "123.456", "", "", "434a6dba3997ce4dbbadc98d87a0cc24")] // 1 number primary key
    [InlineData("some-table", "other-key", "123", "some-key", "some-value", "7aa1b80b0e49bd2078a5453399f4dd67")] // string and number primary keys
    public void GeneratePointerHash_DynamoDb_ShouldGenerateValidHash(string table, string key1, string value1, string key2, string value2, string expectedHash)
    {
        var components = SpanPointers.ConcatenateComponents(table, key1, value1, key2, value2);
        var hash = SpanPointers.GeneratePointerHash(components);

        hash.Should().Be(expectedHash);
    }

    [Fact]
    public async Task AddS3SpanPointer_ShouldAddCorrectSpanLink()
    {
        await using var tracer = GetTracer();
        var scope = AwsS3Common.CreateScope(tracer, "PutObject", out _);
        var span = scope!.Span;
        const string bucket = "test-bucket";
        const string key = "test-key";
        const string eTag = "\"test-etag\"";
        SpanPointers.AddS3SpanPointer(span, bucket, key, eTag);

        span.SpanLinks.Should().ContainSingle("Should have exactly one span link");
        var link = span.SpanLinks[0];

        link.Context.Should().Equal(SpanContext.Zero);

        // we can use Contain(key, value) because Attributes is "dictionary-like"
        link.Attributes.Should().Contain("ptr.kind", "aws.s3.object");
        link.Attributes.Should().Contain("ptr.dir", "d");
        link.Attributes.Should().Contain("link.kind", "span-pointer");
        link.Attributes.Should().Contain("ptr.hash", "b7b8ca30a2b7a33d8412d7ca62bcad36");
    }

    [Fact]
    public async Task AddS3SpanPointer_ShouldSkipMissingEtag()
    {
        await using var tracer = GetTracer();
        var scope = AwsS3Common.CreateScope(tracer, "PutObject", out _);
        var span = scope!.Span;
        const string bucket = "test-bucket";
        const string key = "test-key";
        const string? eTag = null;
        SpanPointers.AddS3SpanPointer(span, bucket, key, eTag);

        span.SpanLinks.Should().BeNull();
    }

    [Fact]
    public async Task AddDynamoDbSpanPointer_ShouldAddCorrectSpanLink()
    {
        await using var tracer = GetTracer();
        var scope = AwsDynamoDbCommon.CreateScope(tracer, "UpdateItem", out _);
        var span = scope!.Span;
        const string table = "test-table";

        // Mock IDynamoDbKeysObject
        var stringAttr = new Mock<IDynamoDbAttributeValue>();
        stringAttr.Setup(a => a.S).Returns("testvalue");
        var keys = new Mock<IDynamoDbKeysObject>();
        keys.Setup(k => k.Instance).Returns(new object()); // not important of what it returns as long as it isn't null
        keys.Setup(k => k.KeyNames).Returns(new[] { "key1" });
        keys.Setup(k => k["key1"]).Returns(stringAttr.Object);

        SpanPointers.AddDynamoDbSpanPointer(span, table, keys.Object);

        span.SpanLinks.Should().ContainSingle("Should have exactly one span link");
        var link = span.SpanLinks[0];

        link.Context.Should().Equal(SpanContext.Zero);

        link.Attributes.Should().Contain("ptr.kind", "aws.dynamodb.item");
        link.Attributes.Should().Contain("ptr.dir", "d");
        link.Attributes.Should().Contain("link.kind", "span-pointer");
        link.Attributes.Should().Contain("ptr.hash", "3fcdea19376daafa3e4202308099429d");
    }

    private static ScopedTracer GetTracer()
    {
        var collection = new NameValueCollection { { ConfigurationKeys.MetadataSchemaVersion, "v1" } };
        IConfigurationSource source = new NameValueConfigurationSource(collection);
        var settings = new TracerSettings(source);
        var writerMock = new Mock<IAgentWriter>();
        var samplerMock = new Mock<ITraceSampler>();

        return TracerHelper.Create(settings, writerMock.Object, samplerMock.Object);
    }
}
