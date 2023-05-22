// <copyright file="ExecutionNodeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net.ASM;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net.ASM
{
    /// <summary>
    /// GraphQL.Execution.ExecuteNodeAsyncIntegration calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        IntegrationName = GraphQLCommon.IntegrationName,
        MethodName = "ExecuteNodeAsync",
        ReturnTypeName = "System.Threading.Tasks.Task",
        ParameterTypeNames = new[] { GraphQLCommon.ExecutionContextTypeName, "GraphQL.Execution.ExecutionNode" },
        AssemblyName = GraphQLCommon.GraphQLAssembly,
        TypeName = "GraphQL.Execution.ExecutionStrategy",
        MinimumVersion = GraphQLCommon.Major2Minor3,
        MaximumVersion = GraphQLCommon.Major4)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ExecutionNodeIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TContext">Type of the execution context</typeparam>
        /// <typeparam name="TNode">Type of the execution node</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="context">The execution context of the GraphQL operation.</param>
        /// <param name="node">The execution node of the GraphQL operation.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TContext, TNode>(TTarget instance, TContext context, TNode node)
        where TNode : IExecutionNode
        {
            GraphQLSecurity.RegisterResolver(context, node, false);
            return new CallTargetState(null);
        }
    }
}
