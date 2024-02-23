// <copyright file="ManualOnlyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias DatadogTraceManual;

using System;
using FluentAssertions;
using Xunit;
using BenchmarkDiscreteStats = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkDiscreteStats;
using BenchmarkHostInfo = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkHostInfo;
using BenchmarkJobInfo = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkJobInfo;
using BenchmarkMeasureType = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkMeasureType;
using ManualTest = DatadogTraceManual::Datadog.Trace.Ci.ManualTest;
using ManualTestModule = DatadogTraceManual::Datadog.Trace.Ci.ManualTestModule;
using ManualTestSession = DatadogTraceManual::Datadog.Trace.Ci.ManualTestSession;
using ManualTestSuite = DatadogTraceManual::Datadog.Trace.Ci.ManualTestSuite;
using TestParameters = DatadogTraceManual::Datadog.Trace.Ci.TestParameters;
using TestStatus = DatadogTraceManual::Datadog.Trace.Ci.TestStatus;

namespace Datadog.Trace.Tests.ManualInstrumentation;

public class ManualOnlyTests
{
    [Fact]
    public void CreatingAManualOnlyCiSessionDoesNotCrash()
    {
        var manualSession = DatadogTraceManual::Datadog.Trace.Ci.TestSession.GetOrCreate("some sesssion");
        manualSession.Should().BeOfType<ManualTestSession>().And.NotBeNull();
        manualSession.SetTag("session_key", "session_value");

        var module = manualSession.CreateModule("somemodule");
        module.Should().BeOfType<ManualTestModule>().And.NotBeNull();
        module.SetTag("module_key", "module_value");
        module.SetErrorInfo(new Exception());

        var suite = module.GetOrCreateSuite("mysuite");
        suite.Should().BeOfType<ManualTestSuite>().And.NotBeNull();
        suite.SetTag("suite_key", "suite_value");

        var test = suite.CreateTest("mytest");
        test.Should().BeOfType<ManualTest>().And.NotBeNull();
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
