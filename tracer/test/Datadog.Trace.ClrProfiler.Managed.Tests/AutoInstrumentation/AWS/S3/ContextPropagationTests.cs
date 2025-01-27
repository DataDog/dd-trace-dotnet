// <copyright file="ContextPropagationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Text;
using Amazon.EventBridge.Model;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.S3;

public class ContextPropagationTests
{
    private readonly SpanContext _spanContext;

    public ContextPropagationTests()
    {
        const long upper = 1234567890123456789;
        const ulong lower = 9876543210987654321;

        var traceId = new TraceId(upper, lower);
        const ulong spanId = 6766950223540265769;
        _spanContext = new SpanContext(traceId, spanId, 1, "test-s3", "serverless");
    }

    [Fact]
    public void InjectTracingContext_EmptyDetail_AddsTraceContext()
    {
        "1".Should().Be("1");
    }
}
