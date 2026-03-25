// <copyright file="DataStreamsTransactionExtractorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Linq;
using Datadog.Trace.DataStreamsMonitoring.TransactionTracking;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsTransactionExtractorTests
{
    // Issues 8 + 10: all known type strings must resolve to the correct enum value.
    // Enum values: Unknown=0, HttpOutHeaders=1, HttpInHeaders=2, KafkaConsumeHeaders=3, KafkaProduceHeaders=4
    [Theory]
    [InlineData("HTTP_OUT_HEADERS",      1)]
    [InlineData("HTTP_IN_HEADERS",       2)]
    [InlineData("KAFKA_CONSUME_HEADERS", 3)]
    [InlineData("KAFKA_PRODUCE_HEADERS", 4)]
    [InlineData("UNKNOWN_STUFF",         0)]
    [InlineData("",                      0)]
    public void ExtractorType_ReturnsCorrectType_ForTypeString(string stringType, int expectedInt)
    {
        var expected = (DataStreamsTransactionExtractor.Type)expectedInt;
        var json = $"[{{\"name\": \"n\", \"type\": \"{stringType}\", \"value\": \"v\"}}]";
        var registry = new DataStreamsExtractorRegistry(json);
        registry.GetExtractorsByType(expected).Should().ContainSingle()
                .Which.ExtractorType.Should().Be(expected);
    }

    [Fact]
    public void ExtractorType_ReturnsSameValue_OnMultipleCalls()
    {
        var registry = new DataStreamsExtractorRegistry("[{\"name\": \"n\", \"type\": \"HTTP_OUT_HEADERS\", \"value\": \"v\"}]");
        var extractor = registry.GetExtractorsByType(DataStreamsTransactionExtractor.Type.HttpOutHeaders)!.Single();
        extractor.ExtractorType.Should().Be(extractor.ExtractorType);
    }
}
