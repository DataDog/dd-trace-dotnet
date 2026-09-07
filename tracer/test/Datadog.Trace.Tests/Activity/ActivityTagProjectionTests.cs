// <copyright file="ActivityTagProjectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Activity;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Activity;

/// <summary>
/// Pins the set of "reserved" OTel keys that <see cref="OtlpHelpers.AgentSetOtlpTag"/> diverts away from
/// normal tag storage against what <see cref="ActivityTagProjection"/> knows to re-emit on the read side.
/// If <c>AgentSetOtlpTag</c>'s switch ever changes which keys are diverted, these tests should fail —
/// either the round-trip test (a diverted key no longer restored) or the "survives as a literal tag" test
/// (a key we assumed diverted no longer is, or vice versa).
/// </summary>
public class ActivityTagProjectionTests
{
    public static IEnumerable<object[]> ReservedKeys =>
        new List<object[]>
        {
            new object[] { "operation.name", "my-operation" },
            new object[] { "service.name", "my-service" },
            new object[] { "resource.name", "my-resource" },
            new object[] { "span.type", "web" },
            new object[] { "http.response.status_code", "200" },
        };

    [Theory]
    [MemberData(nameof(ReservedKeys))]
    public async Task ReservedKeys_AreDivertedFromTagStorage_AndRestoredByProjection(string key, string value)
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        using var span = tracer.StartSpan("operation", new OpenTelemetryTags());

        // Same dispatcher Activity.SetTag(key, object) goes through via OtlpHelpers.SetTagObject.
        OtlpHelpers.SetTagObject(span, key, value);

        // The reserved key must not be retrievable as a literal tag under its own name — it was
        // diverted to a span field (or, for http.response.status_code, a differently-named tag).
        span.GetTag(key).Should().BeNull($"'{key}' should be diverted to a span field, not stored as a literal tag");

        // ActivityTagProjection must restore exactly that key/value pair from the span field.
        var items = new List<KeyValuePair<string, string>>();
        var processor = new CapturingProcessor(items);
        ActivityTagProjection.ProjectReservedKeys(span, ref processor);

        items.Should().ContainSingle(kvp => kvp.Key == key && kvp.Value == value);
    }

    [Fact]
    public async Task OtelStatusCode_IsNotReReservedByProjection_ItAlreadySurvivesAsALiteralTag()
    {
        // otel.status_code is normalised but NOT diverted away from tag storage, so it is already visible
        // via the normal EnumerateTags path. ProjectReservedKeys must not also emit it — that would
        // duplicate the key in get_Tags/get_TagObjects.
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        using var span = tracer.StartSpan("operation", new OpenTelemetryTags());

        OtlpHelpers.SetTagObject(span, "otel.status_code", "OK");

        span.GetTag("otel.status_code").Should().Be("STATUS_CODE_OK");

        var items = new List<KeyValuePair<string, string>>();
        var processor = new CapturingProcessor(items);
        ActivityTagProjection.ProjectReservedKeys(span, ref processor);

        items.Should().NotContain(kvp => kvp.Key == "otel.status_code");
    }

    [Fact]
    public async Task AnalyticsEvent_IsDivertedToADifferentlyNamedMetric_NotRestoredByProjection()
    {
        // Documents a known, deliberately out-of-scope gap: "analytics.event" is diverted into the
        // Tags.Analytics *metric* (not a span field), which ActivityTagProjection does not currently
        // re-emit under its original key. Unlike the 5 reserved keys above, this one is not in the
        // plan's approved list for this pass.
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        using var span = tracer.StartSpan("operation", new OpenTelemetryTags());

        OtlpHelpers.SetTagObject(span, "analytics.event", "true");

        span.GetTag("analytics.event").Should().BeNull();
        span.GetMetric(Tags.Analytics).Should().Be(1);

        var items = new List<KeyValuePair<string, string>>();
        var processor = new CapturingProcessor(items);
        ActivityTagProjection.ProjectReservedKeys(span, ref processor);

        items.Should().NotContain(kvp => kvp.Key == "analytics.event");
    }

    [Theory]
    [MemberData(nameof(ReservedKeys))]
    public async Task GetTagItem_ReservedKeys_LooksUpFromSpanField(string key, string value)
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        using var span = tracer.StartSpan("operation", new OpenTelemetryTags());

        OtlpHelpers.SetTagObject(span, key, value);

        ActivityTagProjection.GetTagItem(span, key).Should().Be(value);
    }

    [Fact]
    public async Task GetTagItem_OrdinaryTag_ReturnsTheTagValue()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        using var span = tracer.StartSpan("operation", new OpenTelemetryTags());

        OtlpHelpers.SetTagObject(span, "some.tag", "some-value");

        ActivityTagProjection.GetTagItem(span, "some.tag").Should().Be("some-value");
    }

    [Fact]
    public async Task GetTagItem_NumericTag_ReturnsTheMetricAsADouble()
    {
        // Numeric attributes are routed to Span.SetMetric by OtlpHelpers.SetTagObject, not SetTag —
        // GetTagItem must check metrics too, or activity.GetTagItem("size") on a numeric tag returns null.
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        using var span = tracer.StartSpan("operation", new OpenTelemetryTags());

        OtlpHelpers.SetTagObject(span, "request.size", 1024);

        ActivityTagProjection.GetTagItem(span, "request.size").Should().Be(1024d);
    }

    [Fact]
    public async Task GetTagItem_UnknownKey_ReturnsNull()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        using var span = tracer.StartSpan("operation", new OpenTelemetryTags());

        ActivityTagProjection.GetTagItem(span, "never.set").Should().BeNull();
    }

    private struct CapturingProcessor : IItemProcessor<string>
    {
        private readonly List<KeyValuePair<string, string>> _items;

        public CapturingProcessor(List<KeyValuePair<string, string>> items) => _items = items;

        public void Process(TagItem<string> item) => _items.Add(new KeyValuePair<string, string>(item.Key, item.Value));
    }
}
