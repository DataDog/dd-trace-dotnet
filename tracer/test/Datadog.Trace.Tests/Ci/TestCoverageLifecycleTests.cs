// <copyright file="TestCoverageLifecycleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(CoverageGlobalStateTestCollection))]
public class TestCoverageLifecycleTests : SettingsTestsBase
{
    [Fact]
    public void ConstructorFailureAfterCoverageStartAbortsCoverageContext()
    {
        using var harness = new TestHarness();
        var logger = new Mock<IDatadogLogger>();
        logger.Setup(
                   x => x.Debug<string, string, string>(
                       "######### New Test Created: {Name} ({Suite} | {Module})",
                       It.IsAny<string>(),
                       It.IsAny<string>(),
                       It.IsAny<string>(),
                       It.IsAny<int>(),
                       It.IsAny<string>()))
              .Throws(new InvalidOperationException("Injected construction failure."));
        harness.TestOptimizationMock.Setup(x => x.Log).Returns(logger.Object);

        var action = () => harness.Suite.CreateTest("constructor-failure");

        action.Should().Throw<InvalidOperationException>().WithMessage("Injected construction failure.");
        AssertBalancedSuppressedCoverage(harness.GlobalHandler, GlobalCoverageFailureReason.TestConstructionFailed);
    }

    [Fact]
    public void CoverageEndFailureStillClosesAndDisposesTheContext()
    {
        var handler = new ThrowingCoverageEventHandler();
        using var harness = new TestHarness(handler);
        var test = harness.Suite.CreateTest("close-failure");

        var action = () => test.Close(TestStatus.Pass);

        action.Should().Throw<InvalidOperationException>().WithMessage("Injected coverage-end failure.");
        test.IsClosed.Should().BeTrue();
        Test.ActiveTests.Should().NotContain(test);
        handler.ContextDiagnostics.Started.Should().Be(1);
        handler.ContextDiagnostics.Closed.Should().Be(1);
        handler.ContextDiagnostics.Disposed.Should().Be(1);
        handler.Container.Should().BeNull();
    }

    private static void AssertBalancedSuppressedCoverage(DefaultWithGlobalCoverageEventHandler handler, GlobalCoverageFailureReason reason)
    {
        handler.ActiveContexts.Should().Be(0);
        handler.ContextDiagnostics.Started.Should().Be(1);
        handler.ContextDiagnostics.Closed.Should().Be(1);
        handler.ContextDiagnostics.Disposed.Should().Be(1);
        handler.AccumulatorDiagnostics.IsValid.Should().BeFalse();
        handler.AccumulatorDiagnostics.FailureReason.Should().Be(reason);
    }

    private sealed class TestHarness : IDisposable
    {
        private readonly ITestOptimization _previousTestOptimization;
        private readonly CoverageEventHandler _previousCoverageHandler;

        public TestHarness(CoverageEventHandler? handler = null)
        {
            _previousTestOptimization = TestOptimization.Instance;
            _previousCoverageHandler = CoverageReporter.Handler;

            var settings = new TestOptimizationSettings(
                CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoverage, "1")),
                NullConfigurationTelemetry.Instance);
            TestOptimizationMock = new Mock<ITestOptimization>();
            var hostInfo = new Mock<ITestOptimizationHostInfo>();
            hostInfo.Setup(x => x.GetOperatingSystemVersion()).Returns("test-os-version");
            TestOptimizationMock.Setup(x => x.Settings).Returns(settings);
            TestOptimizationMock.Setup(x => x.Log).Returns(DatadogLogging.GetLoggerFor(typeof(TestCoverageLifecycleTests)));
            TestOptimizationMock.Setup(x => x.CIValues).Returns(new TestCIEnvironmentValues(Directory.GetCurrentDirectory()));
            TestOptimizationMock.Setup(x => x.HostInfo).Returns(hostInfo.Object);

            Handler = handler ?? new DefaultWithGlobalCoverageEventHandler();
            TestOptimization.Instance = TestOptimizationMock.Object;
            CoverageReporter.Handler = Handler;
            Session = TestSession.GetOrCreate("dotnet test", workingDirectory: null, framework: "xunit", startDate: null);
            Module = Session.CreateModule("coverage-lifecycle");
            Suite = Module.GetOrCreateSuite("coverage-lifecycle-suite");
        }

        public Mock<ITestOptimization> TestOptimizationMock { get; }

        public CoverageEventHandler Handler { get; }

        public DefaultWithGlobalCoverageEventHandler GlobalHandler => (DefaultWithGlobalCoverageEventHandler)Handler;

        public TestSession Session { get; }

        public TestModule Module { get; }

        public TestSuite Suite { get; }

        public void Dispose()
        {
            Suite.Close();
            Module.Close();
            Session.Close(TestStatus.Pass);
            CoverageReporter.Handler = _previousCoverageHandler;
            TestOptimization.Instance = _previousTestOptimization;
        }
    }

    private sealed class ThrowingCoverageEventHandler : CoverageEventHandler
    {
        protected override void OnSessionStart(CoverageContextContainer context)
        {
        }

        protected override object? OnSessionFinished(CoverageContextContainer context, IReadOnlyList<ModuleValue> modules)
            => throw new InvalidOperationException("Injected coverage-end failure.");
    }

    private sealed class TestCIEnvironmentValues : CIEnvironmentValues
    {
        public TestCIEnvironmentValues(string workspacePath)
        {
            WorkspacePath = workspacePath;
            Repository = "https://github.com/DataDog/dd-trace-dotnet";
            Commit = "abcdef123456";
        }

        protected override void Setup(IGitInfo gitInfo)
        {
        }
    }
}
