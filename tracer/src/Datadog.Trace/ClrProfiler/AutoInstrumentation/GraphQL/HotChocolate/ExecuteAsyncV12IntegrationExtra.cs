// <copyright file="ExecuteAsyncV12IntegrationExtra.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.Processing.WorkScheduler calltarget instrumentation
    /// </summary>
    [InstrumentMethodAttribute(
        IntegrationName = HotChocolateCommon.IntegrationName,
        MethodName = "ExecuteAsync",
        ReturnTypeName = "System.Threading.Tasks.Task",
        ParameterTypeNames = new string[0],
        AssemblyName = "HotChocolate.Execution",
        TypeName = "HotChocolate.Execution.Processing.WorkScheduler",
        MinimumVersion = "12",
        MaximumVersion = "12.*.*")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ExecuteAsyncV12IntegrationExtra
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
            where TTarget : IWorkScheduler
        {
            return new CallTargetState(scope: HotChocolateCommon.UpdateScopeFromExecuteAsync(Tracer.Instance, instance));
        }
    }
}
