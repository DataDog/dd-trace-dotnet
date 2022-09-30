// <copyright file="TestMethodAttributeExecuteIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.TestFramework.Execute calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TestPlatform.TestFramework",
    TypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
    MethodName = "Execute",
    ReturnTypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestResult",
    ParameterTypeNames = new[] { "Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod" },
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestMethodAttributeExecuteIntegration
{
    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TTestMethod">Type of the ITestMethod</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="testMethod">Test method instance</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TTestMethod>(TTarget instance, TTestMethod testMethod)
        where TTestMethod : ITestMethod, IDuckType
    {
        if (!MsTestIntegration.IsEnabled)
        {
            return CallTargetState.GetDefault();
        }

        return new CallTargetState(null, MsTestIntegration.OnMethodBegin(testMethod, testMethod.Type));
    }

    /// <summary>
    /// OnMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="returnValue">Return value</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value, in an async scenario will be T of Task of T</returns>
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        if (!MsTestIntegration.IsEnabled)
        {
            return new CallTargetReturn<TReturn>(returnValue);
        }

        if (state.State is Test test)
        {
            var returnValueArray = returnValue as Array;
            if (returnValueArray.Length == 1)
            {
                var testResultObject = returnValueArray.GetValue(0);

                if (testResultObject != null &&
                    testResultObject.TryDuckCast<TestResultStruct>(out var testResult))
                {
                    string errorType = null;
                    string errorMessage = null;
                    string errorStackTrace = null;

                    if (testResult.TestFailureException != null)
                    {
                        var testException = testResult.TestFailureException.InnerException ?? testResult.TestFailureException;
                        var testExceptionType = testException.GetType();
                        var testExceptionName = testExceptionType.Name;
                        if (testExceptionName != "UnitTestAssertException" && testExceptionName != "AssertInconclusiveException")
                        {
                            test.SetErrorInfo(testException);
                        }

                        errorType = testExceptionType.FullName;
                        errorMessage = testException.Message;
                        errorStackTrace = testException.ToString();
                    }

                    switch (testResult.Outcome)
                    {
                        case UnitTestOutcome.Error:
                        case UnitTestOutcome.Failed:
                        case UnitTestOutcome.Timeout:
                            test.SetErrorInfo(errorType, errorMessage, errorStackTrace);
                            test.Close(TestStatus.Fail);
                            break;
                        case UnitTestOutcome.Inconclusive:
                        case UnitTestOutcome.NotRunnable:
                            if (exception is not null)
                            {
                                test.SetErrorInfo(exception);
                                test.Close(TestStatus.Fail);
                            }
                            else
                            {
                                test.Close(TestStatus.Skip, skipReason: errorMessage);
                            }

                            break;
                        case UnitTestOutcome.Passed:
                            if (exception is not null)
                            {
                                test.SetErrorInfo(exception);
                                test.Close(TestStatus.Fail);
                            }
                            else
                            {
                                test.Close(TestStatus.Pass);
                            }

                            break;
                    }
                }
            }
        }

        return new CallTargetReturn<TReturn>(returnValue);
    }
}
