// <copyright file="OtlpMapperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Configuration;
using Datadog.Trace.OpenTelemetry;
using Datadog.Trace.OpenTelemetry.Common;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OpenTelemetry;

public class OtlpMapperTests
{
    [Fact]
    public void EmitResourceAttributes_EmitsServiceName()
    {
        var traceChunk = CreateTraceChunk();
        var attributes = new List<KeyValue>();
        OtlpMapper.EmitResourceAttributesFromTraceChunk(in traceChunk, kv => attributes.Add(kv));

        // Uses underlying StubDatadogTracer, so Tracer.DefaultServiceName is "stub-service"
        attributes.Should().Contain(kv => kv.Key == "service.name" && (string)kv.Value! == "stub-service");
    }

    [Fact]
    public void EmitResourceAttributes_EmitsFallbackServiceName_WhenDefaultServiceNameIsNull()
    {
        var span = CreateSpan();
        var spans = new SpanCollection(new[] { span });
        var traceChunk = new TraceChunkModel(spans, span);
        var attributes = new List<KeyValue>();
        OtlpMapper.EmitResourceAttributesFromTraceChunk(in traceChunk, kv => attributes.Add(kv));

        attributes.Should().Contain(kv => kv.Key == "service.name" && (string)kv.Value! == "unknown_service:dotnet");
    }

    [Fact]
    public void EmitResourceAttributes_EmitsServiceVersion_WhenPresent()
    {
        var traceChunk = CreateTraceChunk(serviceVersion: "1.2.3");
        var attributes = new List<KeyValue>();
        OtlpMapper.EmitResourceAttributesFromTraceChunk(in traceChunk, kv => attributes.Add(kv));

        attributes.Should().Contain(kv => kv.Key == "service.version" && (string)kv.Value! == "1.2.3");
    }

    [Fact]
    public void EmitResourceAttributes_OmitsServiceVersion_WhenNull()
    {
        var traceChunk = CreateTraceChunk();
        var attributes = new List<KeyValue>();
        OtlpMapper.EmitResourceAttributesFromTraceChunk(in traceChunk, kv => attributes.Add(kv));

        attributes.Should().NotContain(kv => kv.Key == "service.version");
    }

    [Fact]
    public void EmitResourceAttributes_EmitsEnvironment_WhenPresent()
    {
        var traceChunk = CreateTraceChunk(environment: "production");
        var attributes = new List<KeyValue>();
        OtlpMapper.EmitResourceAttributesFromTraceChunk(in traceChunk, kv => attributes.Add(kv));

        attributes.Should().Contain(kv => kv.Key == "deployment.environment.name" && (string)kv.Value! == "production");
    }

    [Fact]
    public void EmitResourceAttributes_OmitsEnvironment_WhenNull()
    {
        var traceChunk = CreateTraceChunk();
        var attributes = new List<KeyValue>();
        OtlpMapper.EmitResourceAttributesFromTraceChunk(in traceChunk, kv => attributes.Add(kv));

        attributes.Should().NotContain(kv => kv.Key == "deployment.environment.name");
    }

    [Fact]
    public void EmitResourceAttributes_EmitsTelemetrySdkAttributes()
    {
        var traceChunk = CreateTraceChunk();
        var attributes = new List<KeyValue>();
        OtlpMapper.EmitResourceAttributesFromTraceChunk(in traceChunk, kv => attributes.Add(kv));

        attributes.Should().Contain(kv => kv.Key == "telemetry.sdk.name" && (string)kv.Value! == TracerConstants.TelemetrySdkName);
        attributes.Should().Contain(kv => kv.Key == "telemetry.sdk.language" && (string)kv.Value! == TracerConstants.Language);
        attributes.Should().Contain(kv => kv.Key == "telemetry.sdk.version" && (string)kv.Value! == TracerConstants.AssemblyVersion);
    }

    [Fact]
    public async Task EmitResourceAttributes_EmitsGitCommitSha_WhenPresent()
    {
        await using var tracer = CreateTracerWithGitMetadata(commitSha: "abc123", repositoryUrl: "https://github.com/example/repo");
        using var scope = tracer.StartActive("test-operation");
        var span = (Span)scope.Span;
        var traceChunk = new TraceChunkModel(new SpanCollection(new[] { span }));
        var attributes = new List<KeyValue>();
        OtlpMapper.EmitResourceAttributesFromTraceChunk(in traceChunk, kv => attributes.Add(kv));

        attributes.Should().Contain(kv => kv.Key == "git.commit.sha" && (string)kv.Value! == "abc123");
    }

    [Fact]
    public void EmitResourceAttributes_OmitsGitCommitSha_WhenNull()
    {
        var traceChunk = CreateTraceChunk();
        var attributes = new List<KeyValue>();
        OtlpMapper.EmitResourceAttributesFromTraceChunk(in traceChunk, kv => attributes.Add(kv));

        attributes.Should().NotContain(kv => kv.Key == "git.commit.sha");
    }

