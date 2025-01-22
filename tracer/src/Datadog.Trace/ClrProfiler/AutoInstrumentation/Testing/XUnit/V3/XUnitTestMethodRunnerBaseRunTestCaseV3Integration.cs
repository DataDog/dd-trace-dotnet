// <copyright file="XUnitTestMethodRunnerBaseRunTestCaseV3Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
#if NETFRAMEWORK
using Datadog.Trace.VendoredMicrosoftCode.System.Runtime.CompilerServices.Unsafe;
#endif

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Xunit.v3.TestCaseRunner`3.RunTest calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "xunit.v3.core",
    TypeName = "Xunit.v3.XunitTestMethodRunnerBase`3",
    MethodName = "RunTestCase",
    ParameterTypeNames = ["_", "_"],
    ReturnTypeName = "System.Threading.Tasks.ValueTask`1[Xunit.v3.RunSummary]",
    MinimumVersion = "1.0.0",
    MaximumVersion = "1.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestMethodRunnerBaseRunTestCaseV3Integration
{
    private static int _totalRetries = -1;
    private static int? _runSummaryFieldCount;

    internal static CallTargetState OnMethodBegin<TTarget, TContext, TTestCase>(TTarget instance, TContext context, TTestCase testcaseOriginal)
        where TContext : IXunitTestMethodRunnerBaseContextV3
    {
        Common.Log.Warning("XUnitTestMethodRunnerBaseRunTestCaseV3Integration.OnMethodBegin, instance: {0}, context: {1}, testcase: {2}", instance, context, testcaseOriginal);
        if (!XUnitIntegration.IsEnabled || instance is null)
        {
            return CallTargetState.GetDefault();
        }

        Interlocked.CompareExchange(ref _totalRetries, CIVisibility.Settings.TotalFlakyRetryCount, -1);

        var testcase = testcaseOriginal.DuckCast<IXunitTestCaseV3>()!;
        var testRunnerData = new TestRunnerStruct
        {
            TestClass = testcase.TestMethod.TestClass.Class,
            TestMethod = testcase.TestMethod.Method,
            TestMethodArguments = testcase.TestMethod.TestMethodArguments!,
            TestCase = new TestCaseStruct
            {
                DisplayName = testcase.TestCaseDisplayName,
                Traits = testcase.Traits.ToDictionary(k => k.Key, v => v.Value.ToList()),
            },
            Aggregator = context.Aggregator,
            SkipReason = testcase.SkipReason,
        };

        // Check if the test should be skipped by the ITR
        if (XUnitIntegration.ShouldSkip(ref testRunnerData, out _, out _))
        {
            Common.Log.Debug("ITR: Test skipped: {Class}.{Name}", testcase.TestClass?.ToString() ?? string.Empty, testcase.TestMethod?.Method.Name ?? string.Empty);
            // Refresh values after skip reason change, and create Skip by ITR span.
            testcase.SkipReason = IntelligentTestRunnerTags.SkippedByReason;
            XUnitIntegration.CreateTest(ref testRunnerData);
            return CallTargetState.GetDefault();
        }

        if (testRunnerData.SkipReason is not null)
        {
            // Skip test support
            Common.Log.Debug("Skipping test: {Class}.{Name} Reason: {Reason}", testcase.TestClass?.ToString() ?? string.Empty, testcase.TestMethod?.Method.Name ?? string.Empty, testRunnerData.SkipReason);
            XUnitIntegration.CreateTest(ref testRunnerData);
            return CallTargetState.GetDefault();
        }

        if (CIVisibility.Settings.EarlyFlakeDetectionEnabled != true &&
            CIVisibility.Settings.FlakyRetryEnabled != true)
        {
            return CallTargetState.GetDefault();
        }

        return new CallTargetState(null, new[] { context.MessageBus, context, testcase });
    }

    internal static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        Common.Log.Warning("XUnitTestMethodRunnerBaseRunTestCaseV3Integration.OnAsyncMethodEnd, instance: {0}, returnValue: {1}", instance, returnValue);

        if (state.State is not object[] { Length: 3 } stateArray)
        {
            // State is not the expected array
            return returnValue;
        }

        var retryMessageBus = (stateArray[0] as IDuckType)?.Instance as RetryMessageBus;
        if (retryMessageBus is { TestIsNew: true, AbortByThreshold: false } or { FlakyRetryEnabled: true })
        {
            _runSummaryFieldCount ??= typeof(TReturn).GetFields().Length;
            if (_runSummaryFieldCount != 5)
            {
                Common.Log.Warning("RunSummary type doesn't have the field count we are expecting. Flushing messages from RetryMessageBus");
                retryMessageBus.FlushMessages();
                return returnValue;
            }

            var runSummaryUnsafe = Unsafe.As<TReturn, RunSummaryUnsafeStruct>(ref returnValue);

            Common.Log.Warning<int>("ExecutionIndex: {ExecutionIndex}", retryMessageBus.ExecutionIndex);

            var context = (IXunitTestMethodRunnerBaseContextV3)stateArray[1];
            var testcase = (IXunitTestCaseV3)stateArray[2];
            var isFlakyRetryEnabled = retryMessageBus.FlakyRetryEnabled;
            var index = retryMessageBus.ExecutionIndex;

            if (index == 0)
            {
                // Let's make decisions based on the first execution regarding slow tests or retry failed test feature
                if (isFlakyRetryEnabled)
                {
                    retryMessageBus.TotalExecutions = CIVisibility.Settings.FlakyRetryCount + 1;
                }
                else
                {
                    var duration = TimeSpan.FromSeconds((double)runSummaryUnsafe.Time);
                    retryMessageBus.TotalExecutions = Common.GetNumberOfExecutionsForDuration(duration);
                }

                retryMessageBus.ExecutionNumber = retryMessageBus.TotalExecutions - 1;
            }

            if (retryMessageBus.ExecutionNumber > 0)
            {
                var doRetry = true;
                if (isFlakyRetryEnabled)
                {
                    var remainingTotalRetries = Interlocked.Decrement(ref _totalRetries);
                    if (runSummaryUnsafe.Failed == 0)
                    {
                        Common.Log.Debug("EFD/Retry: [FlakyRetryEnabled] A non failed test execution was detected, skipping the remaining executions.");
                        doRetry = false;
                    }
                    else if (runSummaryUnsafe.NotRun == 0)
                    {
                        Common.Log.Debug("EFD/Retry: [FlakyRetryEnabled] A NotRun test was detected, skipping the remaining executions.");
                        doRetry = false;
                    }
                    else if (remainingTotalRetries < 1)
                    {
                        Common.Log.Debug<int>("EFD/Retry: [FlakyRetryEnabled] Exceeded number of total retries. [{Number}]", CIVisibility.Settings.TotalFlakyRetryCount);
                        doRetry = false;
                    }
                }

                if (doRetry)
                {
                    var retryNumber = retryMessageBus.ExecutionIndex + 1;
                    // Set the retry as a continuation of this execution. This will be executing recursively until the execution count is 0/
                    Common.Log.Debug<int, int>("EFD/Retry: [Retry {Num}] Test class runner is duck casted, running a retry. [Current retry value is {Value}]", retryNumber, retryMessageBus.ExecutionNumber);

                    var mrunner = instance.DuckCast<IXunitTestMethodRunnerV3>()!;

                    // Decrement the execution number (the method body will do the execution)
                    retryMessageBus.ExecutionNumber--;
                    var innerReturnValue = (TReturn)await mrunner.RunTestCase(context.Instance!, testcase.Instance!);
                    Common.Log.Debug<int, int, string>("EFD/Retry: [Retry {Num}] Retry finished. [Current retry value is {Value}]. DisplayName: {DisplayName}", retryNumber, retryMessageBus.ExecutionNumber, testcase.TestCaseDisplayName);

                    var innerReturnValueUnsafe = Unsafe.As<TReturn, RunSummaryUnsafeStruct>(ref innerReturnValue);
                    Common.Log.Debug<int>("EFD/Retry: [Retry {Num}] Aggregating results.", retryNumber);
                    runSummaryUnsafe.Total += innerReturnValueUnsafe.Total;
                    runSummaryUnsafe.Failed += innerReturnValueUnsafe.Failed;
                    runSummaryUnsafe.Skipped += innerReturnValueUnsafe.Skipped;
                    runSummaryUnsafe.NotRun += innerReturnValueUnsafe.NotRun;
                    runSummaryUnsafe.Time += innerReturnValueUnsafe.Time;
                }
            }
            else
            {
                if (isFlakyRetryEnabled && runSummaryUnsafe.Failed == 0)
                {
                    Common.Log.Debug("EFD/Retry: [FlakyRetryEnabled] A non failed test execution was detected.");
                }
                else
                {
                    Common.Log.Debug("EFD/Retry: All retries were executed.");
                }
            }

            if (index == 0)
            {
                retryMessageBus.FlushMessages();

                // Let's clear the failed and skipped runs if we have at least one successful run
#pragma warning disable DDLOG004
                Common.Log.Debug($"EFD/Retry: Summary: {testcase.TestCaseDisplayName} [Total: {runSummaryUnsafe.Total}, Failed: {runSummaryUnsafe.Failed}, Skipped: {runSummaryUnsafe.Skipped}]");
#pragma warning restore DDLOG004
                var passed = runSummaryUnsafe.Total - runSummaryUnsafe.Skipped - runSummaryUnsafe.Failed;
                if (passed > 0)
                {
                    runSummaryUnsafe.Total = 1;
                    runSummaryUnsafe.Failed = 0;
                    runSummaryUnsafe.Skipped = 0;
                }
                else if (runSummaryUnsafe.Skipped > 0)
                {
                    runSummaryUnsafe.Total = 1;
                    runSummaryUnsafe.Skipped = 1;
                    runSummaryUnsafe.Failed = 0;
                }
                else if (runSummaryUnsafe.Failed > 0)
                {
                    runSummaryUnsafe.Total = 1;
                    runSummaryUnsafe.Skipped = 0;
                    runSummaryUnsafe.Failed = 1;
                }

#pragma warning disable DDLOG004
                Common.Log.Debug($"EFD/Retry: Returned summary: {testcase.TestCaseDisplayName} [Total: {runSummaryUnsafe.Total}, Failed: {runSummaryUnsafe.Failed}, Skipped: {runSummaryUnsafe.Skipped}]");
#pragma warning restore DDLOG004
            }
        }
        else
        {
            retryMessageBus?.FlushMessages();
        }

        return returnValue;
    }
}
