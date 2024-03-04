// <copyright file="DuckTypingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias DatadogTraceManual;

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
        var proxyObject = ScopeHelper<CustomISpanContext>.CreateManualSpanContext(spanContext);

        // verify properties are ok
        var proxy = proxyObject.Proxy as CustomISpanContext;
        proxy.Should().NotBeNull();
        proxy.ServiceName.Should().Be(spanContext.ServiceName);
        proxy.SpanId.Should().Be(spanContext.SpanId);
        proxy.TraceId.Should().Be(spanContext.TraceId);
    }

    [Fact]
    public void CanDuckTypeManualScopeAsIScope()
    {
        var scope = _tracer.StartActiveInternal("manual");
        var proxyObject = ScopeHelper<CustomIScope>.CreateManualScope(scope);

        // verify properties are ok
        var proxy = proxyObject.Proxy as CustomIScope;
        proxy.Should().NotBeNull();
        proxy.Span.Should().NotBeNull();
        proxy.Close();
        proxy.Dispose();
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
