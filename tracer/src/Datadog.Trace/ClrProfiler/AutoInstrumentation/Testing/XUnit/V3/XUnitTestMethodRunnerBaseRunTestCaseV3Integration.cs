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
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

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

        return new CallTargetState(null, new[] { context.MessageBus, context, testcase });
    }

    internal static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult returnValue, Exception exception, in CallTargetState state)
    {
        Common.Log.Warning("XUnitTestMethodRunnerBaseRunTestCaseV3Integration.OnMethodEnd, instance: {0}, context: {1}", instance, returnValue);
        return new CallTargetReturn<TResult>(returnValue);
    }

    internal static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        Common.Log.Warning("XUnitTestMethodRunnerBaseRunTestCaseV3Integration.OnAsyncMethodEnd, instance: {0}, returnValue: {1}", instance, returnValue);

        await Task.Yield();
        var stateArray = (object[])state.State!;
        var context = (IXunitTestMethodRunnerBaseContextV3)stateArray[1];
        var testcase = (IXunitTestCaseV3)stateArray[2];
        if (stateArray[0] is IDuckType { Instance: RetryMessageBus messageBus })
        {
            Common.Log.Warning<int>("ExecutionIndex: {ExecutionIndex}", messageBus.ExecutionIndex);

            var index = messageBus.ExecutionIndex;
            if (index == 0)
            {
                messageBus.TotalExecutions = 5;
                messageBus.ExecutionNumber = messageBus.TotalExecutions - 1;
            }

            if (messageBus.ExecutionNumber > 0)
            {
                var doRetry = true;
                var remainingTotalRetries = Interlocked.Decrement(ref _totalRetries);
                if (remainingTotalRetries < 1)
                {
                    Common.Log.Debug<int, string>("EFD/Retry: [FlakyRetryEnabled] Exceeded number of total retries. [{Number}]. DisplayName: {DisplayName}", CIVisibility.Settings.TotalFlakyRetryCount, testcase.TestCaseDisplayName);
                    doRetry = false;
                }

                if (doRetry)
                {
                    var mrunner = instance.DuckCast<IXunitTestMethodRunnerV3>()!;

                    var retryNumber = messageBus.ExecutionIndex + 1;
                    Common.Log.Debug<int, int, string>("EFD/Retry: [Retry {Num}] Running a retry. [Current retry value is {Value}]. DisplayName: {DisplayName}", retryNumber, messageBus.ExecutionNumber, testcase.TestCaseDisplayName);
                    // Decrement the execution number (the method body will do the execution)
                    messageBus.ExecutionNumber--;
                    var result = await mrunner.RunTestCase(context.Instance!, testcase.Instance!);
                    _ = result;
                    Common.Log.Debug<int, int, string>("EFD/Retry: [Retry {Num}] Retry finished. [Current retry value is {Value}]. DisplayName: {DisplayName}", retryNumber, messageBus.ExecutionNumber, testcase.TestCaseDisplayName);
                }
            }

            if (index == 0)
            {
                Common.Log.Warning("Flushing messages from RetryMessageBus");
                messageBus.FlushMessages();
            }
        }

        return returnValue;
    }
}
