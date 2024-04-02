// <copyright file="TestMethodAttributeExecuteIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
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
    ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod"],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TestPlatform.TestFramework",
    TypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
    MethodName = "Execute",
    ReturnTypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestResult",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod"],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName,
    CallTargetIntegrationKind = CallTargetKind.Derived)]
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
        where TTestMethod : ITestMethod
    {
        if (!MsTestIntegration.IsEnabled || instance is ItrSkipTestMethodExecutor)
        {
            return CallTargetState.GetDefault();
        }

        if (Tracer.Instance.InternalActiveScope is { Span.Type: SpanTypes.Test })
        {
            // Avoid a test inside another test
            return CallTargetState.GetDefault();
        }

        return new CallTargetState(null, new TestMethodState(MsTestIntegration.OnMethodBegin(testMethod, testMethod.Type), testMethod));
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
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        if (!MsTestIntegration.IsEnabled || instance is ItrSkipTestMethodExecutor)
        {
            return new CallTargetReturn<TReturn>(returnValue);
        }

        if (state.State is TestMethodState testMethodState)
        {
            if (returnValue is IList { Count: > 0 } returnValueList)
            {
                for (var i = 0; i < returnValueList.Count; i++)
                {
                    var test = i == 0 ?
                                   testMethodState.Test :
                                   MsTestIntegration.OnMethodBegin(testMethodState.TestMethod, testMethodState.TestMethod.Type, testMethodState.Test.StartTime);
                    var testResultObject = returnValueList[i];
                    if (testResultObject.TryDuckCast<TestResultStruct>(out var testResult))
                    {
                        var testException = testResult.TestFailureException?.InnerException ??
                                            testResult.TestFailureException ??
                                            exception;
                        if (testException != null)
                        {
                            var testExceptionType = testException.GetType();
                            var testExceptionName = testExceptionType.Name;
                            if (testExceptionName != "UnitTestAssertException" && testExceptionName != "AssertInconclusiveException")
                            {
                                test.SetErrorInfo(testException);
                            }
                            else
                            {
                                test.SetErrorInfo(testExceptionType.FullName ?? "Error", testException.Message, testException.ToString());
                            }
                        }

                        if (!string.IsNullOrEmpty(testResult.DisplayName) && test.Name != testResult.DisplayName)
                        {
                            test.SetName(testResult.DisplayName);
                            MsTestIntegration.UpdateTestParameters(test, testMethodState.TestMethod, testResult.DisplayName);
                        }

                        if (exception is not null)
                        {
                            test.Close(TestStatus.Fail);
                        }
                        else
                        {
                            switch (testResult.Outcome)
                            {
                                case UnitTestOutcome.Error or UnitTestOutcome.Failed or UnitTestOutcome.Timeout:
                                    test.Close(TestStatus.Fail);
                                    break;
                                case UnitTestOutcome.Inconclusive or UnitTestOutcome.NotRunnable:
                                    test.Close(TestStatus.Skip, TimeSpan.Zero, testException?.Message ?? string.Empty);
                                    break;
                                case UnitTestOutcome.Passed:
                                    test.Close(TestStatus.Pass);
                                    break;
                                default:
                                    Common.Log.Warning("Failed to handle the test status");
                                    test.Close(TestStatus.Fail);
                                    break;
                            }
                        }
                    }
                    else
                    {
                        Common.Log.Warning("Failed to cast TestResultStruct");
                        test.Close(TestStatus.Fail);
                    }
                }
            }
            else
            {
                Common.Log.Warning("Failed to extract TestResult from return value");
                testMethodState.Test.Close(TestStatus.Fail);
            }
        }

        return new CallTargetReturn<TReturn>(returnValue);
    }

    private class TestMethodState
    {
        public TestMethodState(Test test, ITestMethod testMethod)
        {
            Test = test;
            TestMethod = testMethod;
        }

        public Test Test { get; }

        public ITestMethod TestMethod { get; }
    }
}
