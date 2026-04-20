// <copyright file="TraceFilterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.TraceSamplers;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Agent;

public class TraceFilterTests
{
    [Fact]
    public void EmptyFilter_KeepsAllTraces()
    {
        var filter = new TraceFilter(AgentTraceFilterConfig.Empty);
        var span = CreateRootSpan();
        span.ResourceName = "GET /users";

        filter.ShouldKeepTrace(span).Should().BeTrue();
    }

    [Fact]
    public void IgnoreResources_RejectsMatchingResource()
    {
        var config = new AgentTraceFilterConfig(null, null, null, null, ["(GET|POST) /healthcheck", "GET /ping"]);
        var filter = new TraceFilter(config);

        var healthcheck = CreateRootSpan();
        healthcheck.ResourceName = "GET /healthcheck";
        filter.ShouldKeepTrace(healthcheck).Should().BeFalse();

        var ping = CreateRootSpan();
        ping.ResourceName = "GET /ping";
        filter.ShouldKeepTrace(ping).Should().BeFalse();

        var users = CreateRootSpan();
        users.ResourceName = "GET /users";
        filter.ShouldKeepTrace(users).Should().BeTrue();
    }

    [Fact]
    public void FilterTagsReject_RejectsMatchingTag()
    {
        var config = new AgentTraceFilterConfig(null, ["debug", "env:test"], null, null, null);
        var filter = new TraceFilter(config);

        // Key-only reject: any span with "debug" tag
        var debugSpan = CreateRootSpan();
        debugSpan.SetTag("debug", "true");
        filter.ShouldKeepTrace(debugSpan).Should().BeFalse();

        // Key:value reject
        var testSpan = CreateRootSpan();
        testSpan.SetTag("env", "test");
        filter.ShouldKeepTrace(testSpan).Should().BeFalse();

        // Different value: not rejected
        var prodSpan = CreateRootSpan();
        prodSpan.SetTag("env", "prod");
        filter.ShouldKeepTrace(prodSpan).Should().BeTrue();
    }

    [Fact]
    public void FilterTagsRequire_RejectsWhenMissing()
    {
        var config = new AgentTraceFilterConfig(["env:prod"], null, null, null, null);
        var filter = new TraceFilter(config);

        // Missing required tag: rejected
        var noEnvSpan = CreateRootSpan();
        filter.ShouldKeepTrace(noEnvSpan).Should().BeFalse();

        // Wrong value: rejected
        var testSpan = CreateRootSpan();
        testSpan.SetTag("env", "test");
        filter.ShouldKeepTrace(testSpan).Should().BeFalse();

        // Correct value: kept
        var prodSpan = CreateRootSpan();
        prodSpan.SetTag("env", "prod");
        filter.ShouldKeepTrace(prodSpan).Should().BeTrue();
    }

    [Fact]
    public void FilterTagsRegexReject_RejectsMatchingPattern()
    {
        var config = new AgentTraceFilterConfig(null, null, null, ["version:.*-beta"], null);
        var filter = new TraceFilter(config);

        // Tags set via Tags.SetTag go into the additional tags list
        // which is enumerated by EnumerateTags
        var betaSpan = CreateRootSpan();
        betaSpan.Tags.SetTag("version", "1.0.0-beta");
        filter.ShouldKeepTrace(betaSpan).Should().BeFalse();

        var stableSpan = CreateRootSpan();
        stableSpan.Tags.SetTag("version", "1.0.0");
        filter.ShouldKeepTrace(stableSpan).Should().BeTrue();
    }

    [Fact]
    public void FilterOrder_ResourceThenRejectThenRequire()
    {
        // Resource reject takes priority over require
        var config = new AgentTraceFilterConfig(
            FilterTagsRequire: ["env:prod"],
            FilterTagsReject: null,
            FilterTagsRegexRequire: null,
            FilterTagsRegexReject: null,
            IgnoreResources: ["GET /healthcheck"]);
        var filter = new TraceFilter(config);

        // Even with correct required tag, resource reject wins
        var span = CreateRootSpan();
        span.ResourceName = "GET /healthcheck";
        span.SetTag("env", "prod");
        filter.ShouldKeepTrace(span).Should().BeFalse();
    }

    private static Span CreateRootSpan()
    {
        var tracer = new StubDatadogTracer();
        var traceContext = new TraceContext(tracer);
        var context = new SpanContext(null, traceContext, "test-service");
        return new Span(context, DateTimeOffset.UtcNow);
    }
}
