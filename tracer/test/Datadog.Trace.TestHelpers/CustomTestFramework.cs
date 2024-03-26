// <copyright file="CustomTestFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers.FluentAssertionsExtensions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers
{
    public class CustomTestFramework : XunitTestFramework
    {
        public CustomTestFramework(IMessageSink messageSink)
            : base(messageSink)
        {
            FluentAssertions.Formatting.Formatter.AddFormatter(new DiffPaneModelFormatter());

            if (bool.Parse(Environment.GetEnvironmentVariable("enable_crash_dumps") ?? "false"))
            {
                var progress = new Progress<string>(message => messageSink.OnMessage(new DiagnosticMessage(message)));

                try
                {
                    MemoryDumpHelper.InitializeAsync(progress).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    messageSink.OnMessage(new DiagnosticMessage($"MemoryDumpHelper initialization failed: {ex}"));
                }
            }
        }

        public CustomTestFramework(IMessageSink messageSink, Type typeTestedAssembly)
            : this(messageSink)
        {
            var targetPath = GetMonitoringHomeTargetFrameworkFolder();

            if (targetPath != null)
            {
                var file = typeTestedAssembly.Assembly.Location;
                var destination = Path.Combine(targetPath, Path.GetFileName(file));
                File.Copy(file, destination, true);

                messageSink.OnMessage(new DiagnosticMessage("Replaced {0} with {1} to setup code coverage", destination, file));
            }
            else
            {
                var message = "Could not find the target framework directory";

                messageSink.OnMessage(new DiagnosticMessage(message));
                throw new DirectoryNotFoundException(message);
            }
        }

        internal static string GetMonitoringHomeTargetFrameworkFolder()
        {
            var tracerHome = EnvironmentHelper.GetMonitoringHomePath();
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
            return new CustomExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
        }

        private class CustomExecutor : XunitTestFrameworkExecutor
        {
            public CustomExecutor(AssemblyName assemblyName, ISourceInformationProvider sourceInformationProvider, IMessageSink diagnosticMessageSink)
                : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
            {
            }

            protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
            {
                using (var assemblyRunner = new CustomAssemblyRunner(TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions))
                {
                    await assemblyRunner.RunAsync();
                }
            }
        }

        private class CustomAssemblyRunner : XunitTestAssemblyRunner
        {
            public CustomAssemblyRunner(ITestAssembly testAssembly, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
                : base(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
            {
            }

            protected override async Task<RunSummary> RunTestCollectionsAsync(IMessageBus messageBus, CancellationTokenSource cancellationTokenSource)
            {
                var collections = OrderTestCollections().Select(
                    pair =>
                    new
                    {
                        Collection = pair.Item1,
                        TestCases = pair.Item2,
                        DisableParallelization = IsParallelizationDisabled(pair.Item1)
                    })
                    .ToList();

                var summary = new RunSummary();

                using var runner = new ConcurrentRunner();

                var tasks = new List<Task<RunSummary>>();

                foreach (var test in collections.Where(t => !t.DisableParallelization))
                {
                    tasks.Add(runner.RunAsync(async () => await RunTestCollectionAsync(messageBus, test.Collection, test.TestCases, cancellationTokenSource)));
                }

                await Task.WhenAll(tasks);

                foreach (var task in tasks)
                {
                    summary.Aggregate(task.Result);
                }

                // Single threaded collections
                foreach (var test in collections.Where(t => t.DisableParallelization))
                {
                    summary.Aggregate(await RunTestCollectionAsync(messageBus, test.Collection, test.TestCases, cancellationTokenSource));
                }

                return summary;
            }

            protected override Task<RunSummary> RunTestCollectionAsync(IMessageBus messageBus, ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, CancellationTokenSource cancellationTokenSource)
            {
                return new CustomTestCollectionRunner(testCollection, testCases, DiagnosticMessageSink, messageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), cancellationTokenSource).RunAsync();
            }

            private static bool IsParallelizationDisabled(ITestCollection collection)
            {
                var attr = collection.CollectionDefinition?.GetCustomAttributes(typeof(CollectionDefinitionAttribute)).SingleOrDefault();
                var isIntegrationTest = collection.DisplayName.Contains("Datadog.Trace.ClrProfiler.IntegrationTests");

                if (isIntegrationTest)
                {
                    return true;
                }

                return attr?.GetNamedArgument<bool>(nameof(CollectionDefinitionAttribute.DisableParallelization)) is true;
            }
        }

        private class CustomTestCollectionRunner : XunitTestCollectionRunner
        {
            private readonly IMessageSink _diagnosticMessageSink;

            public CustomTestCollectionRunner(ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ITestCaseOrderer testCaseOrderer, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
                : base(testCollection, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource)
            {
                _diagnosticMessageSink = diagnosticMessageSink;
            }

            protected override Task<RunSummary> RunTestClassAsync(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases)
            {
                return new CustomTestClassRunner(testClass, @class, testCases, _diagnosticMessageSink, MessageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), CancellationTokenSource, CollectionFixtureMappings)
                   .RunAsync();
            }
        }

        private class CustomTestClassRunner : XunitTestClassRunner
        {
            public CustomTestClassRunner(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ITestCaseOrderer testCaseOrderer, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, IDictionary<Type, object> collectionFixtureMappings)
                : base(testClass, @class, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource, collectionFixtureMappings)
            {
            }

            protected override Task<RunSummary> RunTestMethodAsync(ITestMethod testMethod, IReflectionMethodInfo method, IEnumerable<IXunitTestCase> testCases, object[] constructorArguments)
            {
                return new CustomTestMethodRunner(testMethod, this.Class, method, testCases, this.DiagnosticMessageSink, this.MessageBus, new ExceptionAggregator(this.Aggregator), this.CancellationTokenSource, constructorArguments)
                   .RunAsync();
            }
        }

        private class CustomTestMethodRunner : XunitTestMethodRunner
        {
            private readonly IMessageSink _diagnosticMessageSink;

            public CustomTestMethodRunner(ITestMethod testMethod, IReflectionTypeInfo @class, IReflectionMethodInfo method, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, object[] constructorArguments)
                : base(testMethod, @class, method, testCases, diagnosticMessageSink, messageBus, aggregator, cancellationTokenSource, constructorArguments)
            {
                _diagnosticMessageSink = diagnosticMessageSink;
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
                    var result = await base.RunTestCaseAsync(testCase);

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

        private class ConcurrentRunner : IDisposable
        {
            private readonly BlockingCollection<Func<Task>> _queue;

            public ConcurrentRunner()
            {
                _queue = new();

                for (int i = 0; i < Environment.ProcessorCount; i++)
                {
                    var thread = new Thread(DoWork) { IsBackground = true };
                    thread.Start();
                }
            }

            public void Dispose()
            {
                _queue.CompleteAdding();
            }

            public Task<T> RunAsync<T>(Func<Task<T>> action)
            {
                var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

                _queue.Add(async () =>
                {
                    try
                    {
                        tcs.TrySetResult(await action());
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });

                return tcs.Task;
            }

            private void DoWork()
            {
                foreach (var item in _queue.GetConsumingEnumerable())
                {
                    item().GetAwaiter().GetResult();
                }
            }
        }
    }
}
