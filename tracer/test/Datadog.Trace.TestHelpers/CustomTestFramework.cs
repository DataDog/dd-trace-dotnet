// <copyright file="CustomTestFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
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

            var settings = TracerSettings.FromDefaultSources();
            settings.TraceBufferSize = 1024 * 1024 * 45; // slightly lower than the 50mb payload agent limit.

            if (string.IsNullOrEmpty(settings.ServiceName))
            {
                // Extract repository name from the git url and use it as a default service name.
                settings.ServiceName = CIVisibility.GetServiceNameFromRepository(CIEnvironmentValues.Repository);
            }

            var tracerManager = new CITracerManagerFactory()
               .CreateTracerManager(new ImmutableTracerSettings(settings), previous: null);

            _tracer = new XUnitTracer(tracerManager, messageSink);

            LifetimeManager.Instance.AddShutdownTask(_tracer.FlushSpans);
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
                    _tracer.FlushSpans();
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
                _tracer.FlushSpans();
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
                return new CustomTestMethodRunner(testMethod, this.Class, method, testCases, this.DiagnosticMessageSink, this.MessageBus, new ExceptionAggregator(this.Aggregator), this.CancellationTokenSource, constructorArguments, _tracer)
                   .RunAsync();
            }
        }

        private class CustomTestMethodRunner : XunitTestMethodRunner
        {
            private static readonly Type XunitMarkerType = typeof(XunitTestMethodRunner);

            private readonly IMessageSink _diagnosticMessageSink;
            private readonly XUnitTracer _tracer;

            public CustomTestMethodRunner(ITestMethod testMethod, IReflectionTypeInfo @class, IReflectionMethodInfo method, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, object[] constructorArguments, XUnitTracer tracer)
                : base(testMethod, @class, method, testCases, diagnosticMessageSink, messageBus, aggregator, cancellationTokenSource, constructorArguments)
            {
                _diagnosticMessageSink = diagnosticMessageSink;
                _tracer = tracer;
            }

            protected override async Task<RunSummary> RunTestCaseAsync(IXunitTestCase testCase)
            {
                Scope scope = null;
                TestRunnerStruct runnerInstance = default;

                if (_tracer is not null)
                {
                    runnerInstance = new TestRunnerStruct
                    {
                        Aggregator = Aggregator.DuckCast<IExceptionAggregator>(),
                        TestCase = testCase.DuckCast<TestCaseStruct>(),
                        TestClass = Class.Type,
                        TestMethod = Method.MethodInfo,
                        TestMethodArguments = testCase.TestMethodArguments
                    };
                    scope = XUnitIntegration.CreateScope(ref runnerInstance, XunitMarkerType, _tracer);
                }

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
                    var result = await base.RunTestCaseAsync(testCase);

                    var status = result.Failed > 0 ? "FAILURE" : "SUCCESS";

                    _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"{status}: {test} ({result.Time}s)"));

                    return result;
                }
                catch (Exception ex)
                {
                    _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"ERROR: {test} ({ex.Message})"));
                    throw;
                }
                finally
                {
                    if (scope is not null)
                    {
                        XUnitIntegration.FinishScope(scope, runnerInstance.Aggregator);
                    }
                }
            }
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
                        _messageSink.OnMessage(new DiagnosticMessage("Integration flushed"));
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
