// <copyright file="XUnitTestInvokerRunAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit
{
    /// <summary>
    /// Xunit.Sdk.TestInvoker`1.RunAsync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyNames = new[] { "xunit.execution.dotnet", "xunit.execution.desktop" },
        TypeName = "Xunit.Sdk.TestInvoker`1",
        MethodName = "RunAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1<System.Decimal>",
        ParameterTypeNames = new string[0],
        MinimumVersion = "2.2.0",
        MaximumVersion = "2.*.*",
        IntegrationName = IntegrationName)]
    public static class XUnitTestInvokerRunAsyncIntegration
    {
        private const string IntegrationName = nameof(IntegrationIds.XUnit);
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            if (!XUnitIntegration.IsEnabled)
            {
                return CallTargetState.GetDefault();
            }

            TestInvokerStruct invokerInstance = instance.DuckCast<TestInvokerStruct>();
            TestRunnerStruct runnerInstance = new TestRunnerStruct
            {
                Aggregator = invokerInstance.Aggregator,
                TestCase = invokerInstance.TestCase,
                TestClass = invokerInstance.TestClass,
                TestMethod = invokerInstance.TestMethod,
                TestMethodArguments = invokerInstance.TestMethodArguments
            };

            return new CallTargetState(XUnitIntegration.CreateScope(ref runnerInstance, instance.GetType()));
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
        public static decimal OnAsyncMethodEnd<TTarget>(TTarget instance, decimal returnValue, Exception exception, CallTargetState state)
        {
            Scope scope = state.Scope;
            if (scope != null)
            {
                TestInvokerStruct invokerInstance = instance.DuckCast<TestInvokerStruct>();
                XUnitIntegration.FinishScope(scope, invokerInstance.Aggregator);
            }

            return returnValue;
        }
    }
}
