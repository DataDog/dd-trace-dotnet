// <copyright file="TelemetryDtoSerializationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.DTOs;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry;

/// <summary>
/// Baseline serialization tests for Telemetry DTO JSON models.
/// These DTOs do NOT use [JsonProperty] attributes — they rely on
/// SerializationHelpers.DefaultJsonSettings (SnakeCaseNamingStrategy + NullValueHandling.Ignore).
/// These tests capture the exact JSON format before any JSON library migration.
/// </summary>
public class TelemetryDtoSerializationTests
{
    private static readonly JsonSerializerSettings Settings = SerializationHelpers.DefaultJsonSettings;

    [Fact]
    public void MetricData_WithSettings_SerializesAsSnakeCase()
    {
        var data = new MetricData(
            metric: "spans_created",
            points: new MetricSeries([new MetricDataPoint(1700000000L, 42)]),
            common: true,
            type: "count")
        {
            Tags = ["env:prod", "version:1.0"],
            Namespace = "tracers",
            Interval = 10,
        };

        var json = JsonConvert.SerializeObject(data, Settings);

        // Verify snake_case naming (no [JsonProperty], relies on settings)
        json.Should().Contain("\"metric\":");
        json.Should().Contain("\"points\":");
        json.Should().Contain("\"common\":");
        json.Should().Contain("\"type\":");
        json.Should().Contain("\"tags\":");
        json.Should().Contain("\"namespace\":");
        json.Should().Contain("\"interval\":");

        // Verify MetricSeries custom converter serializes as nested arrays
        json.Should().Contain("[[1700000000,42]]");

        // Round-trip
        var result = JsonConvert.DeserializeObject<MetricData>(json, Settings);
        result.Metric.Should().Be("spans_created");
        result.Points.Should().ContainSingle();
        result.Points[0].Timestamp.Should().Be(1700000000L);
        result.Points[0].Value.Should().Be(42);
        result.Common.Should().BeTrue();
        result.Type.Should().Be("count");
        result.Tags.Should().HaveCount(2);
        result.Namespace.Should().Be("tracers");
        result.Interval.Should().Be(10);
    }

    [Fact]
    public void MetricData_NullFields_OmittedBySettings()
    {
        var data = new MetricData(
            metric: "test",
            points: new MetricSeries([new MetricDataPoint(1L, 0)]),
            common: false,
            type: "gauge");

        var json = JsonConvert.SerializeObject(data, Settings);

        // NullValueHandling.Ignore should omit null fields
        json.Should().NotContain("\"host\"");
        json.Should().NotContain("\"interval\"");
        json.Should().NotContain("\"tags\"");
        json.Should().NotContain("\"namespace\"");
    }

    [Fact]
    public void LogMessageData_WithStringEnumConverter_RoundTrips()
    {
        var data = new LogMessageData("Something failed", TelemetryLogLevel.ERROR, DateTimeOffset.FromUnixTimeSeconds(1700220630))
        {
            Tags = "tag1:val1,tag2:val2",
            StackTrace = "at MyClass.Method()\n  at Program.Main()",
            Count = 5,
        };

        var json = JsonConvert.SerializeObject(data, Settings);

        // Level should be serialized as string (StringEnumConverter)
        json.Should().Contain("\"ERROR\"");
        // Properties should be snake_case
        json.Should().Contain("\"message\":");
        json.Should().Contain("\"level\":");
        json.Should().Contain("\"tracer_time\":");
        json.Should().Contain("\"stack_trace\":");
        json.Should().Contain("\"tags\":");
        json.Should().Contain("\"count\":");

        var result = JsonConvert.DeserializeObject<LogMessageData>(json, Settings);
        result.Message.Should().Be("Something failed");
        result.Level.Should().Be(TelemetryLogLevel.ERROR);
        result.TracerTime.Should().Be(1700220630L);
        result.Tags.Should().Be("tag1:val1,tag2:val2");
        result.StackTrace.Should().Contain("MyClass.Method");
        result.Count.Should().Be(5);
    }

    [Fact]
    public void TelemetryLogLevel_AllValues_SerializeAsString()
    {
        var allValues = new[]
        {
            (TelemetryLogLevel.ERROR, "\"ERROR\""),
            (TelemetryLogLevel.WARN, "\"WARN\""),
            (TelemetryLogLevel.DEBUG, "\"DEBUG\""),
        };

        foreach (var (level, expectedJson) in allValues)
        {
            var data = new LogMessageData("msg", level, DateTimeOffset.FromUnixTimeSeconds(1));
            var json = JsonConvert.SerializeObject(data, Settings);
            json.Should().Contain(expectedJson);
        }
    }

    [Fact]
    public void LogMessageData_NullOptionalFields_OmittedBySettings()
    {
        var data = new LogMessageData("msg", TelemetryLogLevel.DEBUG, DateTimeOffset.FromUnixTimeSeconds(1));

        var json = JsonConvert.SerializeObject(data, Settings);

        json.Should().NotContain("\"tags\"");
        json.Should().NotContain("\"stack_trace\"");
        json.Should().NotContain("\"count\"");
    }

    [Fact]
    public void ConfigurationKeyValue_WithSettings_SerializesAsSnakeCase()
    {
        var config = ConfigurationKeyValue.Create(
            name: "DD_TRACE_ENABLED",
            value: true,
            origin: "env_var",
            seqId: 42L,
            error: null);

        var json = JsonConvert.SerializeObject(config, Settings);

        json.Should().Contain("\"name\":");
        json.Should().Contain("\"value\":");
        json.Should().Contain("\"origin\":");
        json.Should().Contain("\"seq_id\":");
        // error is null → omitted by NullValueHandling.Ignore
        json.Should().NotContain("\"error\"");

        // ConfigurationKeyValue is a readonly struct with only internal/private constructors
        // and get-only properties; Newtonsoft.Json cannot populate properties during
        // deserialization, so round-trip produces a default struct with null/zero values.
        var result = JsonConvert.DeserializeObject<ConfigurationKeyValue>(json, Settings);
        result.Name.Should().BeNull();
        result.Value.Should().BeNull();
        result.Origin.Should().BeNull();
        result.SeqId.Should().Be(0L);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void MetricSeries_CustomConverter_NestedArrayFormat()
    {
        var series = new MetricSeries(
        [
            new MetricDataPoint(1700000000L, 10),
            new MetricDataPoint(1700000060L, 20),
        ]);

        // MetricSeries has [JsonConverter(typeof(MetricSeriesJsonConverter))]
        // so it doesn't need settings for its own serialization
        var json = JsonConvert.SerializeObject(series);

        json.Should().Be("[[1700000000,10],[1700000060,20]]");

        var result = JsonConvert.DeserializeObject<MetricSeries>(json);
        result.Should().HaveCount(2);
        result[0].Timestamp.Should().Be(1700000000L);
        result[0].Value.Should().Be(10);
        result[1].Timestamp.Should().Be(1700000060L);
        result[1].Value.Should().Be(20);
    }

    [Fact]
    public void MetricSeries_Empty_SerializesAsEmptyArray()
    {
        var series = new MetricSeries();
        var json = JsonConvert.SerializeObject(series);

        json.Should().Be("[]");

        var result = JsonConvert.DeserializeObject<MetricSeries>(json);
        result.Should().BeEmpty();
    }
}
