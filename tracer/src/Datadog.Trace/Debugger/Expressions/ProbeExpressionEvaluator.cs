// <copyright file="ProbeExpressionEvaluator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.Expressions;

internal class ProbeExpressionEvaluator
{
    internal ProbeExpressionEvaluator(
        DebuggerExpression[] templates,
        DebuggerExpression? condition,
        DebuggerExpression? metric,
        MethodScopeMembers scopeMembers)
    {
        Templates = templates;
        Condition = condition;
        Metric = metric;
        ScopeMembers = scopeMembers;
        CompiledTemplates = new Lazy<CompiledExpression<string>[]>(CompileTemplates, true);
        CompiledCondition = new Lazy<CompiledExpression<bool>?>(CompileCondition, true);
        CompiledMetric = new Lazy<CompiledExpression<double>?>(CompileMetric, true);
    }

    internal MethodScopeMembers ScopeMembers { get; }

    internal DebuggerExpression[] Templates { get; }

    internal DebuggerExpression? Condition { get; }

    internal DebuggerExpression? Metric { get; }

    internal Lazy<CompiledExpression<string>[]> CompiledTemplates { get; set; }

    internal Lazy<CompiledExpression<bool>?> CompiledCondition { get; set; }

    internal Lazy<CompiledExpression<double>?> CompiledMetric { get; set; }

    internal ExpressionEvaluationResult Evaluate()
    {
        ExpressionEvaluationResult result = default;
        EvaluateTemplates(ref result);
        EvaluateCondition(ref result);
        EvaluateMetric(ref result);
        return result;
    }

    private void EvaluateTemplates(ref ExpressionEvaluationResult result)
    {
        var resultBuilder = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
        try
        {
            var compiledExpressions = CompiledTemplates.Value;

            for (int i = 0; i < compiledExpressions.Length; i++)
            {
                try
                {
                    resultBuilder.Append(Templates[i].Str ?? compiledExpressions[i].Delegate(ScopeMembers.InvocationTarget, ScopeMembers.Members));
                    if (compiledExpressions[i].Errors != null)
                    {
                        (result.Errors ??= new List<EvaluationError>()).AddRange(compiledExpressions[i].Errors);
                    }
                }
                catch (Exception e)
                {
                    HandleException(result, compiledExpressions[i], e.Message);
                }
            }

            var finalMessage = StringBuilderCache.GetStringAndRelease(resultBuilder);
            result.Template = finalMessage;
        }
        catch (Exception e)
        {
            result.Template = e.Message;
            (result.Errors ??= new List<EvaluationError>()).Add(new EvaluationError { Expression = null, Message = e.Message });
        }
    }

    private void EvaluateCondition(ref ExpressionEvaluationResult result)
    {
        CompiledExpression<bool> compiledExpression = default;
        try
        {
            if (!CompiledCondition.Value.HasValue)
            {
                return;
            }

            compiledExpression = CompiledCondition.Value.Value;
            var condition = compiledExpression.Delegate(ScopeMembers.InvocationTarget, ScopeMembers.Members);
            result.Condition = condition;
            if (compiledExpression.Errors != null)
            {
                (result.Errors ??= new List<EvaluationError>()).AddRange(compiledExpression.Errors);
            }
        }
        catch (Exception e)
        {
            HandleException(result, compiledExpression, e.Message);
            result.Condition = true;
        }
    }

    private void EvaluateMetric(ref ExpressionEvaluationResult result)
    {
        CompiledExpression<double> compiledExpression = default;
        try
        {
            if (!CompiledMetric.Value.HasValue)
            {
                return;
            }

            compiledExpression = CompiledMetric.Value.Value;
            var metric = compiledExpression.Delegate(ScopeMembers.InvocationTarget, ScopeMembers.Members);
            result.Metric = metric;
            if (compiledExpression.Errors != null)
            {
                (result.Errors ??= new List<EvaluationError>()).AddRange(compiledExpression.Errors);
            }
        }
        catch (Exception e)
        {
            HandleException(result, compiledExpression, e.Message);
        }
    }

    private CompiledExpression<string>[] CompileTemplates()
    {
        var compiledExpressions = new CompiledExpression<string>[Templates.Length];
        for (int i = 0; i < Templates.Length; i++)
        {
            var current = Templates[i];
            if (current.Json != null)
            {
                compiledExpressions[i] = ProbeExpressionParser<string>.ParseExpression(current.Json, ScopeMembers.InvocationTarget, ScopeMembers.Members);
            }
            else if (current.Str != null)
            {
                compiledExpressions[i] = new CompiledExpression<string>(null, null, current.Str, null);
            }
            else
            {
                throw new Exception($"{nameof(CompileTemplates)}: Template segment must have json or str");
            }
        }

        return compiledExpressions;
    }

    private CompiledExpression<bool>? CompileCondition()
    {
        if (!Condition.HasValue)
        {
            return null;
        }

        return ProbeExpressionParser<bool>.ParseExpression(Condition.Value.Json, ScopeMembers.InvocationTarget, ScopeMembers.Members);
    }

    private CompiledExpression<double>? CompileMetric()
    {
        if (!Metric.HasValue)
        {
            return null;
        }

        return ProbeExpressionParser<double>.ParseExpression(Metric?.Json, ScopeMembers.InvocationTarget, ScopeMembers.Members);
    }

    private void HandleException<T>(ExpressionEvaluationResult result, CompiledExpression<T> compiledExpression, string message)
    {
        result.Errors ??= new List<EvaluationError>();
        if (compiledExpression.Errors != null)
        {
            result.Errors.AddRange(compiledExpression.Errors);
        }

        result.Errors.Add(new EvaluationError { Expression = compiledExpression.ParsedExpression?.ToString() ?? "null", Message = message });
    }
}
