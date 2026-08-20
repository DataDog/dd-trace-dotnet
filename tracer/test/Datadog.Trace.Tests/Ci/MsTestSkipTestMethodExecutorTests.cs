// <copyright file="MsTestSkipTestMethodExecutorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Ci
{
    [Collection(nameof(EnvironmentVariablesTestCollection))]
    public class MsTestSkipTestMethodExecutorTests
    {
        [Fact]
        public void SyncExecutorRecordsCoverageBackfillSkipWhenSpanCannotBeCreated()
        {
            var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
            skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
            var testOptimization = new Mock<ITestOptimization>();
            testOptimization.Setup(x => x.Log).Returns(DatadogLogging.GetLoggerFor<MsTestSkipTestMethodExecutorTests>());
            testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

            var method = typeof(MsTestSkipTestMethodExecutorTests).GetMethod(nameof(SampleTest), BindingFlags.NonPublic | BindingFlags.Static)!;
            var expectedModuleName = method.DeclaringType!.Assembly.GetName().Name!;
            var testMethod = new TestMethodStub(method);
            var previousSuite = TestSuite.Current;
            var previousModule = TestModule.Current;
            TestOptimization.Instance = testOptimization.Object;

            try
            {
                TestSuite.Current = null;
                TestModule.Current = null;
                var executor = new SkipTestMethodExecutor.SyncImpl(
                    typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestResult).Assembly,
                    skipReason: "Skipped by Intelligent Test Runner",
                    recordCoverageBackfillSkip: true);

                var result = executor.Execute(testMethod);

                AssertSkippedResultArray(result);
                skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(expectedModuleName), Times.Once);
            }
            finally
            {
                TestSuite.Current = previousSuite;
                TestModule.Current = previousModule;
                TestOptimization.Instance = new TestOptimization();
                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void SyncExecutorRecordsCoverageBackfillCandidateWhenSpanCannotBeCreated()
        {
            var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
            skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
            var testOptimization = new Mock<ITestOptimization>();
            testOptimization.Setup(x => x.Log).Returns(DatadogLogging.GetLoggerFor<MsTestSkipTestMethodExecutorTests>());
            testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

            var method = typeof(MsTestSkipTestMethodExecutorTests).GetMethod(nameof(SampleTest), BindingFlags.NonPublic | BindingFlags.Static)!;
            var expectedModuleName = method.DeclaringType!.Assembly.GetName().Name!;
            var candidate = new SkippableTest(
                method.Name,
                typeof(MsTestSkipTestMethodExecutorTests).FullName!,
                parameters: null,
                configurations: new TestsConfigurations(
                    osPlatform: "test-os",
                    osVersion: "test-os-version",
                    osArchitecture: "test-arch",
                    runtimeName: ".NET",
                    runtimeVersion: "test-runtime-version",
                    runtimeArchitecture: "test-runtime-arch",
                    custom: new Dictionary<string, string>
                    {
                        [TestTags.Bundle] = expectedModuleName
                    }));
            var testMethod = new TestMethodStub(method);
            var previousSuite = TestSuite.Current;
            var previousModule = TestModule.Current;
            TestOptimization.Instance = testOptimization.Object;

            try
            {
                TestSuite.Current = null;
                TestModule.Current = null;
                var executor = new SkipTestMethodExecutor.SyncImpl(
                    typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestResult).Assembly,
                    skipReason: "Skipped by Intelligent Test Runner",
                    recordCoverageBackfillSkip: true,
                    skippableTest: candidate);

                var result = executor.Execute(testMethod);

                AssertSkippedResultArray(result);
                skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(candidate, expectedModuleName), Times.Once);
                skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(It.IsAny<string>()), Times.Never);
            }
            finally
            {
                TestSuite.Current = previousSuite;
                TestModule.Current = previousModule;
                TestOptimization.Instance = new TestOptimization();
                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public async Task AsyncExecutorRecordsCoverageBackfillCandidateWhenSpanCannotBeCreated()
        {
            var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
            skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
            var testOptimization = new Mock<ITestOptimization>();
            testOptimization.Setup(x => x.Log).Returns(DatadogLogging.GetLoggerFor<MsTestSkipTestMethodExecutorTests>());
            testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

            var method = typeof(MsTestSkipTestMethodExecutorTests).GetMethod(nameof(SampleTest), BindingFlags.NonPublic | BindingFlags.Static)!;
            var expectedModuleName = method.DeclaringType!.Assembly.GetName().Name!;
            var candidate = new SkippableTest(
                method.Name,
                typeof(MsTestSkipTestMethodExecutorTests).FullName!,
                parameters: null,
                configurations: new TestsConfigurations(
                    osPlatform: "test-os",
                    osVersion: "test-os-version",
                    osArchitecture: "test-arch",
                    runtimeName: ".NET",
                    runtimeVersion: "test-runtime-version",
                    runtimeArchitecture: "test-runtime-arch",
                    custom: new Dictionary<string, string>
                    {
                        [TestTags.Bundle] = expectedModuleName
                    }));
            var testMethod = new TestMethodStub(method);
            var previousSuite = TestSuite.Current;
            var previousModule = TestModule.Current;
            TestOptimization.Instance = testOptimization.Object;

            try
            {
                TestSuite.Current = null;
                TestModule.Current = null;
                var executor = new SkipTestMethodExecutor.AsyncImpl(
                    typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestResult).Assembly,
                    skipReason: "Skipped by Intelligent Test Runner",
                    recordCoverageBackfillSkip: true,
                    skippableTest: candidate);

                var task = executor.Execute(testMethod);
                task.Should().BeAssignableTo<Task>();

                await (Task)task;
                var result = task.GetType().GetProperty("Result")!.GetValue(task);

                AssertSkippedResultArray(result);
                skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(candidate, expectedModuleName), Times.Once);
                skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(It.IsAny<string>()), Times.Never);
            }
            finally
            {
                TestSuite.Current = previousSuite;
                TestModule.Current = previousModule;
                TestOptimization.Instance = new TestOptimization();
                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public async Task AsyncExecuteTestIntegrationReplacementExecutorRecordsExactCoverageBackfillCandidate()
        {
            var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
            skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
            var testOptimization = new Mock<ITestOptimization>();
            testOptimization.Setup(x => x.IsRunning).Returns(true);
            testOptimization.Setup(x => x.Log).Returns(DatadogLogging.GetLoggerFor<MsTestSkipTestMethodExecutorTests>());
            testOptimization.Setup(x => x.Settings).Returns(new TestOptimizationSettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance));
            testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

            var method = typeof(MsTestSkipTestMethodExecutorTests).GetMethod(nameof(SampleTest), BindingFlags.NonPublic | BindingFlags.Static)!;
            var expectedModuleName = method.DeclaringType!.Assembly.GetName().Name!;
            var candidate = new SkippableTest(
                method.Name,
                typeof(MsTestSkipTestMethodExecutorTests).FullName!,
                parameters: null,
                configurations: new TestsConfigurations(
                    osPlatform: "test-os",
                    osVersion: "test-os-version",
                    osArchitecture: "test-arch",
                    runtimeName: ".NET",
                    runtimeVersion: "test-runtime-version",
                    runtimeArchitecture: "test-runtime-arch",
                    custom: new Dictionary<string, string>
                    {
                        [TestTags.Bundle] = expectedModuleName
                    }));
            var testMethodInfo = new TestMethodInfoV3_9Stub(method)
            {
                Executor = new OriginalAsyncExecutorStub()
            };
            var runner = new TestMethodRunnerV3_9Stub(testMethodInfo);
            var reason = string.Empty;
            var previousSuite = TestSuite.Current;
            var previousModule = TestModule.Current;

            skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(typeof(MsTestSkipTestMethodExecutorTests).FullName!, method.Name, expectedModuleName)).Returns([candidate]);
            skippableFeature.Setup(x => x.CanSkipWithCoverageBackfill(candidate, expectedModuleName, out reason)).Returns(true);
            TestOptimization.Instance = testOptimization.Object;

            try
            {
                TestSuite.Current = null;
                TestModule.Current = null;

                var originalExecutor = testMethodInfo.Executor;
                var state = TestMethodRunnerExecuteTestIntegrationV3_9.OnMethodBegin(runner, testMethodInfo);

                state.Should().NotBe(CallTargetState.GetDefault());
                testMethodInfo.Executor.Should().NotBeSameAs(originalExecutor);
                var replacementExecutor = testMethodInfo.Executor.Should().BeAssignableTo<OriginalAsyncExecutorStub>().Subject;

                var result = await replacementExecutor.ExecuteAsync(testMethodInfo);

                AssertSkippedResultArray(result);
                skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(candidate, expectedModuleName), Times.Once);
                skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(It.IsAny<string>()), Times.Never);

                TestMethodRunnerExecuteTestIntegrationV3_9.OnAsyncMethodEnd<object, object>(new object(), returnValue: null, exception: null, state);
                testMethodInfo.Executor.Should().BeSameAs(originalExecutor);
            }
            finally
            {
                TestSuite.Current = previousSuite;
                TestModule.Current = previousModule;
                TestOptimization.Instance = new TestOptimization();
                TestOptimization.Instance.Reset();
            }
        }

        private static void SampleTest()
        {
        }

        private static void AssertSkippedResultArray(object? result)
        {
            var resultArray = result.Should().BeAssignableTo<Array>().Subject;
            resultArray.Length.Should().Be(1);
            var testResult = resultArray.GetValue(0).Should().BeOfType<Microsoft.VisualStudio.TestTools.UnitTesting.TestResult>().Subject;
            testResult.Outcome.Should().Be(UnitTestOutcome.Inconclusive);
        }

        private sealed class TestMethodStub : ITestMethod
        {
            private readonly MethodInfo _methodInfo;

            public TestMethodStub(MethodInfo methodInfo)
            {
                _methodInfo = methodInfo;
            }

            public object Instance => this;

            public Type Type => typeof(TestMethodStub);

            public string? TestMethodName => _methodInfo.Name;

            public string? TestClassName => typeof(MsTestSkipTestMethodExecutorTests).FullName;

            public MethodInfo? MethodInfo => _methodInfo;

            public object[]? Arguments => [];

            public ref TReturn? GetInternalDuckTypedInstance<TReturn>()
            {
                throw new NotImplementedException();
            }

            public override string ToString()
            {
                return nameof(TestMethodStub);
            }
        }

        private sealed class TestMethodRunnerV3_9Stub : ITestMethodRunnerV3_9
        {
            public TestMethodRunnerV3_9Stub(ITestMethodInfoV3_9 testMethodInfo)
            {
                TestMethodInfo = testMethodInfo;
            }

            public ITestMethodInfoV3_9 TestMethodInfo { get; }
        }

        private sealed class TestMethodInfoV3_9Stub : ITestMethodInfoV3_9, Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod
        {
            private readonly MethodInfo _methodInfo;

            public TestMethodInfoV3_9Stub(MethodInfo methodInfo)
            {
                _methodInfo = methodInfo;
            }

            public object Instance => this;

            public Type Type => typeof(TestMethodInfoV3_9Stub);

            public string? TestMethodName => _methodInfo.Name;

            public string? TestClassName => typeof(MsTestSkipTestMethodExecutorTests).FullName;

            public MethodInfo? MethodInfo => _methodInfo;

            public object[]? Arguments => [];

            public ITestClassInfo? Parent => null;

            public object? Executor { get; set; }

            public ref TReturn? GetInternalDuckTypedInstance<TReturn>()
            {
                throw new NotImplementedException();
            }

            public override string ToString()
            {
                return nameof(TestMethodInfoV3_9Stub);
            }
        }

        private class OriginalAsyncExecutorStub
        {
            public virtual Task<Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[]> ExecuteAsync(Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod testMethod)
            {
                return Task.FromResult(Array.Empty<Microsoft.VisualStudio.TestTools.UnitTesting.TestResult>());
            }
        }
    }
}
