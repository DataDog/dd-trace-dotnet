// <copyright file="CustomTestFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;
using Datadog.Trace.Configuration;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers
{
    public class CustomTestFramework : XunitTestFramework
    {
        private readonly XUnitTracer _tracer;

        public CustomTestFramework(IMessageSink messageSink)
            : base(messageSink)
        {
            if (Environment.GetEnvironmentVariable("ENABLE_CUSTOM_CI_VISIBILITY") != "true")
            {
                return;
            }

            // We _only_ load from environment here, otherwise we pick up other datadog.json files etc
            // that we need for tests and unintentionally overwrite settings
            var source = new EnvironmentConfigurationSource();
            var settings = new TracerSettings(source);
            settings.TraceBufferSize = 1024 * 1024 * 45; // slightly lower than the 50mb payload agent limit.

            if (string.IsNullOrEmpty(settings.ServiceName))
            {
                // Extract repository name from the git url and use it as a default service name.
                settings.ServiceName = CIVisibility.GetServiceNameFromRepository(CIEnvironmentValues.Repository);
            }

            if (string.IsNullOrEmpty(settings.Environment))
            {
                settings.Environment = "ci";
            }

            var tracerManager = new CITracerManagerFactory()
               .CreateTracerManager(new ImmutableTracerSettings(settings), previous: null);

            _tracer = new XUnitTracer(tracerManager, messageSink);

            LifetimeManager.Instance.AddShutdownTask(_tracer.FlushSpans);

            messageSink.OnMessage(
                new DiagnosticMessage(
                    "Using manual CI visibility. AgentUri: {0}, Service: {1}, Env: {2}, Version: {3}",
                    _tracer.Settings.Exporter.AgentUri,
                    _tracer.Settings.ServiceName,
                    _tracer.Settings.Environment,
                    _tracer.Settings.ServiceVersion));
        }

        public CustomTestFramework(IMessageSink messageSink, Type typeTestedAssembly)
            : this(messageSink)
        {
            var targetPath = GetProfilerTargetFolder();

            if (targetPath != null)
            {
                var file = typeTestedAssembly.Assembly.Location;
                var destination = Path.Combine(targetPath, Path.GetFileName(file));
                File.Copy(file, destination, true);

                messageSink.OnMessage(new DiagnosticMessage("Replaced {0} with {1} to setup code coverage", destination, file));

                return;
            }

            var message = "Could not find the target framework directory";

            messageSink.OnMessage(new DiagnosticMessage(message));

            throw new DirectoryNotFoundException(message);
        }

        internal static string GetProfilerTargetFolder()
        {
            var tracerHome = EnvironmentHelper.GetTracerHomePath();
            var targetFrameworkDirectory = EnvironmentTools.GetTracerTargetFrameworkDirectory();

            var finalDirectory = Path.Combine(tracerHome, targetFrameworkDirectory);

            if (Directory.Exists(finalDirectory))
            {
                return finalDirectory;
            }

            return null;
        }

        protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
        {
            return new CustomExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink, _tracer);
        }

        private class CustomExecutor : XunitTestFrameworkExecutor
        {
            private readonly XUnitTracer _tracer;

            public CustomExecutor(AssemblyName assemblyName, ISourceInformationProvider sourceInformationProvider, IMessageSink diagnosticMessageSink, XUnitTracer tracer)
                : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
            {
                _tracer = tracer;
            }

            protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
            {
                using (var assemblyRunner = new CustomAssemblyRunner(TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions, _tracer))
                {
                    await assemblyRunner.RunAsync();
                    _tracer?.FlushSpans();
                }
            }
        }

        private class CustomAssemblyRunner : XunitTestAssemblyRunner
        {
            private readonly XUnitTracer _tracer;

            public CustomAssemblyRunner(ITestAssembly testAssembly, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions, XUnitTracer tracer)
                : base(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
            {
                _tracer = tracer;
            }

            protected override async Task<RunSummary> RunTestCollectionAsync(IMessageBus messageBus, ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, CancellationTokenSource cancellationTokenSource)
            {
                var result = await new CustomTestCollectionRunner(testCollection, testCases, DiagnosticMessageSink, messageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), cancellationTokenSource, _tracer).RunAsync();
                _tracer?.FlushSpans();
                return result;
            }
        }

        private class CustomTestCollectionRunner : XunitTestCollectionRunner
        {
            private readonly IMessageSink _diagnosticMessageSink;
            private readonly XUnitTracer _tracer;

            public CustomTestCollectionRunner(ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ITestCaseOrderer testCaseOrderer, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, XUnitTracer tracer)
                : base(testCollection, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource)
            {
                _diagnosticMessageSink = diagnosticMessageSink;
                _tracer = tracer;
            }

            protected override Task<RunSummary> RunTestClassAsync(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases)
            {
                return new CustomTestClassRunner(testClass, @class, testCases, _diagnosticMessageSink, MessageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), CancellationTokenSource, CollectionFixtureMappings, _tracer)
                   .RunAsync();
            }
        }

        private class CustomTestClassRunner : XunitTestClassRunner
        {
            private readonly XUnitTracer _tracer;

            public CustomTestClassRunner(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ITestCaseOrderer testCaseOrderer, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, IDictionary<Type, object> collectionFixtureMappings, XUnitTracer tracer)
                : base(testClass, @class, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource, collectionFixtureMappings)
            {
                _tracer = tracer;
            }

            protected override Task<RunSummary> RunTestMethodAsync(ITestMethod testMethod, IReflectionMethodInfo method, IEnumerable<IXunitTestCase> testCases, object[] constructorArguments)
            {
                return new CustomTestMethodRunner(testMethod, Class, method, testCases, DiagnosticMessageSink, MessageBus, new ExceptionAggregator(Aggregator), CancellationTokenSource, constructorArguments, _tracer)
                   .RunAsync();
            }
        }

        private class CustomTestMethodRunner : XunitTestMethodRunner
        {
            private readonly IMessageSink _diagnosticMessageSink;
            private readonly object[] _constructorArguments;
            private readonly XUnitTracer _tracer;

            public CustomTestMethodRunner(ITestMethod testMethod, IReflectionTypeInfo @class, IReflectionMethodInfo method, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, object[] constructorArguments, XUnitTracer tracer)
                : base(testMethod, @class, method, testCases, diagnosticMessageSink, messageBus, aggregator, cancellationTokenSource, constructorArguments)
            {
                _diagnosticMessageSink = diagnosticMessageSink;
                _constructorArguments = constructorArguments;
                _tracer = tracer;
            }

            protected override async Task<RunSummary> RunTestCaseAsync(IXunitTestCase testCase)
            {
                var parameters = string.Empty;

                if (testCase.TestMethodArguments != null)
                {
                    parameters = string.Join(", ", testCase.TestMethodArguments.Select(a => a?.ToString() ?? "null"));
                }

                var test = $"{TestMethod.TestClass.Class.Name}.{TestMethod.Method.Name}({parameters})";

                _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"STARTED: {test}"));

                using var timer = new Timer(
                    _ => _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"WARNING: {test} has been running for more than 15 minutes")),
                    null,
                    TimeSpan.FromMinutes(15),
                    Timeout.InfiniteTimeSpan);

                try
                {
                    var result = testCase switch
                    {
                        XunitTheoryTestCase
                            => await new CustomTheoryTestCaseRunner(
                                   testCase,
                                   testCase.DisplayName,
                                   testCase.SkipReason,
                                   _constructorArguments,
                                   _diagnosticMessageSink,
                                   MessageBus,
                                   Aggregator,
                                   CancellationTokenSource,
                                   _tracer).RunAsync(),
                        _ => await new CustomTestCaseRunner(
                                 testCase,
                                 testCase.DisplayName,
                                 testCase.SkipReason,
                                 _constructorArguments,
                                 testCase.TestMethodArguments,
                                 MessageBus,
                                 Aggregator,
                                 CancellationTokenSource,
                                 _tracer).RunAsync(),
                    };

                    var status = result.Failed > 0 ? "FAILURE" : (result.Skipped > 0 ? "SKIPPED" : "SUCCESS");

                    _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"{status}: {test} ({result.Time}s)"));

                    return result;
                }
                catch (Exception ex)
                {
                    _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"ERROR: {test} ({ex.Message})"));
                    throw;
                }
            }
        }

        private class CustomTheoryTestCaseRunner : XunitTheoryTestCaseRunner
        {
            private readonly XUnitTracer _tracer;

            public CustomTheoryTestCaseRunner(IXunitTestCase testCase, string displayName, string skipReason, object[] constructorArguments, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, XUnitTracer tracer)
                : base(testCase, displayName, skipReason, constructorArguments, diagnosticMessageSink, messageBus, aggregator, cancellationTokenSource)
            {
                _tracer = tracer;
            }

            protected override XunitTestRunner CreateTestRunner(ITest test, IMessageBus messageBus, Type testClass, object[] constructorArguments, MethodInfo testMethod, object[] testMethodArguments, string skipReason, IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
                => new CustomTestRunner(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource, _tracer);
        }

        private class CustomTestCaseRunner : XunitTestCaseRunner
        {
            private readonly XUnitTracer _tracer;

            public CustomTestCaseRunner(IXunitTestCase testCase, string displayName, string skipReason, object[] constructorArguments, object[] testMethodArguments, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, XUnitTracer tracer)
                : base(testCase, displayName, skipReason, constructorArguments, testMethodArguments, messageBus, aggregator, cancellationTokenSource)
            {
                _tracer = tracer;
            }

            protected override XunitTestRunner CreateTestRunner(ITest test, IMessageBus messageBus, Type testClass, object[] constructorArguments, MethodInfo testMethod, object[] testMethodArguments, string skipReason, IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
                => new CustomTestRunner(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource, _tracer);
        }

        private class CustomTestRunner : XunitTestRunner
        {
            private static readonly Type XunitMarkerType = typeof(XunitTestMethodRunner);
            private readonly XUnitTracer _tracer;

            public CustomTestRunner(ITest test, IMessageBus messageBus, Type testClass, object[] constructorArguments, MethodInfo testMethod, object[] testMethodArguments, string skipReason, IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, XUnitTracer tracer)
                : base(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource)
            {
                _tracer = tracer;
            }

            protected override void AfterTestStarting()
            {
                if (_tracer is null || string.IsNullOrEmpty(Test.TestCase.SkipReason))
                {
                    // Only create (And dispose) the test
                    return;
                }

                var runnerInstance = new TestRunnerStruct
                    {
                        Aggregator = new CustomExceptionAggregator(Aggregator),
                        TestCase = new TestCaseStruct
                        {
                            DisplayName =  Test.DisplayName,
                            Traits = Test.TestCase.Traits
                        },
                        TestClass = TestClass,
                        TestMethod = TestMethod,
                        TestMethodArguments = Test.TestCase.TestMethodArguments
                    };

                var scope = XUnitIntegration.CreateScope(ref runnerInstance, XunitMarkerType, _tracer);
                Debug.Assert(scope is null, "A scope was created that won't be disposed");
            }

            protected override Task<decimal> InvokeTestMethodAsync(ExceptionAggregator aggregator)
            {
                return new CustomTestInvoker(Test, MessageBus, TestClass, ConstructorArguments, TestMethod, TestMethodArguments, BeforeAfterAttributes, aggregator, CancellationTokenSource, _tracer).RunAsync();
            }
        }

        private class CustomTestInvoker : XunitTestInvoker
        {
            private static readonly Type XunitMarkerType = typeof(XunitTestMethodRunner);
            private readonly XUnitTracer _tracer;

            public CustomTestInvoker(ITest test, IMessageBus messageBus, Type testClass, object[] constructorArguments, MethodInfo testMethod, object[] testMethodArguments, IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, XUnitTracer tracer)
                : base(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, beforeAfterAttributes, aggregator, cancellationTokenSource)
            {
                _tracer = tracer;
            }

            protected override async Task<decimal> InvokeTestMethodAsync(object testClassInstance)
            {
                var customExceptionAggregator = new CustomExceptionAggregator(Aggregator);
                Scope scope = null;

                if (_tracer is not null)
                {
                    var runnerInstance = new TestRunnerStruct
                    {
                        Aggregator = customExceptionAggregator,
                        TestCase = new TestCaseStruct
                        {
                            DisplayName =  Test.DisplayName,
                            Traits = Test.TestCase.Traits
                        },
                        TestClass = TestClass,
                        TestMethod = TestMethod,
                        TestMethodArguments = Test.TestCase.TestMethodArguments
                    };

                    scope = XUnitIntegration.CreateScope(ref runnerInstance, XunitMarkerType, _tracer);
                }

                try
                {
                    return await base.InvokeTestMethodAsync(testClassInstance);
                }
                finally
                {
                    if (scope is not null)
                    {
                        XUnitIntegration.FinishScope(scope, customExceptionAggregator);
                    }
                }
            }
        }

        private class CustomExceptionAggregator : IExceptionAggregator
        {
            private readonly ExceptionAggregator _aggregator;

            public CustomExceptionAggregator(ExceptionAggregator aggregator)
            {
                _aggregator = aggregator;
            }

            public Exception ToException() => _aggregator.ToException();
        }

        private class XUnitTracer : Tracer
        {
            private readonly IMessageSink _messageSink;

            public XUnitTracer(TracerManager tracerManager, IMessageSink messageSink)
                : base(tracerManager)
            {
                _messageSink = messageSink;
            }

            public void FlushSpans()
            {
                try
                {
                    var flushThread = new Thread(() => InternalFlush().GetAwaiter().GetResult());
                    flushThread.IsBackground = false;
                    flushThread.Name = "FlushThread";
                    flushThread.Start();
                    flushThread.Join();
                }
                catch (Exception ex)
                {
                    _messageSink.OnMessage(new DiagnosticMessage("Exception occurred when flushing spans. {0}", ex));
                }

                async Task InternalFlush()
                {
                    try
                    {
                        // We have to ensure the flush of the buffer after we finish the tests of an assembly.
                        // For some reason, sometimes when all test are finished none of the callbacks to handling the tracer disposal is triggered.
                        // So the last spans in buffer aren't send to the agent.
                        _messageSink.OnMessage(new DiagnosticMessage("Flushing spans"));
                        await FlushAsync().ConfigureAwait(false);
                        _messageSink.OnMessage(new DiagnosticMessage("CI spans flushed"));
                    }
                    catch (Exception ex)
                    {
                        _messageSink.OnMessage(new DiagnosticMessage("Exception occurred when flushing spans. {0}", ex));
                    }
                }
            }
        }
    }
}
