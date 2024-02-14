// <copyright file="GraphQLParserParseArgumentIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    /// <summary>
    /// GraphQLParser.AST.GraphQLArguments GraphQLParser.ParserContext::ParseArguments() calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "GraphQLParser",
        TypeName = "GraphQLParser.ParserContext",
        MethodName = "ParseArgument",
        ReturnTypeName = "GraphQLParser.AST.GraphQLArgument",
        ParameterTypeNames = [],
        MinimumVersion = "8.0.0",
        MaximumVersion = "8.*.*",
        IntegrationName = GraphQLCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class GraphQLParserParseArgumentIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<GraphQLParserParseArgumentIntegration>();

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value (GraphQLParser.AST.GraphQLArgument)</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Instance of GraphQLParser.AST.GraphQLArgument</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A return value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            // Check for execpetion

            try
            {
                if (exception is not null || returnValue is null || !Iast.Iast.Instance.Settings.Enabled)
                {
                    return new CallTargetReturn<TReturn>(returnValue);
                }

                var arg = returnValue.DuckCast<GraphQLArgumentStruct>();
                var name = arg.Name.StringValue;
                var stringValue = arg.Value.DuckCast<IHasValueNode>().Value.ToString();
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(stringValue))
                {
                    return new CallTargetReturn<TReturn>(returnValue);
                }

                Iast.IastModule.GetIastContext()?.AddGraphQlResolverArgument(name, stringValue);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error instrumenting method {MethodName}", "GraphQLParser.ParserContext.ParseArgument()");
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
