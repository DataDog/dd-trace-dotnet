// <copyright file="XUnitTestClassRunnerRunAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

/// <summary>
/// Xunit.Sdk.TestClassRunner`1.RunAsync calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = new[] { "xunit.execution.dotnet", "xunit.execution.desktop" },
    TypeName = "Xunit.Sdk.TestClassRunner`1",
    MethodName = "RunAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Xunit.Sdk.RunSummary]",
    MinimumVersion = "2.2.0",
    MaximumVersion = "2.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestClassRunnerRunAsyncIntegration
{
    private static readonly int Retries = 3;
    private static readonly ConditionalWeakTable<object, StrongBox<int>> ExecutionCount = new();

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        if (!XUnitIntegration.IsEnabled || instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var totalExecutionNumber = Retries + 1;

        if (TestModule.Current is { } testModule)
        {
            if (totalExecutionNumber == 1)
            {
                var classRunnerInstance = instance.DuckCast<TestClassRunnerStruct>();
                return new CallTargetState(null, testModule.InternalGetOrCreateSuite(classRunnerInstance.TestClass.Class.Name ?? string.Empty));
            }
            else
            {
                // Get the current execution number for this TestClassRunner instance
                Common.Log.Debug("Get the current execution number for this TestClassRunner instance.");
                if (!ExecutionCount.TryGetValue(instance, out var execCount))
                {
                    // Create a new strong box to keep count of the number of execution of the TestClassRunner instance
                    Common.Log.Debug<int>("Create a new strong box to keep count of the number of execution of the TestClassRunner instance. [Total={TotalNumber}]", totalExecutionNumber);
                    execCount = new StrongBox<int>(totalExecutionNumber);
                    ExecutionCount.Add(instance!, execCount);
                }

                // Try to ducktype the current instance to ITestClassRunner
                Common.Log.Debug("Let's check if the current message bus is our own implementation.");
                if (!instance.TryDuckCast<ITestClassRunner>(out var classRunnerInstance))
                {
                    Common.Log.Error("Current class runner instance cannot be ducktyped.");
                    return CallTargetState.GetDefault();
                }

                // Let's check if the current message bus is our own implementation.
                if (classRunnerInstance.MessageBus is { } messageBus and not IDuckType)
                {
                    // Let's replace the IMessageBus with our own implementation to process all results before sending them to the original bus
                    Common.Log.Debug("Current message bus is not a duck type");
                    var duckMessageBus = messageBus.DuckCast<IMessageBus>();
                    Common.Log.Debug("Getting the interface");
                    var messageBusInterfaceType = messageBus.GetType().GetInterface("IMessageBus")!;
                    Common.Log.Debug("Create the new bus");
                    var newMessageBus = new RetryMessageBus(duckMessageBus, totalExecutionNumber, execCount);
                    Common.Log.Debug("Create the new reverse duck type");
                    classRunnerInstance.MessageBus = newMessageBus.DuckImplement(messageBusInterfaceType);
                }

                // Decrement the execution number (the method body will do the execution)
                execCount.Value--;
                return new CallTargetState(null, new TestClassRunnerState(testModule.InternalGetOrCreateSuite(classRunnerInstance.TestClass.Class.Name ?? string.Empty), execCount, classRunnerInstance));
            }
        }

        Common.Log.Warning("Test module cannot be found.");
        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// OnMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TResult">TestResult type</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="returnValue">Original method return value</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>Return value of the method</returns>
    internal static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult returnValue, Exception exception, in CallTargetState state)
    {
        if (state.State == TestSuite.Current)
        {
            // Restore the AsyncLocal set
            // This is used to mimic the ExecutionContext copy from the StateMachine
            // CallTarget integrations does this automatically when using a normal `Scope`
            // in this case we have to do it manually.
            TestSuite.Current = null;
        }

        return new CallTargetReturn<TResult>(returnValue);
    }

    /// <summary>
    /// OnAsyncMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return type</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="returnValue">Return value</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value, in an async scenario will be T of Task of T</returns>
    internal static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        if (state.State is TestClassRunnerState { } testClassRunnerState)
        {
            if (Retries == 0)
            {
                if (testClassRunnerState.TestSuite is { } testSuite)
                {
                    testSuite.Close();
                }
            }
            else
            {
                Common.Log.Debug("Calculating execution index.");
                var execCount = testClassRunnerState.ExecutionCount;
                var index = Retries - execCount.Value;
                if (execCount.Value > 0)
                {
                    var retryNumber = index + 1;
                    Common.Log.Debug<int, int>("[Retry {Num}] We need to retry, the current retry value is {Value}", retryNumber, execCount.Value);
                    if (testClassRunnerState.TestClassRunner is { } testClassRunner)
                    {
                        // Set the retry as a continuation of this execution. This will be executing recursively until the execution count is 0/
                        Common.Log.Debug<int>("[Retry {Num}] Test class runner is duck casted, running a retry.", retryNumber);
                        var innerReturnValue = await ((Task<TReturn>)testClassRunner.RunAsync()).ConfigureAwait(false);
                        Common.Log.Debug<int>("[Retry {Num}] Duck casting the inner run summary.", retryNumber);
                        var innerRunSummary = innerReturnValue.DuckCast<IRunSummary>()!;
                        Common.Log.Debug<int>("[Retry {Num}] Duck casting the run summary.", retryNumber);
                        var runSummary = returnValue.DuckCast<IRunSummary>()!;
                        Common.Log.Debug<int>("[Retry {Num}] Aggregating.", retryNumber);
                        runSummary.Aggregate(innerRunSummary);
                    }
                }
                else
                {
                    Common.Log.Debug("All retries were executed.");
                }

                if (index == 0 && testClassRunnerState.TestSuite is { } testSuite)
                {
                    Common.Log.Debug("Removing skipped and failed if there's a least 1 success.");
                    var runSummary = returnValue.DuckCast<IRunSummary>()!;

                    // Check if we have at least one successful run
                    if (runSummary.Total > runSummary.Skipped + runSummary.Failed)
                    {
                        // We clear the failed and skipped runs if we have at least one successful run
                        runSummary.Skipped = 0;
                        runSummary.Failed = 0;
                    }

                    Common.Log.Information<int, int, int>("Summary: [Total: {Total}, Failed: {Failed}, Skipped: {Skipped}]", runSummary.Total, runSummary.Failed, runSummary.Skipped);
                    testSuite.Close();
                }
            }
        }

        return returnValue;
    }

    private readonly struct TestClassRunnerState
    {
        public readonly TestSuite TestSuite;
        public readonly StrongBox<int> ExecutionCount;
        public readonly ITestClassRunner? TestClassRunner;

        public TestClassRunnerState(TestSuite testSuite, StrongBox<int> executionCount, ITestClassRunner? testClassRunner)
        {
            TestSuite = testSuite;
            ExecutionCount = executionCount;
            TestClassRunner = testClassRunner;
        }
    }
}
