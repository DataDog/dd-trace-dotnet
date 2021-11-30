// <copyright file="UnitTestRunnerIsTestMethodRunnableIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Reflection.Emit;
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
            return new CallTargetState(null, new CustomState(testMethod, testMethodInfo, PtrConverter<TArg3>.GetPointer(ref notRunnableResult)));
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static CallTargetReturn<bool> OnMethodEnd<TTarget>(TTarget instance, bool returnValue, Exception exception, CallTargetState state)
        {
            if (!MsTestIntegration.IsEnabled)
            {
                return new CallTargetReturn<bool>(returnValue);
            }

            var customState = (CustomState)state.State;
            var notRunnableResult = customState.GetNotRunnableResult();

            if (!returnValue && notRunnableResult is Array notRunnableResultArray && notRunnableResultArray.Length == 1)
            {
                object unitTestResultObject = notRunnableResultArray.GetValue(0);

                if (unitTestResultObject != null &&
                    unitTestResultObject.TryDuckCast<UnitTestResultStruct>(out var unitTestResult) &&
                    customState.TestMethodInfo.TryDuckCast<ITestMethod>(out var testMethodInfo))
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
            }

            return new CallTargetReturn<bool>(returnValue);
        }

        private readonly struct CustomState
        {
            public readonly object TestMethod;
            public readonly object TestMethodInfo;
            public readonly IntPtr NotRunnableResultIntPtr;

            internal CustomState(object testMethod, object testMethodInfo, IntPtr notRunnableResultIntPtr)
            {
                TestMethod = testMethod;
                TestMethodInfo = testMethodInfo;
                NotRunnableResultIntPtr = notRunnableResultIntPtr;
            }

            public object GetNotRunnableResult()
            {
                try
                {
                    return PtrConverter<object>.GetValue(NotRunnableResultIntPtr);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static class PtrConverter<T>
        {
            private static GetPointerDelegate _getPointer;
            private static GetValueDelegate _getValue;

            static PtrConverter()
            {
                var dGetPointer = new DynamicMethod("GetPointer", typeof(IntPtr), new Type[] { typeof(T).MakeByRefType() }, typeof(PtrConverter<T>), true);
                var ilGetPointer = dGetPointer.GetILGenerator();
                ilGetPointer.Emit(OpCodes.Ldarg_0);
                ilGetPointer.Emit(OpCodes.Ret);
                _getPointer = (GetPointerDelegate)dGetPointer.CreateDelegate(typeof(GetPointerDelegate));

                var dGetValue = new DynamicMethod("GetValue", typeof(T), new Type[] { typeof(IntPtr) }, typeof(PtrConverter<T>), true);
                var ilGetValue = dGetValue.GetILGenerator();
                ilGetValue.Emit(OpCodes.Ldarg_0);
                ilGetValue.Emit(OpCodes.Ldobj, typeof(T));
                ilGetValue.Emit(OpCodes.Ret);
                _getValue = (GetValueDelegate)dGetValue.CreateDelegate(typeof(GetValueDelegate));
            }

            private delegate IntPtr GetPointerDelegate(ref T arg);

            private delegate T GetValueDelegate(IntPtr ptr);

            public static IntPtr GetPointer(ref T arg) => _getPointer(ref arg);

            public static T GetValue(IntPtr ptr) => _getValue(ptr);
        }
    }
}
