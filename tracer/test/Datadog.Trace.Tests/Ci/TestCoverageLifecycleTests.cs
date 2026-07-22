// <copyright file="TestCoverageLifecycleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
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
    public void ConstructorFailureAfterHandleAssignmentAbortsCoverageContext()
    {
        using var harness = new TestHarness();
        Test.LifecycleCallbackForTests = checkpoint =>
        {
            if (checkpoint == TestLifecycleCheckpoint.ConstructionCoverageHandleInstalled)
            {
                throw new InvalidOperationException("Injected construction failure.");
            }
        };

        var action = () => harness.Suite.CreateTest("constructor-failure");

        action.Should().Throw<InvalidOperationException>().WithMessage("Injected construction failure.");
        AssertBalancedSuppressedCoverage(harness.Handler, GlobalCoverageFailureReason.TestConstructionFailed);
    }

    [Fact]
    public void CloseFailureAfterHandleExchangeAbortsCoverageContext()
    {
        using var harness = new TestHarness();
        var test = harness.Suite.CreateTest("close-failure");
        Test.LifecycleCallbackForTests = checkpoint =>
        {
            if (checkpoint == TestLifecycleCheckpoint.CloseCoverageHandleDetached)
            {
                throw new InvalidOperationException("Injected close failure.");
            }
        };

        var action = () => test.Close(TestStatus.Pass);

        action.Should().Throw<InvalidOperationException>().WithMessage("Injected close failure.");
        test.IsClosed.Should().BeTrue();
        Test.ActiveTests.Should().NotContain(test);
        AssertBalancedSuppressedCoverage(harness.Handler, GlobalCoverageFailureReason.TestCloseBeforeCoverage);
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

        internal TestHarness()
        {
            _previousTestOptimization = TestOptimization.Instance;
            _previousCoverageHandler = CoverageReporter.Handler;

            var settings = new TestOptimizationSettings(
                CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoverage, "1")),
                NullConfigurationTelemetry.Instance);
            var testOptimization = new Mock<ITestOptimization>();
            var hostInfo = new Mock<ITestOptimizationHostInfo>();
            hostInfo.Setup(x => x.GetOperatingSystemVersion()).Returns("test-os-version");
            testOptimization.Setup(x => x.Settings).Returns(settings);
            testOptimization.Setup(x => x.Log).Returns(DatadogLogging.GetLoggerFor(typeof(TestCoverageLifecycleTests)));
            testOptimization.Setup(x => x.CIValues).Returns(new TestCIEnvironmentValues(Directory.GetCurrentDirectory()));
            testOptimization.Setup(x => x.HostInfo).Returns(hostInfo.Object);

            Handler = new DefaultWithGlobalCoverageEventHandler();
            TestOptimization.Instance = testOptimization.Object;
            CoverageReporter.Handler = Handler;
            Session = TestSession.GetOrCreate("dotnet test", workingDirectory: null, framework: "xunit", startDate: null);
            Module = Session.CreateModule("coverage-lifecycle");
            Suite = Module.GetOrCreateSuite("coverage-lifecycle-suite");
        }

        internal DefaultWithGlobalCoverageEventHandler Handler { get; }

        internal TestSession Session { get; }

        internal TestModule Module { get; }

        internal TestSuite Suite { get; }

        public void Dispose()
        {
            Test.LifecycleCallbackForTests = null;
            Suite.Close();
            Module.Close();
            Session.Close(TestStatus.Pass);
            CoverageReporter.Handler = _previousCoverageHandler;
            TestOptimization.Instance = _previousTestOptimization;
        }
    }

    private sealed class TestCIEnvironmentValues : CIEnvironmentValues
    {
        internal TestCIEnvironmentValues(string workspacePath)
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
