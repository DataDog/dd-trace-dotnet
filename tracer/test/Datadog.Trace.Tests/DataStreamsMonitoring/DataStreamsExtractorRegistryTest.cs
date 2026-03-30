// <copyright file="DataStreamsExtractorRegistryTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DataStreamsMonitoring.TransactionTracking;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsExtractorRegistryTest
{
    [Fact]
    public void DeserializeCorrectly()
    {
        var registry = new DataStreamsExtractorRegistry("[{\"name\": \"transaction-origin\", \"type\": \"HTTP_OUT_HEADERS\", \"value\": \"transaction-id\"}]");
        registry.AsJson().Should().Be("{\"HttpOutHeaders\":[{\"name\":\"transaction-origin\",\"type\":\"HTTP_OUT_HEADERS\",\"value\":\"transaction-id\",\"ExtractorType\":1}]}");
    }

    [Fact]
    public void GetExtractorsByType_ReturnsAllExtractors_ForSameType()
    {
        var registry = new DataStreamsExtractorRegistry(
            "[" +
            "{\"name\": \"n1\", \"type\": \"HTTP_OUT_HEADERS\", \"value\": \"v1\"}," +
            "{\"name\": \"n2\", \"type\": \"HTTP_OUT_HEADERS\", \"value\": \"v2\"}" +
            "]");

        var extractors = registry.GetExtractorsByType(DataStreamsTransactionExtractor.Type.HttpOutHeaders);

        extractors.Should().HaveCount(2);
        extractors.Should().Contain(e => e.Name == "n1" && e.Value == "v1");
        extractors.Should().Contain(e => e.Name == "n2" && e.Value == "v2");
    }

    [Fact]
    public void GetExtractorsByType_DoesNotReturnExtractors_ForOtherType()
    {
        var registry = new DataStreamsExtractorRegistry(
            "[{\"name\": \"n1\", \"type\": \"HTTP_OUT_HEADERS\", \"value\": \"v1\"}]");

        registry.GetExtractorsByType(DataStreamsTransactionExtractor.Type.HttpInHeaders).Should().BeNull();
    }
}
