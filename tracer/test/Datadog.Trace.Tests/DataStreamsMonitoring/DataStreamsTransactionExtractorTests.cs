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
    [Theory]
    [InlineData("HTTP_OUT_HEADERS",      1)]
    [InlineData("HTTP_IN_HEADERS",       2)]
    [InlineData("KAFKA_CONSUME_HEADERS", 3)]
    [InlineData("KAFKA_PRODUCE_HEADERS", 4)]
    public void ExtractorType_ReturnsCorrectType_ForKnownTypeString(string stringType, int expectedInt)
    {
        var expected = (DataStreamsTransactionExtractor.ExtractorType)expectedInt;
        var json = $"[{{\"name\": \"n\", \"type\": \"{stringType}\", \"value\": \"v\"}}]";
        var registry = new DataStreamsExtractorRegistry(DataStreamsTransactionExtractor.ParseList(json));
        registry.GetExtractorsByType(expected).Should().ContainSingle()
                .Which.ParsedType.Should().Be(expected);
    }

    [Theory]
    [InlineData("UNKNOWN_STUFF")]
    [InlineData("")]
    public void ExtractorType_SkipsItem_ForUnknownTypeString(string stringType)
    {
        var json = $"[{{\"name\": \"n\", \"type\": \"{stringType}\", \"value\": \"v\"}}]";
        var registry = new DataStreamsExtractorRegistry(DataStreamsTransactionExtractor.ParseList(json));
        registry.GetExtractorsByType(DataStreamsTransactionExtractor.ExtractorType.Unknown).Should().BeNull();
    }
}
