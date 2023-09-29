// <copyright file="AwsDynamoDbCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using Amazon.DynamoDBv2.Model;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.DynamoDb;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.DynamoDb;

public class AwsDynamoDbCommonTests
{
    private const string TableName = "MyTableName";

    [Fact]
    public void TagTableNameAndResourceName_TagsProperly()
    {
        var tracer = GetTracer();
        var scope = AwsDynamoDbCommon.CreateScope(tracer, "GetItem", out AwsDynamoDbTags tags);

        AwsDynamoDbCommon.TagTableNameAndResourceName(TableName, tags, scope);

        tags.TableName.Should().Be("MyTableName");

        var span = scope.Span;
        span.ResourceName.Should().Be("DynamoDB.GetItem MyTableName");
    }

    [Fact]
    public void TagTableNameAndResourceName_WithNullTags_SkipsTagging()
    {
        var tracer = GetTracer();
        var scope = AwsDynamoDbCommon.CreateScope(tracer, "GetItem", out AwsDynamoDbTags tags);
        var request = new GetItemRequest { TableName = TableName };
        var proxy = request.DuckCast<IAmazonDynamoDbRequestWithTableName>();

        tags = null;
        AwsDynamoDbCommon.TagTableNameAndResourceName(proxy.TableName, tags, scope);

        var span = scope.Span;
        span.ResourceName.Should().Be("DynamoDB.GetItem");
    }

    [Fact]
    public void TagTableNameAndResourceName_WithNullScope_SkipsTagging()
    {
        var tracer = GetTracer();
        var scope = AwsDynamoDbCommon.CreateScope(tracer, "GetItem", out AwsDynamoDbTags tags);
        var request = new GetItemRequest { TableName = TableName };
        var proxy = request.DuckCast<IAmazonDynamoDbRequestWithTableName>();

        scope = null;
        AwsDynamoDbCommon.TagTableNameAndResourceName(proxy.TableName, tags, scope);

        tags.TableName.Should().Be(null);
    }

    [Fact]
    public void TagTableNameAndResourceName_WithEmptyRequest_SkipsTagging()
    {
        var tracer = GetTracer();
        var scope = AwsDynamoDbCommon.CreateScope(tracer, "GetItem", out AwsDynamoDbTags tags);
        var request = new GetItemRequest();
        var proxy = request.DuckCast<IAmazonDynamoDbRequestWithTableName>();

        AwsDynamoDbCommon.TagTableNameAndResourceName(proxy.TableName, tags, scope);

        tags.TableName.Should().Be(null);

        var span = scope.Span;
        span.ResourceName.Should().Be("DynamoDB.GetItem");
    }

    [Fact]
    public void TagBatchRequest_WithOneTable_TagsTableNameAndResourceName()
    {
        var tracer = GetTracer();
        var scope = AwsDynamoDbCommon.CreateScope(tracer, "BatchGetItem", out AwsDynamoDbTags tags);
        var request = new BatchGetItemRequest
        {
            RequestItems = new Dictionary<string, KeysAndAttributes>()
            {
                { TableName, new() }
            }
        };
        var proxy = request.DuckCast<IBatchRequest>();

        AwsDynamoDbCommon.TagBatchRequest(proxy, tags, scope);

        tags.TableName.Should().Be("MyTableName");

        var span = scope.Span;
        span.ResourceName.Should().Be("DynamoDB.BatchGetItem MyTableName");
    }

    [Fact]
    public void TagBatchRequest_WithMultipleTables_SkipsTagging()
    {
        var tracer = GetTracer();
        var scope = AwsDynamoDbCommon.CreateScope(tracer, "BatchGetItem", out AwsDynamoDbTags tags);
        var request = new BatchGetItemRequest
        {
            RequestItems = new Dictionary<string, KeysAndAttributes>()
            {
                { TableName, new() },
                { "MyOtherTable", new() }
            }
        };
        var proxy = request.DuckCast<IBatchRequest>();

        AwsDynamoDbCommon.TagBatchRequest(proxy, tags, scope);

        tags.TableName.Should().Be(null);

        var span = scope.Span;
        span.ResourceName.Should().Be("DynamoDB.BatchGetItem");
    }

    [Fact]
    public void TagBatchRequest_WithEmptyRequest_SkipsTagging()
    {
        var tracer = GetTracer();
        var scope = AwsDynamoDbCommon.CreateScope(tracer, "BatchWriteItem", out AwsDynamoDbTags tags);
        var request = new BatchWriteItemRequest
        {
            RequestItems = new Dictionary<string, List<WriteRequest>>()
        };
        var proxy = request.DuckCast<IBatchRequest>();

        AwsDynamoDbCommon.TagBatchRequest(proxy, tags, scope);

        tags.TableName.Should().Be(null);

        var span = scope.Span;
        span.ResourceName.Should().Be("DynamoDB.BatchWriteItem");
    }

    private static Tracer GetTracer(string schemaVersion = "v1")
    {
        var collection = new NameValueCollection { { ConfigurationKeys.MetadataSchemaVersion, schemaVersion } };
        IConfigurationSource source = new NameValueConfigurationSource(collection);
        var settings = new TracerSettings(source);
        var writerMock = new Mock<IAgentWriter>();
        var samplerMock = new Mock<ITraceSampler>();

        return new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
    }
}