    [Fact]
    public async Task EmitResourceAttributes_EmitsGitRepositoryUrl_WhenPresent()
    {
        await using var tracer = CreateTracerWithGitMetadata(commitSha: "abc123", repositoryUrl: "https://github.com/example/repo");
        using var scope = tracer.StartActive("test-operation");
        var span = (Span)scope.Span;
        var traceChunk = new TraceChunkModel(new SpanCollection(new[] { span }));
        var attributes = new List<KeyValue>();
        OtlpMapper.EmitResourceAttributesFromTraceChunk(in traceChunk, kv => attributes.Add(kv));

        attributes.Should().Contain(kv => kv.Key == "git.repository_url" && (string)kv.Value! == "https://github.com/example/repo");
    }

    [Fact]
    public void EmitResourceAttributes_OmitsGitRepositoryUrl_WhenNull()
    {
        var traceChunk = CreateTraceChunk();
        var attributes = new List<KeyValue>();
        OtlpMapper.EmitResourceAttributesFromTraceChunk(in traceChunk, kv => attributes.Add(kv));

        attributes.Should().NotContain(kv => kv.Key == "git.repository_url");
    }

    [Fact]
    public void EmitResourceAttributes_EmitsRuntimeId()
    {
        var traceChunk = CreateTraceChunk();
        var attributes = new List<KeyValue>();
        OtlpMapper.EmitResourceAttributesFromTraceChunk(in traceChunk, kv => attributes.Add(kv));

        attributes.Should().Contain(kv => kv.Key == Tags.RuntimeId && (string)kv.Value! == Tracer.RuntimeId);
    }

