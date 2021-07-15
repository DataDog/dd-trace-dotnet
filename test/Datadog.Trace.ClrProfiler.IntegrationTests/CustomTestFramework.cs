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
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestFramework("Datadog.Trace.ClrProfiler.IntegrationTests.CustomTestFramework", "Datadog.Trace.ClrProfiler.IntegrationTests")]

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class CustomTestFramework : XunitTestFramework
    {
        public CustomTestFramework(IMessageSink messageSink)
            : base(messageSink)
        {
            var targetPath = GetProfilerTargetFolder();

            if (targetPath != null)
            {
                var file = typeof(Instrumentation).Assembly.Location;
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
            var targetFrameworkDirectory = GetTargetFrameworkDirectory();

            var paths = EnvironmentHelper.GetProfilerPathCandidates(null).ToArray();

            foreach (var path in paths)
            {
                var baseDirectory = Path.GetDirectoryName(path);
                var finalDirectory = Path.Combine(baseDirectory, targetFrameworkDirectory);

                if (Directory.Exists(finalDirectory))
                {
                    return finalDirectory;
                }
            }

            return null;
        }

        protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
        {
            return new CustomExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
        }

        private static string GetTargetFrameworkDirectory()
        {
            // The conditions looks weird, but it seems like _OR_GREATER is not supported yet in all environments
            // We can trim all the additional conditions when this is fixed
#if NETCOREAPP3_1_OR_GREATER || NETCOREAPP3_1 || NET50
            return "netcoreapp3.1";
#elif NETCOREAPP || NETSTANDARD
            return "netstandard2.0";
#elif NET461_OR_GREATER || NET461 || NET47 || NET471 || NET472 || NET48
            return "net461";
#elif NET45_OR_GREATER || NET45 || NET451 || NET452 || NET46
            return "net45";
#else
#error Unexpected TFM
#endif
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

            protected override Task<RunSummary> RunTestCollectionAsync(IMessageBus messageBus, ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, CancellationTokenSource cancellationTokenSource)
            {
                return new CustomTestCollectionRunner(testCollection, testCases, DiagnosticMessageSink, messageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), cancellationTokenSource).RunAsync();
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
            }
        }
    }
}
