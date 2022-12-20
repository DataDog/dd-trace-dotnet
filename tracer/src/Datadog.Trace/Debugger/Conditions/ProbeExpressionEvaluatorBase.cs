// <copyright file="ProbeExpressionEvaluatorBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Instrumentation;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Conditions;

internal abstract class ProbeExpressionEvaluatorBase
{
    protected static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeExpressionEvaluatorBase));
    private bool _shouldEvaluate = true;

    protected ProbeExpressionEvaluatorBase(string probeId, ProbeType probeType, EvaluateAt evaluateAt, DebuggerExpression[] expressions)
    {
        ProbeInfo = new ProbeInfo(probeId, probeType, evaluateAt);
        DebuggerExpressions = expressions;
    }

    public MethodScopeMembers MethodScopeMembers { get; private set; }

    public ProbeInfo ProbeInfo { get; set; }

    public DebuggerExpression[] DebuggerExpressions { get; set; }

    public static ProbeExpressionEvaluatorBase CreateEvaluator(string probeId, ProbeType probeType, EvaluateAt evaluateAt, DebuggerExpression[] expressions)
    {
        switch (probeType)
        {
            case ProbeType.Snapshot:
                return new ProbeConditionEvaluator(probeId, evaluateAt, expressions);
            case ProbeType.Log:
                return new ProbeTemplateEvaluator(probeId, evaluateAt, expressions);
            case ProbeType.Metric:
                throw new NotImplementedException("Metric probe not yet implemented");
            default:
                throw new ArgumentOutOfRangeException(nameof(probeType), probeType, null);
        }
    }

    internal void AddMember(string name, Type type, object value, ScopeMemberKind memberKind)
    {
        switch (memberKind)
        {
            case ScopeMemberKind.This:
                MethodScopeMembers.InvocationTarget = new ScopeMember(name, type, value, ScopeMemberKind.This);
                return;
            case ScopeMemberKind.Exception:
                MethodScopeMembers.Exception = (Exception)value;
                break;
            case ScopeMemberKind.Return:
                MethodScopeMembers.Return = new ScopeMember("return", type, value, ScopeMemberKind.Return);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(memberKind) + "is not a valid enum value");
        }

        MethodScopeMembers.AddMember(new ScopeMember(name, type, value, memberKind));
    }

    public bool ShouldEvaluate(EvaluateAt evaluateAt)
    {
        return _shouldEvaluate && evaluateAt == ProbeInfo.EvaluateAt;
    }

    protected CompiledExpression<T>[] CompileExpression<T>()
    {
        var compiledExpressions = new CompiledExpression<T>[DebuggerExpressions.Length];
        for (int i = 0; i < DebuggerExpressions.Length; i++)
        {
            var current = DebuggerExpressions[i];
            compiledExpressions[i] = ProbeExpressionParser.ParseExpression<T>(current.Json, MethodScopeMembers.InvocationTarget, MethodScopeMembers.Members);
        }

        return compiledExpressions;
    }

    internal abstract ExpressionEvaluationResult Evaluate();

    internal virtual unsafe void Post(ref ExpressionEvaluationResult result, ref MethodDebuggerState state, delegate* managed<ref MethodDebuggerState, void> finalizeSnapshotFuncPointer = null)
    {
        MethodScopeMembers?.Clean();
        MethodScopeMembers = null;
    }

    public void CreateMethodScopeMembers(int numberOfLocals, int numberOfArguments)
    {
        MethodScopeMembers = new MethodScopeMembers(numberOfLocals, numberOfArguments);
    }
}
