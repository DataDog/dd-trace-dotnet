// <copyright file="CustomTestFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Tagging;
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

        protected virtual Task RunTestCollectionsCallback(IMessageSink diagnosticsMessageSink, IEnumerable<IXunitTestCase> testCases)
        {
            return Task.CompletedTask;
        }

        protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
        {
            return new CustomExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink, RunTestCollectionsCallback);
        }

        private class CustomExecutor : XunitTestFrameworkExecutor
        {
            private readonly Func<IMessageSink, IEnumerable<IXunitTestCase>, Task> _runTestCollectionsCallback;

            public CustomExecutor(AssemblyName assemblyName, ISourceInformationProvider sourceInformationProvider, IMessageSink diagnosticMessageSink, Func<IMessageSink, IEnumerable<IXunitTestCase>, Task> runTestCollectionsCallback)
                : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
            {
                _runTestCollectionsCallback = runTestCollectionsCallback;
            }

            protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
            {
                using var assemblyRunner = new CustomAssemblyRunner(TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions, _runTestCollectionsCallback);
                await assemblyRunner.RunAsync();
            }
        }

        private class CustomAssemblyRunner : XunitTestAssemblyRunner
        {
            private readonly Func<IMessageSink, IEnumerable<IXunitTestCase>, Task> _runTestCollectionsCallback;

            public CustomAssemblyRunner(ITestAssembly testAssembly, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions, Func<IMessageSink, IEnumerable<IXunitTestCase>, Task> runTestCollectionsCallback)
                : base(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
            {
                _runTestCollectionsCallback = runTestCollectionsCallback;
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

                if (Environment.GetEnvironmentVariable("RANDOM_SEED") is not { } environmentSeed
                 || !int.TryParse(environmentSeed, out var seed))
                {
                    seed = new Random().Next();
                }

                DiagnosticMessageSink.OnMessage(new DiagnosticMessage($"Using seed {seed} to randomize tests order"));

                var random = new Random(seed);
                Shuffle(collections, random);

                foreach (var collection in collections)
                {
                    Shuffle(collection.TestCases, random);
                }

                _runTestCollectionsCallback?.Invoke(DiagnosticMessageSink, collections.SelectMany(c => c.TestCases));

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

            private static void Shuffle<T>(IList<T> list, Random rng)
            {
                int n = list.Count;

                while (n > 1)
                {
                    n--;
                    int k = rng.Next(n + 1);
                    var value = list[k];
                    list[k] = list[n];
                    list[n] = value;
                }
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
            private readonly object[] _constructorArguments;

            public CustomTestMethodRunner(ITestMethod testMethod, IReflectionTypeInfo @class, IReflectionMethodInfo method, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, object[] constructorArguments)
                : base(testMethod, @class, method, testCases, diagnosticMessageSink, messageBus, aggregator, cancellationTokenSource, constructorArguments)
            {
                _diagnosticMessageSink = diagnosticMessageSink;
                _constructorArguments = constructorArguments;
            }

            protected override async Task<RunSummary> RunTestCaseAsync(IXunitTestCase testCase)
            {
                var parameters = string.Empty;

                if (testCase.TestMethodArguments != null)
                {
                    // We replace ##vso to avoid sending "commands" via the log output when we're testing CI Visibility stuff
                    // We should redact other CI's command prefixes as well in the future, but for now this is enough
                    parameters = string.Join(", ", testCase.TestMethodArguments.Select(a => a?.ToString()?.Replace("##vso", "#####") ?? "null"));
                }

                var test = $"{TestMethod.TestClass.Class.Name}.{TestMethod.Method.Name}({parameters})";

                var attemptsRemaining = 1;
                var retryReason = string.Empty;
                try
                {
                    var flakyAttribute = Method.MethodInfo.GetCustomAttribute<FlakyAttribute>();
                    if (flakyAttribute != null)
                    {
                        attemptsRemaining = flakyAttribute.MaxRetries + 1;
                        retryReason = flakyAttribute.Reason;
                    }
                }
                catch (Exception e)
                {
                    _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"ERROR: Looking for FlakyAttribute {e}"));
                }

                DelayedMessageBus messageBus = null;
                try
                {
                    while (true)
                    {
                        attemptsRemaining--;
                        messageBus = new DelayedMessageBus(MessageBus);

                        // If this throws, we just let it bubble up, regardless of whether there's a retry, as this indicates an xunit infra issue
                        var summary = await RunTest(messageBus);
                        if (summary.Failed == 0 || attemptsRemaining <= 0)
                        {
                            // No failures, or not allowed to retry
                            return summary;
                        }

                        _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"RETRYING: {test} ({attemptsRemaining} attempts remaining, {retryReason})"));
                        var testFullName = $"{TestMethod.TestClass.Class.Name}.{testCase.DisplayName}";
                        await SendMetric(_diagnosticMessageSink, "dd_trace_dotnet.ci.tests.retries", testFullName, retryReason);
                    }
                }
                finally
                {
                    // need to dispose of the message bus to flush any messages
                    messageBus?.Dispose();
                }

                async Task<RunSummary> RunTest(DelayedMessageBus messageBus)
                {
                    using var timer = new Timer(
                        _ => _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"WARNING: {test} has been running for more than 15 minutes")),
                        null,
                        TimeSpan.FromMinutes(15),
                        Timeout.InfiniteTimeSpan);

                    _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"STARTED: {test}"));

                    try
                    {
                        var result = await testCase.RunAsync(_diagnosticMessageSink, messageBus, _constructorArguments, new ExceptionAggregator(Aggregator), CancellationTokenSource);

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

                async Task SendMetric(IMessageSink outputHelper, string metricName, string testFullName, string reason)
                {
                    var envKey = Environment.GetEnvironmentVariable("DD_LOGGER_DD_API_KEY");
                    if (string.IsNullOrEmpty(envKey))
                    {
                        // We're probably not in CI
                        outputHelper.OnMessage(new DiagnosticMessage($"No CI API Key found, skipping {metricName} metric submission"));
                        return;
                    }

                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.Add("DD-API-KEY", envKey);

                    var tags = $$"""
                                     "os.platform:{{SanitizeTagValue(FrameworkDescription.Instance.OSPlatform)}}",
                                     "os.architecture:{{SanitizeTagValue(EnvironmentTools.GetPlatform())}}",
                                     "target.framework:{{SanitizeTagValue(FrameworkDescription.Instance.ProductVersion)}}",
                                     "test.name:{{SanitizeTagValue(testFullName)}}",
                                     "git.branch:{{SanitizeTagValue(Environment.GetEnvironmentVariable("DD_LOGGER_BUILD_SOURCEBRANCH"))}}",
                                     "flaky_retry_reason: {{SanitizeTagValue(reason)}}"
                                 """;

                    var payload = $$"""
                                        {
                                            "series": [{
                                                "metric": "{{metricName}}",
                                                "type": 1,
                                                "points": [{
                                                    "timestamp": {{((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds()}},
                                                    "value": 1
                                                    }],
                                                "tags": [
                                                    {{tags}}
                                                ]
                                            }]
                                        }
                                    """;

                    var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync("https://api.datadoghq.com/api/v2/series", content);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    var result = response.IsSuccessStatusCode
                                     ? "Successfully submitted metric"
                                     : "Failed to submit metric";
                    outputHelper.OnMessage(new DiagnosticMessage($"{result} {metricName}. Response was: Code: {response.StatusCode}. Response: {responseContent}. Payload sent was: \"{payload}\""));

                    string SanitizeTagValue(string tag)
                    {
                        SpanTagHelper.TryNormalizeTagName(tag, normalizeSpaces: true, out var normalizedTag);
                        return normalizedTag;
                    }
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
