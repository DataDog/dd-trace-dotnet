// <copyright file="FlakyTestMethodRunner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers;

/// <summary>
/// Base test method runner that supports retrying flaky tests marked with [Flaky] attribute
/// </summary>
public class FlakyTestMethodRunner : XunitTestMethodRunner
{
    private readonly IMessageSink _diagnosticMessageSink;
    private readonly object[] _constructorArguments;

    public FlakyTestMethodRunner(
        ITestMethod testMethod,
        IReflectionTypeInfo @class,
        IReflectionMethodInfo method,
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink diagnosticMessageSink,
        IMessageBus messageBus,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        object[] constructorArguments)
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

                // Allow derived classes to perform additional actions on retry (e.g., send metrics)
                var testFullName = $"{TestMethod.TestClass.Class.Name}.{testCase.DisplayName}";
                await OnTestRetryAsync(_diagnosticMessageSink, testFullName, retryReason);
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
    }

    /// <summary>
    /// Called when a test is being retried. Override to perform additional actions such as sending metrics.
    /// </summary>
    /// <param name="diagnosticMessageSink">Message sink for diagnostics</param>
    /// <param name="testFullName">Full name of the test being retried</param>
    /// <param name="retryReason">Reason for the retry from the FlakyAttribute</param>
    /// <returns>Task representing the async operation</returns>
    protected virtual Task OnTestRetryAsync(IMessageSink diagnosticMessageSink, string testFullName, string retryReason)
    {
        return Task.CompletedTask;
    }
}
