// <copyright file="ProbeTemplateEvaluator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Instrumentation;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.Conditions
{
    internal class ProbeTemplateEvaluator : ProbeExpressionEvaluatorBase
    {
        internal ProbeTemplateEvaluator(string probeId, EvaluateAt evaluateAt, DebuggerExpression[] expressions)
        : base(probeId, ProbeType.Log, evaluateAt, expressions)
        {
            CompiledExpressions = new Lazy<CompiledExpression<string>[]>(CompileExpression, true);
        }

        public Lazy<CompiledExpression<string>[]> CompiledExpressions { get; set; }

        private CompiledExpression<string>[] CompileExpression()
        {
            return CompileExpression<string>();
        }

        internal override ExpressionEvaluationResult Evaluate()
        {
            var resultBuilder = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
            var compiledExpressions = CompiledExpressions.Value;
            try
            {
                for (int i = 0; i < compiledExpressions.Length; i++)
                {
                    try
                    {
                        resultBuilder.AppendLine(compiledExpressions[i].Delegate(MethodScopeMembers.InvocationTarget, MethodScopeMembers.Members));
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, $"Failed to evaluate expression for probe {ProbeInfo.ProbeId}.");
                    }
                }

                var result = StringBuilderCache.GetStringAndRelease(resultBuilder);
                return new ExpressionEvaluationResult(succeeded: true, expression: result);
            }
            catch (Exception e)
            {
                Log.Error(e, $"Failed to evaluate expression for probe {ProbeInfo.ProbeId}");
                return new ExpressionEvaluationResult(succeeded: false);
            }
        }

        internal override unsafe void Post(ref ExpressionEvaluationResult result, ref MethodDebuggerState state, delegate* managed<ref MethodDebuggerState, void> finalizeSnapshotFuncPointer)
        {
            base.Post(ref result, ref state);
        }
    }
}
