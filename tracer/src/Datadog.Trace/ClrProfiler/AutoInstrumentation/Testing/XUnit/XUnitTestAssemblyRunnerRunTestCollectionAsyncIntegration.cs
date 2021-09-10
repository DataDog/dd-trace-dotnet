// <copyright file="XUnitTestAssemblyRunnerRunTestCollectionAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit
{
    /// <summary>
    /// Xunit.Sdk.TestAssemblyRunner`1.RunTestCollectionAsync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyNames = new[] { "xunit.execution.dotnet", "xunit.execution.desktop" },
        TypeName = "Xunit.Sdk.TestAssemblyRunner`1",
        MethodName = "RunTestCollectionAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1<Xunit.Sdk.RunSummary>",
        ParameterTypeNames = new[] { "Xunit.Sdk.IMessageBus", "_", "_", "_" },
        MinimumVersion = "2.2.0",
        MaximumVersion = "2.*.*",
        IntegrationName = XUnitIntegration.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class XUnitTestAssemblyRunnerRunTestCollectionAsyncIntegration
    {
        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return type</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            Common.FlushSpans(XUnitIntegration.IntegrationId);
            return returnValue;
        }
    }
}
