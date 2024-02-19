// <copyright file="DuckTypingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias DatadogTraceManual;

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;
using BenchmarkHostInfo = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkHostInfo;
using BenchmarkJobInfo = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkJobInfo;
using CustomIScope = DatadogTraceManual::Datadog.Trace.IScope;
using CustomISpan = DatadogTraceManual::Datadog.Trace.ISpan;
using CustomISpanContext = DatadogTraceManual::Datadog.Trace.ISpanContext;
using ITestSession = DatadogTraceManual::Datadog.Trace.Ci.ITestSession;
using ManualScope = DatadogTraceManual::Datadog.Trace.ManualScope;
using ManualSpan = DatadogTraceManual::Datadog.Trace.ManualSpan;
using ManualSpanContext = DatadogTraceManual::Datadog.Trace.ManualSpanContext;
using TestParameters = DatadogTraceManual::Datadog.Trace.Ci.TestParameters;
using TestStatus = DatadogTraceManual::Datadog.Trace.Ci.TestStatus;

namespace Datadog.Trace.Tests.ManualInstrumentation;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class DuckTypingTests
{
    private readonly AsyncLocalScopeManager _scopeManager = new();
    private readonly TracerSettings _settings = new() { StartupDiagnosticLogEnabled = false };
    private readonly Tracer _tracer;

    public DuckTypingTests()
    {
        _tracer = new Tracer(_settings, new Mock<IAgentWriter>().Object, new Mock<ITraceSampler>().Object, scopeManager: _scopeManager, statsd: null);
    }

    [Fact]
    public void CanDuckTypeManualSpanContextAsISpanContext()
    {
        var scope = _tracer.StartActiveInternal("manual");
        var spanContext = scope.Span.Context;
        var obj = ScopeHelper<CustomISpanContext>.CreateManualSpanContext(spanContext);

        // verify properties are ok
        var manualContext = obj.Should().BeOfType<ManualSpanContext>().Subject;
        manualContext.Should().NotBeNull();
        manualContext.AutomaticContext.Should().Be(spanContext);
        manualContext.ServiceName.Should().Be(spanContext.ServiceName);
        manualContext.SpanId.Should().Be(spanContext.SpanId);
        manualContext.TraceId.Should().Be(spanContext.TraceId);
    }

    [Fact]
    public void CanDuckTypeManualScopeAsIScope()
    {
        var scope = _tracer.StartActiveInternal("manual");
        var obj = ScopeHelper<CustomIScope>.CreateManualScope(scope);
        var span = scope.Span;

        // verify properties are ok
        var manualScope = obj.Should().BeOfType<ManualScope>().Subject;
        manualScope.Should().NotBeNull();
        manualScope.AutomaticScope.Should().Be(scope);

        // call all the properties to check for duck typing issues
        var manualSpan = manualScope.Span.Should().BeOfType<ManualSpan>().Subject;
        manualSpan.AutomaticSpan.Should().Be(span);
        manualSpan.SpanId.Should().Be(span.SpanId);
        manualSpan.Context.Should().BeOfType<ManualSpanContext>();
        manualSpan.SetException(new Exception("MyException"));
        manualSpan.Error.Should().Be(span.Error).And.BeTrue();
        manualSpan.Type.Should().Be(span.Type);
        manualSpan.OperationName.Should().Be(span.OperationName);
        manualSpan.ResourceName.Should().Be(span.ResourceName);
        manualSpan.ServiceName.Should().Be(span.ServiceName);
        manualSpan.TraceId.Should().Be(span.TraceId);
        manualSpan.SetTag("Test", "SomeValue");
        manualSpan.GetTag("Test").Should().Be("SomeValue");
        span.GetTag("Test").Should().Be("SomeValue"); // check it was mirrored

        manualScope.Close();
        manualScope.Dispose();
    }

    [Fact]
    public void CanDuckTypeManualTestSessionAsISession()
    {
        var session = TestSession.GetOrCreate("blah");
        var proxy = (ITestSession)TestObjectsHelper<ITestSession>.CreateTestSession(session);
        proxy.Should().NotBeNull();

        // call the methods to make sure it works
        var module = proxy.CreateModule("somemodule");
        module.Should().NotBeNull();

        var suite = module.GetOrCreateSuite("mysuite");
        suite.Should().NotBeNull();

        var test = suite.CreateTest("mytest");
        test.Should().NotBeNull();

        test.SetParameters(new TestParameters { Arguments = new(), Metadata = new() });
        test.SetBenchmarkMetadata(new BenchmarkHostInfo() { RuntimeVersion = "123" }, new BenchmarkJobInfo() { Description = "weeble" });

        test.Close(TestStatus.Pass);
        suite.Close();
        module.Close();
        proxy.Close(TestStatus.Pass);
    }
}
