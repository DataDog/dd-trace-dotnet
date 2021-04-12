using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2
{
    /// <summary>
    /// Microsoft.VisualStudio.TestPlatform.TestFramework.Execute calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter",
        TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodRunner",
        MethodName = "Execute",
        ReturnTypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestResult",
        ParameterTypeNames = new string[0],
        MinimumVersion = "14.0.0",
        MaximumVersion = "14.*.*",
        IntegrationName = IntegrationName)]
    public class TestMethodRunnerExecuteIntegration
    {
        private const string IntegrationName = nameof(IntegrationIds.MsTestV2);
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestMethodRunnerExecuteIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            if (!Common.TestTracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return CallTargetState.GetDefault();
            }

            return new CallTargetState((Scope)null, instance);
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
            where TTarget : ITestMethodRunner
        {
            if (returnValue is Array returnValueArray && returnValueArray.Length == 1)
            {
                object unitTestResultObject = returnValueArray.GetValue(0);
                if (unitTestResultObject != null && unitTestResultObject.TryDuckCast<UnitTestResultStruct>(out var unitTestResult))
                {
                    var outcome = unitTestResult.Outcome;
                    if (outcome == UnitTestResultOutcome.Inconclusive || outcome == UnitTestResultOutcome.NotRunnable || outcome == UnitTestResultOutcome.Ignored)
                    {
                        // This instrumentation catches all tests being ignored
                        if (state.State != null && state.State.TryDuckCast<ITestMethodRunner>(out var testMethodRunner))
                        {
                            var scope = MsTestIntegration.OnMethodBegin(testMethodRunner.TestMethodInfo, testMethodRunner.GetType());
                            scope.Span.SetTag(TestTags.Status, TestTags.StatusSkip);
                            scope.Span.SetTag(TestTags.SkipReason, unitTestResult.ErrorMessage);
                            scope.Dispose();
                        }
                    }
                }
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
