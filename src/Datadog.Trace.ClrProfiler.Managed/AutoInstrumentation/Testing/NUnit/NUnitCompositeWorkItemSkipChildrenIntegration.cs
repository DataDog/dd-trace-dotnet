// <copyright file="NUnitCompositeWorkItemSkipChildrenIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// NUnit.Framework.Internal.Execution.CompositeWorkItem.SkipChildren() calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "nunit.framework",
        TypeName = "NUnit.Framework.Internal.Execution.CompositeWorkItem",
        MethodName = "SkipChildren",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "_", "NUnit.Framework.Interfaces.ResultState", ClrNames.String },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = NUnitIntegration.IntegrationName)]
    public class NUnitCompositeWorkItemSkipChildrenIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TSuite">Test suite type</typeparam>
        /// <typeparam name="TResultState">Result state type</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="testSuite">Test suite instance</param>
        /// <param name="resultState">Result state instance</param>
        /// <param name="message">Message instance</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TSuite, TResultState>(TTarget instance, TSuite testSuite, TResultState resultState, string message)
        {
            if (testSuite is not null)
            {
                string typeName = testSuite.GetType().Name;

                if (typeName == "CompositeWorkItem")
                {
                    // In case we have a CompositeWorkItem we check if there is a OneTimeSetUp failure
                    var compositeWorkItem = testSuite.DuckCast<ICompositeWorkItem>();

                    if (compositeWorkItem.Result?.ResultState?.Status == TestStatus.Failed && compositeWorkItem.Result.ResultState.Site == FailureSite.SetUp)
                    {
                        foreach (var item in compositeWorkItem.Children)
                        {
                            if (item.GetType().Name == "CompositeWorkItem")
                            {
                                var compositeWorkItem2 = item.DuckCast<ICompositeWorkItem>();
                                foreach (var item2 in compositeWorkItem2.Children)
                                {
                                    var testResult = item2.DuckCast<IWorkItem>().Result;
                                    Scope scope = NUnitIntegration.CreateScope(testResult.Test, typeof(TTarget));
                                    scope.Span.Error = true;
                                    scope.Span.SetTag(Tags.ErrorMsg, compositeWorkItem.Result.Message);
                                    scope.Span.SetTag(Tags.ErrorStack, compositeWorkItem.Result.StackTrace);
                                    scope.Span.SetTag(Tags.ErrorType, "SetUpException");
                                    scope.Span.SetTag(TestTags.Status, TestTags.StatusFail);
                                    scope.Span.Finish(new TimeSpan(10));
                                    scope.Dispose();
                                }
                            }
                            else
                            {
                                var testResult = item.DuckCast<IWorkItem>().Result;
                                Scope scope = NUnitIntegration.CreateScope(testResult.Test, typeof(TTarget));
                                scope.Span.Error = true;
                                scope.Span.SetTag(Tags.ErrorMsg, compositeWorkItem.Result.Message);
                                scope.Span.SetTag(Tags.ErrorStack, compositeWorkItem.Result.StackTrace);
                                scope.Span.SetTag(Tags.ErrorType, "SetUpException");
                                scope.Span.SetTag(TestTags.Status, TestTags.StatusFail);
                                scope.Span.Finish(new TimeSpan(10));
                                scope.Dispose();
                            }
                        }

                        return new CallTargetState((Scope)null, (object)null);
                    }
                }
            }

            return new CallTargetState(null, new object[] { testSuite, message });
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>Return value of the method</returns>
        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
        {
            if (state.State != null)
            {
                object[] stateArray = (object[])state.State;
                object testSuiteOrWorkItem = stateArray[0];
                string skipMessage = (string)stateArray[1];

                bool isOneTimeSetup = false;
                const string startString = "OneTimeSetUp:";
                if (skipMessage?.StartsWith(startString, StringComparison.OrdinalIgnoreCase) == true)
                {
                    skipMessage = skipMessage.Substring(startString.Length).Trim();
                    isOneTimeSetup = true;
                }

                if (testSuiteOrWorkItem is not null)
                {
                    string typeName = testSuiteOrWorkItem.GetType().Name;

                    if (typeName == "ParameterizedMethodSuite")
                    {
                        // In case the TestSuite is a ParameterizedMethodSuite instance
                        var testSuite = testSuiteOrWorkItem.DuckCast<ITestSuite>();
                        foreach (var item in testSuite.Tests)
                        {
                            Scope scope = NUnitIntegration.CreateScope(item.DuckCast<ITest>(), typeof(TTarget));
                            NUnitIntegration.FinishSkippedScope(scope, skipMessage);
                        }
                    }
                    else if (typeName == "CompositeWorkItem" && !isOneTimeSetup)
                    {
                        // In case we have a CompositeWorkItem
                        var compositeWorkItem = testSuiteOrWorkItem.DuckCast<ICompositeWorkItem>();

                        foreach (var item in compositeWorkItem.Children)
                        {
                            var testResult = item.DuckCast<IWorkItem>().Result;
                            Scope scope = NUnitIntegration.CreateScope(testResult.Test, typeof(TTarget));
                            NUnitIntegration.FinishSkippedScope(scope, skipMessage);
                        }
                    }
                }
            }

            return default;
        }
    }
}
