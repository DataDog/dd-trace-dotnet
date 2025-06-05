// <copyright file="SpanContextPropagationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Propagators;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ManualInstrumentation;

public class SpanContextPropagationTests
{
    [Fact]
    public void SpanContextExtractorExtractIntegration_WhenUserProvidedGetterReturnsNull_IntegrationCatchesException()
    {
        var headers = new Dictionary<string, string[]>();
        Func<Dictionary<string, string[]>, string, IEnumerable<string>> getter = (dict, key) => null;

        // instance isn't used, so just passing this to avoid generic gymnastics
        var state = SpanContextExtractorExtractIntegration.OnMethodBegin(this, headers, getter);
        var result = state.State as SpanContext;
        result.Should().BeNull();
    }

    [Fact]
    public void SpanContextExtractorExtractIntegration_WhenUserProvidedGetterThrowsException_IntegrationCatchesException()
    {
        var headers = new Dictionary<string, string[]>();
        Func<Dictionary<string, string[]>, string, IEnumerable<string>> getter = (dict, key) => throw new Exception("oops!");

        // instance isn't used, so just passing this to avoid generic gymnastics
        var state = SpanContextExtractorExtractIntegration.OnMethodBegin(this, headers, getter);
        var result = state.State as SpanContext;
        result.Should().BeNull();
    }

    [Fact]
    public void SpanContextExtractorExtractIncludingDsmIntegration_WhenUserProvidedGetterReturnsNull_IntegrationCatchesException()
    {
        var headers = new Dictionary<string, string[]>();
        Func<Dictionary<string, string[]>, string, IEnumerable<string>> getter = (dict, key) => null;

        // instance isn't used, so just passing this to avoid generic gymnastics
        var state = SpanContextExtractorExtractIncludingDsmIntegration.OnMethodBegin(this, headers, getter, "messageType", "source");
        var result = state.State as SpanContext;
        result.Should().BeNull();
    }

    [Fact]
    public void SpanContextExtractorExtractIncludingDsmIntegration_WhenUserProvidedGetterThrowsException_IntegrationCatchesException()
    {
        var headers = new Dictionary<string, string[]>();
        Func<Dictionary<string, string[]>, string, IEnumerable<string>> getter = (dict, key) => throw new Exception("oops!");

        // instance isn't used, so just passing this to avoid generic gymnastics
        var state = SpanContextExtractorExtractIncludingDsmIntegration.OnMethodBegin(this, headers, getter, "messageType", "source");
        var result = state.State as SpanContext;
        result.Should().BeNull();
    }

    [Fact]
    public void SpanContextInjectorInjectIntegration_WhenUserProvidedGetterThrowsException_IntegrationCatchesException()
    {
        var spanContext = new SpanContext(123, 123, SamplingPriority.UserKeep);
        var headers = new Dictionary<string, string[]>();
        Action<Dictionary<string, string[]>, string, string> setter = (dict, key, value) => throw new Exception("oops!");

        // instance isn't used, so just passing this to avoid generic gymnastics
        var state = SpanContextInjectorInjectIntegration.OnMethodBegin(this, headers, setter, spanContext);
        var result = state.State as SpanContext;
        result.Should().BeNull();
    }

    [Fact]
    public void SpanContextInjectorInjectIncludingDsmIntegration_WhenUserProvidedGetterThrowsException_IntegrationCatchesException()
    {
        var headers = new Dictionary<string, string[]>();
        var spanContext = new SpanContext(123, 123, SamplingPriority.UserKeep);
        Action<Dictionary<string, string[]>, string, string> setter = (dict, key, value) => throw new Exception("oops!");

        // instance isn't used, so just passing this to avoid generic gymnastics
        var state = SpanContextInjectorInjectIncludingDsmIntegration.OnMethodBegin(this, headers, setter, spanContext, "messageType", "source");
        var result = state.State as SpanContext;
        result.Should().BeNull();
    }
}
