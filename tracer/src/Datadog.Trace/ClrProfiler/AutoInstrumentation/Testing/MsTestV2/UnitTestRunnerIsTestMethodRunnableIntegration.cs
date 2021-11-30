// <copyright file="UnitTestRunnerIsTestMethodRunnableIntegration.cs" company="Datadog">
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
        MethodName = "IsTestMethodRunnable",
        ReturnTypeName = "_",
        ParameterTypeNames = new string[] { "_", "_", "_" },
        MinimumVersion = "14.0.0",
        MaximumVersion = "14.*.*",
        IntegrationName = MsTestIntegration.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class UnitTestRunnerIsTestMethodRunnableIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TArg1">Type of the arg1</typeparam>
        /// <typeparam name="TArg2">Type of the arg2</typeparam>
        /// <typeparam name="TArg3">Type of the arg3</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="testMethod">Test method argument</param>
        /// <param name="testMethodInfo">Test method info argument</param>
        /// <param name="notRunnableResult">Not runnable result argument</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, TArg3>(TTarget instance, TArg1 testMethod, TArg2 testMethodInfo, ref TArg3 notRunnableResult)
        {
            MsTestIntegration.Log.Warning(" ############ (instance) " + instance?.ToString() ?? "(null)");
            MsTestIntegration.Log.Warning(" ############ (testMethod) " + testMethod?.ToString() ?? "(null)");
            MsTestIntegration.Log.Warning(" ############ (testMethodInfo) " + testMethodInfo?.ToString() ?? "(null)");
            MsTestIntegration.Log.Warning(" ############ (notRunnableResult) " + notRunnableResult?.ToString() ?? "(null)");
            return new CallTargetState(null, testMethod);
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
            if (!MsTestIntegration.IsEnabled)
            {
                return new CallTargetReturn<TReturn>(returnValue);
            }

            MsTestIntegration.Log.Warning(" ############ (returnValue) " + returnValue?.ToString() ?? "(null)");

            /*
            if (returnValue is Array returnValueArray && returnValueArray.Length == 1)
            {
                object unitTestResultObject = returnValueArray.GetValue(0);
                if (unitTestResultObject != null && unitTestResultObject.TryDuckCast<UnitTestResultStruct>(out var unitTestResult))
                {
                    var outcome = unitTestResult.Outcome;
                    if (outcome == UnitTestResultOutcome.Inconclusive || outcome == UnitTestResultOutcome.NotRunnable || outcome == UnitTestResultOutcome.Ignored)
                    {
                        MsTestIntegration.Log.Warning(" ############ " + instance?.ToString());
                        MsTestIntegration.Log.Warning(" ############ " + state.State?.ToString());

                        // This instrumentation catches all tests being ignored
                        var scope = MsTestIntegration.OnMethodBegin(instance.TestMethodInfo, instance.GetType());
                        scope.Span.SetTag(TestTags.Status, TestTags.StatusSkip);
                        scope.Span.SetTag(TestTags.SkipReason, unitTestResult.ErrorMessage);
                        scope.Dispose();
                    }
                }
            }*/

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
