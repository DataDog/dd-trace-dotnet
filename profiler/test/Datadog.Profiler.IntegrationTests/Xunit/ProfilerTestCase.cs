// <copyright file="ProfilerTestCase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Xunit
{
    internal class ProfilerTestCase : XunitTestCase
    {
        [Obsolete]
        public ProfilerTestCase()
            : base()
        {
        }

        public ProfilerTestCase(
            IMessageSink diagnosticMessageSink,
            TestMethodDisplay defaultMethodDisplay,
            TestMethodDisplayOptions defaultMethodDisplayOptions,
            ITestMethod testMethod,
            object[] testMethodArguments = null)
            : base(diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod, testMethodArguments)
        {
        }

        public override async Task<RunSummary> RunAsync(
            IMessageSink diagnosticMessageSink,
            IMessageBus messageBus,
            object[] constructorArguments,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource)
        {
            byte attemptsRemaining = 1;
            var retryReason = string.Empty;

            try
            {
                var flakyAttribute = TestMethod.Method.ToRuntimeMethod().GetCustomAttribute<FlakyAttribute>();
                if (flakyAttribute is not null)
                {
                    // First attempt + retries
                    attemptsRemaining = flakyAttribute.MaxRetries + 1;
                    retryReason = flakyAttribute.Reason;
                }
            }
            catch (Exception e)
            {
                diagnosticMessageSink.OnMessage(new DiagnosticMessage($"ERROR: Looking for FlakyAttribute: {e}"));
            }

            if (attemptsRemaining <= 1)
            {
                return await RunOnceAsync(messageBus);
            }

            DelayedMessageBus delayedBus = null;
            try
            {
                while (true)
                {
                    attemptsRemaining--;
                    delayedBus = new DelayedMessageBus(messageBus);

                    var summary = await RunOnceAsync(delayedBus);

                    if (summary.Failed == 0 || attemptsRemaining <= 0)
                    {
                        return summary;
                    }

                    diagnosticMessageSink.OnMessage(
                        new DiagnosticMessage($"RETRYING: {DisplayName} ({attemptsRemaining} attempts remaining, {retryReason})"));
                }
            }
            finally
            {
                delayedBus?.Dispose();
            }

            Task<RunSummary> RunOnceAsync(IMessageBus bus)
                => new ProfilerTestCaseRunner(this, DisplayName, SkipReason, constructorArguments, TestMethodArguments, bus, aggregator, cancellationTokenSource).RunAsync();
        }
    }
}
