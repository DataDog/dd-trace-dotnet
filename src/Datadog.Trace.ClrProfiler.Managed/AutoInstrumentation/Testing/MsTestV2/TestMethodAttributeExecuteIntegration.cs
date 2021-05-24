// <copyright file="TestMethodAttributeExecuteIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2
{
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
    public class TestMethodAttributeExecuteIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TTestMethod">Type of the ITestMethod</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="testMethod">Test method instance</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TTestMethod>(TTarget instance, TTestMethod testMethod)
            where TTestMethod : ITestMethod, IDuckType
        {
            if (!MsTestIntegration.IsEnabled)
            {
                return CallTargetState.GetDefault();
            }

            var scope = MsTestIntegration.OnMethodBegin(testMethod, testMethod.Type);
            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            if (MsTestIntegration.IsEnabled)
            {
                Scope scope = state.Scope;
                if (scope != null)
                {
                    Array returnValueArray = returnValue as Array;
                    if (returnValueArray.Length == 1)
                    {
                        object testResultObject = returnValueArray.GetValue(0);
                        if (testResultObject != null &&
                            testResultObject.TryDuckCast<TestResultStruct>(out var testResult))
                        {
                            string errorMessage = null;
                            string errorStackTrace = null;

                            if (testResult.TestFailureException != null)
                            {
                                Exception testException = testResult.TestFailureException.InnerException ?? testResult.TestFailureException;
                                string testExceptionName = testException.GetType().Name;
                                if (testExceptionName != "UnitTestAssertException" && testExceptionName != "AssertInconclusiveException")
                                {
                                    scope.Span.SetException(testException);
                                }

                                errorMessage = testException.Message;
                                errorStackTrace = testException.ToString();
                            }

                            switch (testResult.Outcome)
                            {
                                case UnitTestOutcome.Error:
                                case UnitTestOutcome.Failed:
                                case UnitTestOutcome.Timeout:
                                    scope.Span.SetTag(TestTags.Status, TestTags.StatusFail);
                                    scope.Span.Error = true;
                                    scope.Span.SetTag(Tags.ErrorMsg, errorMessage);
                                    scope.Span.SetTag(Tags.ErrorStack, errorStackTrace);
                                    break;
                                case UnitTestOutcome.Inconclusive:
                                case UnitTestOutcome.NotRunnable:
                                    scope.Span.SetTag(TestTags.Status, TestTags.StatusSkip);
                                    scope.Span.SetTag(TestTags.SkipReason, errorMessage);
                                    break;
                                case UnitTestOutcome.Passed:
                                    scope.Span.SetTag(TestTags.Status, TestTags.StatusPass);
                                    break;
                            }
                        }
                    }

                    if (exception != null)
                    {
                        scope.Span.SetException(exception);
                        scope.Span.SetTag(TestTags.Status, TestTags.StatusFail);
                    }

                    scope.Dispose();
                }
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
