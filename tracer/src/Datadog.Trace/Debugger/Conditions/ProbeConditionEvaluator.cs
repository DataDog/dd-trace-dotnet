// <copyright file="ProbeConditionEvaluator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Instrumentation;

namespace Datadog.Trace.Debugger.Conditions;

internal class ProbeConditionEvaluator : ProbeExpressionEvaluatorBase
{
    internal ProbeConditionEvaluator(string probeId, EvaluateAt evaluateAt, DebuggerExpression[] expressions)
        : base(probeId, ProbeType.Snapshot, evaluateAt, expressions)
    {
        CompiledExpressions = new Lazy<CompiledExpression<bool>[]>(CompileExpression, true);
    }

    public Lazy<CompiledExpression<bool>[]> CompiledExpressions { get; set; }

    private CompiledExpression<bool>[] CompileExpression()
    {
        return CompileExpression<bool>();
    }

    internal override ExpressionEvaluationResult Evaluate()
    {
        var compiledExpressions = CompiledExpressions.Value;
        try
        {
            var condition = compiledExpressions[0].Delegate(MethodScopeMembers.InvocationTarget, MethodScopeMembers.Members);
            return new ExpressionEvaluationResult(succeeded: true, condition: condition);
        }
        catch (Exception e)
        {
            Log.Error(e, $"Failed to evaluate expression for probe {ProbeInfo.ProbeId}.");
            return new ExpressionEvaluationResult(succeeded: false);
        }
    }

    internal override unsafe void Post(
        ref ExpressionEvaluationResult result,
        ref MethodDebuggerState state,
        delegate* managed<ref MethodDebuggerState, void> finalizeSnapshotFuncPointer)
    {
        if (!result.Condition.HasValue)
        {
            // should not be here
            Log.Error("Condition result is missing even though the evaluation succeeded");
            state.IsActive = false;
        }
        else if (!result.Condition.Value)
        {
            state.IsActive = false;
            Log.Information("Skipping capturing of probe " + state.ProbeId + "because the condition evaluated to false");
        }
        else
        {
            if (ProbeInfo.EvaluateAt == EvaluateAt.Entry)
            {
                state.SnapshotCreator.CaptureEntryMethodStartMarker(ref state);
            }
            else
            {
                state.SnapshotCreator.CaptureExitMethodStartMarker(MethodScopeMembers.Return.Value, MethodScopeMembers.Exception, ref state);
            }

            state.SnapshotCreator.CaptureScopeMembers(MethodScopeMembers.Members);
            if (ProbeInfo.EvaluateAt == EvaluateAt.Entry)
            {
                state.SnapshotCreator.CaptureEntryMethodEndMarker(ref state);
            }
            else
            {
                state.SnapshotCreator.CaptureExitMethodEndMarker(ref state, finalizeSnapshotFuncPointer);
            }
        }

        base.Post(ref result, ref state);
    }
}
