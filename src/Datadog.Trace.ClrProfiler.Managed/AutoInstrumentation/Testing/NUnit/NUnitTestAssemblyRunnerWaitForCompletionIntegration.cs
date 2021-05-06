using System;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// NUnit.Framework.Api.NUnitTestAssemblyRunner.WaitForCompletion() calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "nunit.framework",
        TypeName = "NUnit.Framework.Api.NUnitTestAssemblyRunner",
        MethodName = "WaitForCompletion",
        ReturnTypeName = ClrNames.Bool,
        ParameterTypeNames = new[] { ClrNames.Int32 },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = IntegrationName)]
    public class NUnitTestAssemblyRunnerWaitForCompletionIntegration
    {
        private const string IntegrationName = nameof(IntegrationIds.NUnit);
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="timeout">Time to wait in milliseconds.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance, int timeout)
        {
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
        public static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult returnValue, Exception exception, CallTargetState state)
        {
            if (Common.TestTracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                NUnitIntegration.FlushSpans();
            }

            return new CallTargetReturn<TResult>(returnValue);
        }
    }
}
