// <copyright file="UnitTestRunnerRunSingleTestIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2
{
    /// <summary>
    /// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.UnitTestRunner.RunSingleTest calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter",
        TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.UnitTestRunner",
        MethodName = "RunSingleTest",
        ReturnTypeName = "_",
        ParameterTypeNames = new string[] { "_", "_" },
        MinimumVersion = "14.0.0",
        MaximumVersion = "14.*.*",
        IntegrationName = MsTestIntegration.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class UnitTestRunnerRunSingleTestIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TArg1">Type of the arg1</typeparam>
        /// <typeparam name="TArg2">Type of the arg2</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="testMethod">Test method argument</param>
        /// <param name="testContextProperties">Test method info argument</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2>(TTarget instance, TArg1 testMethod, TArg2 testContextProperties)
        {
            return new CallTargetState(null, testMethod);
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
        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            if (!MsTestIntegration.IsEnabled)
            {
                return new CallTargetReturn<TReturn>(returnValue);
            }

            var objTestMethodInfo = MsTestIntegration.IsTestMethodRunnableThreadLocal.Value;
            if (objTestMethodInfo is not null && returnValue is Array returnValueArray && returnValueArray.Length == 1)
            {
                object unitTestResultObject = returnValueArray.GetValue(0);

                if (unitTestResultObject != null &&
                    unitTestResultObject.TryDuckCast<UnitTestResultStruct>(out var unitTestResult) &&
                    objTestMethodInfo.TryDuckCast<ITestMethod>(out var testMethodInfo))
                {
                    var outcome = unitTestResult.Outcome;
                    if (outcome == UnitTestResultOutcome.Inconclusive || outcome == UnitTestResultOutcome.NotRunnable || outcome == UnitTestResultOutcome.Ignored)
                    {
                        // This instrumentation catches all tests being ignored
                        var scope = MsTestIntegration.OnMethodBegin(testMethodInfo, instance.GetType());
                        scope.Span.SetTag(TestTags.Status, TestTags.StatusSkip);
                        scope.Span.SetTag(TestTags.SkipReason, unitTestResult.ErrorMessage);
                        scope.Dispose();
                    }
                }

                MsTestIntegration.IsTestMethodRunnableThreadLocal.Value = null;
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
