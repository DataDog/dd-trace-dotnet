// <copyright file="SpanContextInjectorExtractorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

extern alias DatadogTraceManual;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class SpanContextInjectorExtractorTests
{
    [Fact]
    public void BasicInjectionInDict()
    {
        var injector = new SpanContextInjector();
        var headers = new Dictionary<string, string>();
        const ulong traceId = 123456789101112;
        const ulong spanId = 109876543210;
        var context = new SpanContext(traceId, spanId);

        injector.Inject(headers, (h, k, v) => h[k] = v, context);

        headers.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["x-datadog-trace-id"] = traceId.ToString(),
            ["x-datadog-parent-id"] = spanId.ToString(),
            ["traceparent"] = $"00-{traceId:x32}-{spanId:x16}-01",
            ["tracestate"] = $"dd=p:{spanId:x16}",
        });
    }

    [Fact]
    public void BasicInjectionInStringBuilder()
    {
        var injector = new SpanContextInjector();
        var headers = new StringBuilder();
        var context = new SpanContext(traceId: 123456789101112, spanId: 109876543210);

        headers.Append("{");
        injector.Inject(headers, (h, k, v) => h.Append("{" + k + "," + v + "},"), context);
        headers.Length--;
        headers.Append("}");

        headers.ToString().Should().Be("{{x-datadog-trace-id,123456789101112},{x-datadog-parent-id,109876543210},{traceparent,00-000000000000000000007048860f3a38-000000199526feea-01},{tracestate,dd=p:000000199526feea}}");
    }

    [Fact]
    public void ShouldNotInjectIfSpanNotOurs()
    {
        var injector = new SpanContextInjector();
        var headers = new Dictionary<string, string>();
        // type of context is not a Datadog.Trace.SpanContext
        var context = new ReadOnlySpanContext(new TraceId(), spanId: 1, "myService");

        injector.Inject(headers, (h, k, v) => h[k] = v, context);

        headers.Count.Should().Be(expected: 0);
    }

    [Fact]
    public void InjectionWithDsm()
    {
        // enable DSM
        Tracer.Configure(TracerSettings.Create(new Dictionary<string, object> { { ConfigurationKeys.DataStreamsMonitoring.Enabled, true } }));

        var injector = new SpanContextInjector();
        var headers = new Dictionary<string, string>();
        var context = new SpanContext(traceId: 1, spanId: 2);

        injector.InjectIncludingDsm(headers, (h, k, v) => h[k] = v, context, "Pneumatic Tube", "cashier1");

        headers.Keys.Should().BeEquivalentTo(
        [
            // regular trace propagation headers
            "x-datadog-trace-id",
            "x-datadog-parent-id",
            "traceparent",
            "tracestate",
            // DSM specific header
            "dd-pathway-ctx-base64",
        ]);

        // should not throw (i.e. should be valid base64)
        Convert.FromBase64String(headers["dd-pathway-ctx-base64"]).Should().NotBeEmpty();

        context.PathwayContext.Should().NotBeNull();
        // I'd like to check that a checkpoint is sent, but I don't know how.
    }

    [Fact]
    public void NoDsmInjectionIsDisabled()
    {
        // DSM is disabled by default

        var injector = new SpanContextInjector();
        var headers = new Dictionary<string, string>();
        var context = new SpanContext(traceId: 1, spanId: 2);

        injector.InjectIncludingDsm(headers, (h, k, v) => h[k] = v, context, "Pneumatic Tube", "cashier1");

        headers.Keys.Should().BeEquivalentTo(
        [
            // regular trace propagation headers
            "x-datadog-trace-id",
            "x-datadog-parent-id",
            "traceparent",
            "tracestate",
        ]);
    }

    [Fact]
    public void CanExtractAfterInjection()
    {
        var injector = new SpanContextInjector();
        var headers = new Dictionary<string, string>();
        var context = new SpanContext(traceId: 123, spanId: 456);

        injector.Inject(headers, (h, k, v) => h[k] = v, context);

        var extractor = new SpanContextExtractor();

        var newContext = extractor.Extract(
            headers,
            (h, k) =>
            {
                if (h.TryGetValue(k, out var val))
                {
                    return [val];
                }

                return [];
            });

        newContext.Should().NotBeNull();
        newContext.TraceId.Should().Be(context.TraceId);
        newContext.Should().BeOfType(typeof(SpanContext));
        ((SpanContext)newContext).SpanId.Should().Be(context.SpanId);
        ((SpanContext)newContext).SamplingPriority.Should().Be(context.SamplingPriority);
    }

    [Fact]
    public void CanExtractDsmAfterDsmInjection()
    {
        // enable DSM
        Tracer.Configure(TracerSettings.Create(new Dictionary<string, object> { { ConfigurationKeys.DataStreamsMonitoring.Enabled, true } }));

        var injector = new SpanContextInjector();
        var headers = new Dictionary<string, string>();
        var context = new SpanContext(traceId: 123, spanId: 456);

        injector.InjectIncludingDsm(headers, (h, k, v) => h[k] = v, context, "Pneumatic Tube", "cashier1");

        var extractor = new SpanContextExtractor();

        var extractedContext = extractor.ExtractIncludingDsm(
            headers,
            (h, k) =>
            {
                if (h.TryGetValue(k, out var val))
                {
                    return [val];
                }

                return [];
            },
            "Pneumatic Tube",
            "cashier1");

        extractedContext.Should().NotBeNull();
        extractedContext.TraceId.Should().Be(context.TraceId);
        extractedContext.Should().BeOfType(typeof(SpanContext));
        ((SpanContext)extractedContext).SpanId.Should().Be(context.SpanId);
        ((SpanContext)extractedContext).SamplingPriority.Should().Be(context.SamplingPriority);
        ((SpanContext)extractedContext).PathwayContext.Should().NotBeNull();
        ((SpanContext)extractedContext).PathwayContext.Should().NotBeEquivalentTo(context.PathwayContext);
    }

    [Fact]
    public void ShouldReadPathwaySetByKafkaIntegrationOnExtract()
    {
        // enable DSM
        Tracer.Configure(TracerSettings.Create(new Dictionary<string, object> { { ConfigurationKeys.DataStreamsMonitoring.Enabled, true } }));

        var headers = new Dictionary<string, string>
        {
            { "x-datadog-trace-id", "1" },
            { "x-datadog-parent-id", "2" },
            { "x-datadog-sampling-priority", "1" },
            { "x-datadog-origin", "nowhere" },
            { "x-datadog-tags", string.Empty },
            { "traceparent", "00-00000000000000000000000000000001-0000000000000002-01" },
            { "tracestate", string.Empty },
            { DataStreamsPropagationHeaders.TemporaryBase64PathwayContext, "VGhlIHF1aWMWIGJyb3duIGZveCBqdW1wcyBvdmVyIDEzIGxhenkgZG9ncy4=" } // it's a semi-random base64 string that's good enough to pass the parsing checks
        };
        var extractor = new SpanContextExtractor();

        var newContext = extractor.Extract(headers, (h, k) => [h[k]]);

        newContext.Should().NotBeNull();
        ((SpanContext)newContext).PathwayContext.Should().NotBeNull();
        ((SpanContext)newContext).PathwayContext.Value.Hash.Value.Should().Be(expected: 7163385811044755540UL);
    }
}
