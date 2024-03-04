// <copyright file="ManualOnlyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias DatadogTraceManual;

using System;
using DatadogTraceManual::Datadog.Trace.Ci;
using FluentAssertions;
using Xunit;
using BenchmarkDiscreteStats = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkDiscreteStats;
using BenchmarkHostInfo = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkHostInfo;
using BenchmarkJobInfo = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkJobInfo;
using BenchmarkMeasureType = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkMeasureType;
using ManualIScope = DatadogTraceManual::Datadog.Trace.IScope;
using ManualISpan = DatadogTraceManual::Datadog.Trace.ISpan;
using ManualISpanContext = DatadogTraceManual::Datadog.Trace.ISpanContext;
using ManualITest = DatadogTraceManual::Datadog.Trace.Ci.ITest;
using ManualITestModule = DatadogTraceManual::Datadog.Trace.Ci.ITestModule;
using ManualITestSession = DatadogTraceManual::Datadog.Trace.Ci.ITestSession;
using ManualITestSuite = DatadogTraceManual::Datadog.Trace.Ci.ITestSuite;
using TestParameters = DatadogTraceManual::Datadog.Trace.Ci.TestParameters;
using TestStatus = DatadogTraceManual::Datadog.Trace.Ci.TestStatus;

namespace Datadog.Trace.Tests.ManualInstrumentation;

public class ManualOnlyTests
{
    [Fact]
    public void CreatingAManualSpanDoesNotCrash()
    {
        using var scope = DatadogTraceManual::Datadog.Trace.Tracer.Instance.StartActive("manual");
        scope.Should().BeAssignableTo<ManualIScope>().And.NotBeNull();

        var span = scope.Span.Should().NotBeNull().And.BeAssignableTo<ManualISpan>().Subject;
        span.SetException(new Exception());
        span.SetTag("James", "Bond").Should().BeSameAs(span);
        span.GetTag("James").Should().BeNull();
        span.OperationName.Should().BeNullOrEmpty();
        span.ResourceName.Should().BeNullOrEmpty();

        var context = span.Context.Should().NotBeNull().And.BeAssignableTo<ManualISpanContext>().Subject;
        context.Should().NotBeNull();
        context.ServiceName.Should().BeNullOrEmpty();
    }

    [Fact]
    public void CreatingAManualOnlyCiSessionDoesNotCrash()
    {
        var manualSession = TestSession.GetOrCreate("some sesssion");
        manualSession.Should().BeAssignableTo<ManualITestSession>().And.NotBeNull();
        manualSession.SetTag("session_key", "session_value");

        var module = manualSession.CreateModule("somemodule");
        module.Should().BeAssignableTo<ManualITestModule>().And.NotBeNull();
        module.SetTag("module_key", "module_value");
        module.SetErrorInfo(new Exception());

        var suite = module.GetOrCreateSuite("mysuite");
        suite.Should().BeAssignableTo<ManualITestSuite>().And.NotBeNull();
        suite.SetTag("suite_key", "suite_value");

        var test = suite.CreateTest("mytest");
        test.Should().BeAssignableTo<ManualITest>().And.NotBeNull();
        test.SetTag("key", "value");

        test.SetParameters(new TestParameters { Arguments = new(), Metadata = new() });
        test.SetBenchmarkMetadata(new BenchmarkHostInfo() { RuntimeVersion = "123" }, new BenchmarkJobInfo() { Description = "weeble" });
        var stats = new BenchmarkDiscreteStats(100, 100, 100, 100, 100, 0, 0, 0, 0, 100, 100, 100);
        test.AddBenchmarkData(BenchmarkMeasureType.ApplicationLaunch, info: "something", in stats);

        test.Close(TestStatus.Pass);
        suite.Close();
        module.Close();
        manualSession.Close(TestStatus.Pass);
    }
}
