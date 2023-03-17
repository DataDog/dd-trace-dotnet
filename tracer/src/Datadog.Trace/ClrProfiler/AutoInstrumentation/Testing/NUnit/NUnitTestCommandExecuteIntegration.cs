// <copyright file="NUnitTestCommandExecuteIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

/// <summary>
/// NUnit.Framework.Internal.Commands.TestCommand.Execute() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "nunit.framework",
    TypeName = "NUnit.Framework.Internal.Commands.TestCommand",
    MethodName = "Execute",
    ReturnTypeName = "NUnit.Framework.Internal.TestResult",
    ParameterTypeNames = new[] { "NUnit.Framework.Internal.TestExecutionContext" },
    MinimumVersion = "3.0.0",
    MaximumVersion = "3.*.*",
    CallTargetIntegrationType = IntegrationType.Derived,
    IntegrationName = NUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NUnitTestCommandExecuteIntegration
{
    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TContext">ExecutionContext type</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="executionContext">Execution context instance</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext executionContext)
        where TContext : ITestExecutionContext
    {
        if (!NUnitIntegration.IsEnabled)
        {
            return CallTargetState.GetDefault();
        }

        var testResult = executionContext.CurrentResult;
        if (testResult.ResultState.Status == TestStatus.Failed && executionContext.CurrentTest.TestType == NUnitIntegration.TestSuiteConst)
        {
            CIVisibility.Log.Warning("{FullName} | {TestType} | {TestName} | {Message} | {Status} | {Site}", new object[] { typeof(TTarget).FullName, executionContext.CurrentTest.TestType, executionContext.CurrentTest.FullName, executionContext.CurrentResult.Message, executionContext.CurrentResult.ResultState.Status, executionContext.CurrentResult.ResultState.Site });

            if (NUnitIntegration.GetTestSuiteFrom(executionContext.CurrentTest) is { } suite)
            {
                if (testResult.ResultState.Site == FailureSite.SetUp)
                {
                    suite.SetErrorInfo("SetUpException", testResult.Message, testResult.StackTrace);
                }
                else if (testResult.ResultState.Site == FailureSite.TearDown)
                {
                    suite.SetErrorInfo("TearDownException", testResult.Message, testResult.StackTrace);
                }
                else if (testResult.ResultState.Site == FailureSite.Child)
                {
                    suite.SetErrorInfo("Exception", testResult.Message, testResult.StackTrace);
                }

                suite.Tags.Status = TestTags.StatusFail;
            }
        }

        switch (typeof(TTarget).FullName)
        {
            case "NUnit.Framework.Internal.Commands.EmptyTestCommand":
                return CallTargetState.GetDefault();
            case "NUnit.Framework.Internal.Commands.SkipCommand":
                if (NUnitIntegration.CreateTest(executionContext.CurrentTest) is { } test)
                {
                    test.Close(Ci.TestStatus.Skip, TimeSpan.Zero);
                }

                return CallTargetState.GetDefault();
            case "NUnit.Framework.Internal.Commands.TestMethodCommand":
                return new CallTargetState(null, NUnitIntegration.CreateTest(executionContext.CurrentTest));
            default:
                if (executionContext.CurrentTest.Method is not null && !string.IsNullOrEmpty(executionContext.CurrentTest.MethodName))
                {
                    return new CallTargetState(null, NUnitIntegration.CreateTest(executionContext.CurrentTest));
                }

                break;
        }

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
        if (state.State is Test test)
        {
            NUnitIntegration.FinishTest(test, exception);
        }

        return new CallTargetReturn<TResult>(returnValue);
    }
}
