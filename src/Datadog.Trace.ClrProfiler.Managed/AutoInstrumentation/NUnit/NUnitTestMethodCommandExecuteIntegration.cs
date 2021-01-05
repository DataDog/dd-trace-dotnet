using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.NUnit
{
    /// <summary>
    /// NUnit.Framework.Internal.Commands.TestMethodCommand.Execute() calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        Assembly = "nunit.framework",
        Type = "NUnit.Framework.Internal.Commands.TestMethodCommand",
        Method = "Execute",
        ReturnTypeName = "NUnit.Framework.Internal.TestResult",
        ParametersTypesNames = new[] { "NUnit.Framework.Internal.TestExecutionContext" },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = IntegrationName)]
    public class NUnitTestMethodCommandExecuteIntegration
    {
        private const string IntegrationName = "NUnit";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TContext">ExecutionContext type</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="executionContext">Execution context instance</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext executionContext)
            where TContext : ITestExecutionContext
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                return CallTargetState.GetDefault();
            }

            return new CallTargetState(NUnitIntegration.CreateScope(executionContext, typeof(TTarget)));
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
            Scope scope = (Scope)state.State;
            if (scope != null)
            {
                NUnitIntegration.FinishScope(scope, exception);
                scope.Dispose();
            }

            return new CallTargetReturn<TResult>(returnValue);
        }
    }
}
