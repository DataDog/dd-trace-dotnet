// <copyright file="AwsS3CommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Specialized;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.S3;

public class AwsS3CommonTests
{
    private const string BucketName = "MyBucketName";
    private const string ObjectKey = "MyObjectKey";

    [Fact]
    public async Task GetCorrectOperationName()
    {
        await using var tracerV0 = GetTracer("v0");
        AwsS3Common.GetOperationName(tracerV0).Should().Be("s3.request");

        await using var tracerV1 = GetTracer("v1");
        AwsS3Common.GetOperationName(tracerV1).Should().Be("aws.s3.request");
    }

    [Fact]
    public async Task CreateScopeCorrectAttributes()
    {
        await using var tracer = GetTracer();
        var scope = AwsS3Common.CreateScope(tracer, "PutObject", out var tags);
        scope.Should().NotBeNull();

        var span = scope!.Span;
        span.Type.Should().Be(SpanTypes.Http);
        span.ResourceName.Should().Be("S3.PutObject");

        tags.Should().NotBeNull();
        tags!.SpanKind.Should().Be(SpanKinds.Client);
        tags.InstrumentationName.Should().Be("aws-sdk");
        tags.Operation.Should().Be("PutObject");
        tags.AwsService.Should().Be("S3");
    }

    [Fact]
    public async Task SetTags_WithValidParams()
    {
        await using var tracer = GetTracer();
        AwsS3Common.CreateScope(tracer, "PutObject", out var tags);
        tags.Should().NotBeNull();

        AwsS3Common.SetTags(tags, BucketName, ObjectKey);
        tags!.BucketName.Should().Be(BucketName);
        tags.ObjectKey.Should().Be(ObjectKey);
    }

    [Fact]
    public void SetTags_WithNullTags()
    {
        AwsS3Tags? tags = null;

        AwsS3Common.SetTags(tags, BucketName, ObjectKey);
        tags.Should().BeNull();
    }

    [Fact]
    public void SetTags_WithEmptyTags()
    {
        AwsS3Tags tags = new();
        AwsS3Common.SetTags(tags, BucketName, ObjectKey);
        tags.Should().NotBeNull();

        tags.BucketName.Should().Be(BucketName);
        tags.ObjectKey.Should().Be(ObjectKey);
    }

    [Fact]
    public async Task SetTags_WithMissingBucketName()
    {
        await using var tracer = GetTracer();
        AwsS3Common.CreateScope(tracer, "SomeOperation", out var tags);
        tags.Should().NotBeNull();

        AwsS3Common.SetTags(tags, null, ObjectKey);
        tags!.BucketName.Should().BeNull();
        tags.ObjectKey.Should().Be(ObjectKey);
    }

    [Fact]
    public async Task SetTags_WithMissingObjectKey()
    {
        await using var tracer = GetTracer();
        AwsS3Common.CreateScope(tracer, "PutBucket", out var tags);
        tags.Should().NotBeNull();

        AwsS3Common.SetTags(tags, BucketName, null);
        tags!.BucketName.Should().Be(BucketName);
        tags.ObjectKey.Should().BeNull();
    }

    private static ScopedTracer GetTracer(string schemaVersion = "v1")
    {
        var collection = new NameValueCollection { { ConfigurationKeys.MetadataSchemaVersion, schemaVersion } };
        IConfigurationSource source = new NameValueConfigurationSource(collection);
        var settings = new TracerSettings(source);
        var writerMock = new Mock<IAgentWriter>();
        var samplerMock = new Mock<ITraceSampler>();

        return TracerHelper.Create(settings, writerMock.Object, samplerMock.Object);
    }
}
