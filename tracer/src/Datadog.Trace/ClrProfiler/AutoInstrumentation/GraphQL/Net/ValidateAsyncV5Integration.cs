// <copyright file="ValidateAsyncV5Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    /// <summary>
    /// GraphQL.Validation.DocumentValidator calltarget instrumentation for GraphQL 4
    /// </summary>
    [InstrumentMethod(
        AssemblyName = GraphQLCommon.GraphQLAssembly,
        TypeName = "GraphQL.Validation.DocumentValidator",
        MethodName = "ValidateAsyncCoreAsync",
        ReturnTypeName = ClrNames.GenericParameterTask,
        ParameterTypeNames = new[] { "GraphQL.Validation.ValidationContext", "System.Collections.Generic.IEnumerable`1[GraphQL.Validation.IValidationRule]" },
        MinimumVersion = GraphQLCommon.Major5,
        MaximumVersion = GraphQLCommon.Major5,
        IntegrationName = GraphQLCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ValidateAsyncV5Integration
    {
        internal static CallTargetState OnMethodBegin<TTarget, TValidationContext, TRules>(TTarget instance, TValidationContext validationContext, TRules rules)
            where TValidationContext : IValidationContext
        {
            return new CallTargetState(GraphQLCommon.CreateScopeFromValidate(Tracer.Instance, validationContext.Document.Source?.ToString()));
        }

        internal static TValidationResult OnAsyncMethodEnd<TTarget, TValidationResult>(TTarget instance, TValidationResult validationResult, Exception exception, in CallTargetState state)
            where TValidationResult : IValidationResultTuple
        {
            var scope = state.Scope;

            if (state.Scope is null)
            {
                return validationResult;
            }

            try
            {
                if (exception != null)
                {
                    scope.Span?.SetException(exception);
                }
                else
                {
                    GraphQLCommon.RecordExecutionErrorsIfPresent(scope.Span, GraphQLCommon.ValidationErrorType, validationResult.Item1.Errors);
                }
            }
            finally
            {
                scope.Dispose();
            }

            return validationResult;
        }
    }
}