    [Fact]
    public void EmitAttributesFromSpan_EmitsServiceName()
    {
        var span = CreateSpan();

        var attributes = new List<KeyValue>();
        OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), CreateSpanModel(span), limit: 128);

        attributes.Should().Contain(kv => kv.Key == "service.name" && (string)kv.Value! == span.ServiceName);
    }

    [Fact]
    public void EmitAttributesFromSpan_EmitsResourceName()
    {
        var span = CreateSpan();

        var attributes = new List<KeyValue>();
        OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), CreateSpanModel(span), limit: 128);

        attributes.Should().Contain(kv => kv.Key == "resource.name" && (string)kv.Value! == span.ResourceName);
    }

    [Fact]
    public void EmitAttributesFromSpan_EmitsOperationName()
    {
        var span = CreateSpan();

        var attributes = new List<KeyValue>();
        OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), CreateSpanModel(span), limit: 128);

        attributes.Should().Contain(kv => kv.Key == "operation.name" && (string)kv.Value! == span.OperationName);
    }

    [Fact]
    public void EmitAttributesFromSpan_EmitsSpanType()
    {
        var span = CreateSpan();

        var attributes = new List<KeyValue>();
        OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), CreateSpanModel(span), limit: 128);

        attributes.Should().Contain(kv => kv.Key == "span.type" && (string)kv.Value! == span.Type);
    }

    [Fact]
    public void EmitAttributesFromSpan_EmitsStringTags()
    {
        var span = CreateSpan();
        span.SetTag("http.method", "GET");
        span.SetTag("http.url", "https://example.com");

        var attributes = new List<KeyValue>();
        OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), CreateSpanModel(span), limit: 128);

        attributes.Should().Contain(kv => kv.Key == "http.method" && (string)kv.Value! == "GET");
        attributes.Should().Contain(kv => kv.Key == "http.url" && (string)kv.Value! == "https://example.com");
    }

    [Fact]
    public void EmitAttributesFromSpan_EmitsDoubleMetrics()
    {
        var span = CreateSpan();
        span.SetMetric("my.metric", 42.5);

        var attributes = new List<KeyValue>();
        OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), CreateSpanModel(span), limit: 128);

        attributes.Should().Contain(kv => kv.Key == "my.metric" && (double)kv.Value! == 42.5);
    }

    [Fact]
    public void EmitAttributesFromSpan_EmitsLastParentId_WhenSet()
    {
        var span = CreateSpan();
        span.Context.LastParentId = "0000000000000042";

        var attributes = new List<KeyValue>();
        OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), CreateSpanModel(span), limit: 128);

        attributes.Should().Contain(kv => kv.Key == Tags.LastParentId && (string)kv.Value! == "0000000000000042");
    }

    [Fact]
    public void EmitAttributesFromSpan_OmitsLastParentId_WhenEmpty()
    {
        var span = CreateSpan();

        var attributes = new List<KeyValue>();
        OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), CreateSpanModel(span), limit: 128);

        attributes.Should().NotContain(kv => kv.Key == Tags.LastParentId);
    }

    [Fact]
    public void EmitAttributesFromSpan_EmitsRuntimeId_ForTopLevelSpan()
    {
        var span = CreateSpan();

        var attributes = new List<KeyValue>();
        OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), CreateSpanModel(span), limit: 128);

        attributes.Should().Contain(kv => kv.Key == Tags.RuntimeId && (string)kv.Value! == Tracer.RuntimeId);
    }

    [Fact]
    public void EmitAttributesFromSpan_EmitsOrigin_WhenPresent()
    {
        var span = CreateSpan(origin: "synthetics");

        var spans = new SpanCollection(new[] { span });
        var traceChunk = new TraceChunkModel(spans); // Use the main constructor so Origin is populated from TraceContext
        var spanModel = traceChunk.GetSpanModel(0);

        var attributes = new List<KeyValue>();
        OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), spanModel, limit: 128);

        attributes.Should().Contain(kv => kv.Key == Tags.Origin && (string)kv.Value! == "synthetics");
    }

    [Fact]
    public void EmitAttributesFromSpan_OmitsOrigin_WhenNull()
    {
        var span = CreateSpan();

        var attributes = new List<KeyValue>();
        OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), CreateSpanModel(span), limit: 128);

        attributes.Should().NotContain(kv => kv.Key == Tags.Origin);
    }

    [Fact]
    public void EmitAttributesFromSpan_ReturnsZeroDropped_WhenUnderLimit()
    {
        var span = CreateSpan();
        span.SetTag("single.tag", "value");

        var attributes = new List<KeyValue>();
        int droppedCount = OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), CreateSpanModel(span), limit: 128);

        droppedCount.Should().Be(0);
    }

    [Fact]
    public void EmitAttributesFromSpan_ReturnsDroppedCount_WhenLimitExceeded()
    {
        var span = CreateSpan();
        for (var i = 0; i < 10; i++)
        {
            span.SetTag($"tag.{i}", $"value.{i}");
        }

        var attributes = new List<KeyValue>();
        int droppedCount = OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), CreateSpanModel(span), limit: 3);

        attributes.Should().HaveCount(3);
        droppedCount.Should().BeGreaterThanOrEqualTo(7);
    }

    [Fact]
    public void EmitAttributesFromSpan_DropsTelemetrySdkAttributes()
    {
        var span = CreateSpan();
        span.SetTag("telemetry.sdk.name", "should-be-dropped");
        span.SetTag("telemetry.sdk.language", "should-be-dropped");
        span.SetTag("telemetry.sdk.version", "should-be-dropped");
        span.SetTag("other.tag", "should-be-kept");

        var attributes = new List<KeyValue>();
        OtlpMapper.EmitAttributesFromSpan(kv => attributes.Add(kv), CreateSpanModel(span), limit: 128);

        // Ensures that telemetry SDK attributes (added by OpenTelemetry) are dropped
        attributes.Should().NotContain(kv => kv.Key == "telemetry.sdk.name");
        attributes.Should().NotContain(kv => kv.Key == "telemetry.sdk.language");
        attributes.Should().NotContain(kv => kv.Key == "telemetry.sdk.version");
        attributes.Should().Contain(kv => kv.Key == "other.tag" && (string)kv.Value! == "should-be-kept");
    }

    private static Span CreateSpan(string? origin = null)
    {
        var traceContext = new TraceContext(new StubDatadogTracer());
        traceContext.Origin = origin;
        var spanContext = new SpanContext(parent: null, traceContext, serviceName: null);
        return TestSpanExtensions.CreateSpan(spanContext, DateTimeOffset.UtcNow);
    }

    private static SpanModel CreateSpanModel(Span span)
    {
        var spans = new SpanCollection(new[] { span });
        var traceChunk = new TraceChunkModel(spans, span);
        return traceChunk.GetSpanModel(0);
    }

    private static TraceChunkModel CreateTraceChunk(
        string? serviceVersion = null,
        string? environment = null)
    {
        var span = CreateSpan();
        var traceContext = span.Context.TraceContext;
        if (serviceVersion is not null)
        {
            traceContext.ServiceVersion = serviceVersion;
        }

        if (environment is not null)
        {
            traceContext.Environment = environment;
        }

        return new TraceChunkModel(new SpanCollection(new[] { span }));
    }

    /// <summary>
    /// Creates a <see cref="ScopedTracer"/> with git metadata configured via <c>DD_GIT_COMMIT_SHA</c>
    /// and <c>DD_GIT_REPOSITORY_URL</c> settings.
    /// </summary>
    private static ScopedTracer CreateTracerWithGitMetadata(string commitSha, string repositoryUrl)
    {
        var configSource = new DictionaryConfigurationSource(new Dictionary<string, string>
        {
            { "DD_GIT_COMMIT_SHA", commitSha },
            { "DD_GIT_REPOSITORY_URL", repositoryUrl },
        });
        var settings = new TracerSettings(configSource);
        return TracerHelper.Create(settings);
    }
}
