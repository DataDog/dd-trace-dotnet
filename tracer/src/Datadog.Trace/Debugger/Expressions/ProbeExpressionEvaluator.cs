// <copyright file="ProbeExpressionEvaluator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.Expressions;

internal class ProbeExpressionEvaluator
{
    private Lazy<CompiledExpression<string>[]> _compiledTemplates;

    private Lazy<CompiledExpression<bool>?> _compiledCondition;

    private Lazy<CompiledExpression<double>?> _compiledMetric;

    private int _expressionsCompiled;

    internal ProbeExpressionEvaluator(
        DebuggerExpression[] templates,
        DebuggerExpression? condition,
        DebuggerExpression? metric)
    {
        Templates = templates;
        Condition = condition;
        Metric = metric;
    }

    /// <summary>
    /// Gets CompiledTemplates, for use in "DebuggerExpressionLanguageTests"
    /// </summary>
    internal CompiledExpression<string>[] CompiledTemplates
    {
        get
        {
            return _compiledTemplates.Value;
        }
    }

    /// <summary>
    /// Gets CompiledCondition, for use in "DebuggerExpressionLanguageTests"
    /// </summary>
    internal CompiledExpression<bool>? CompiledCondition
    {
        get
        {
            return _compiledCondition.Value;
        }
    }

    internal DebuggerExpression[] Templates { get; }

    internal DebuggerExpression? Condition { get; }

    private DebuggerExpression? Metric { get; }

    internal ExpressionEvaluationResult Evaluate(MethodScopeMembers scopeMembers)
    {
        if (Interlocked.CompareExchange(ref _expressionsCompiled, 1, 0) == 0)
        {
            Interlocked.CompareExchange(ref _compiledTemplates, new Lazy<CompiledExpression<string>[]>(() => CompileTemplates(scopeMembers)), null);
            Interlocked.CompareExchange(ref _compiledCondition, new Lazy<CompiledExpression<bool>?>(() => CompileCondition(scopeMembers)), null);
            Interlocked.CompareExchange(ref _compiledMetric, new Lazy<CompiledExpression<double>?>(() => CompileMetric(scopeMembers)), null);
        }

        ExpressionEvaluationResult result = default;
        EvaluateTemplates(ref result, scopeMembers);
        EvaluateCondition(ref result, scopeMembers);
        EvaluateMetric(ref result, scopeMembers);
        return result;
    }

    private void EvaluateTemplates(ref ExpressionEvaluationResult result, MethodScopeMembers scopeMembers)
    {
        var resultBuilder = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
        try
        {
            EnsureNotNull(_compiledTemplates);

            var compiledExpressions = _compiledTemplates.Value;
            if (compiledExpressions == null)
            {
                return;
            }

            for (int i = 0; i < compiledExpressions.Length; i++)
            {
                try
                {
                    if (IsLiteral(Templates[i]))
                    {
                        resultBuilder.Append(Templates[i].Str);
                    }
                    else if (IsExpression(Templates[i]))
                    {
                        resultBuilder.Append(compiledExpressions[i].Delegate(scopeMembers.InvocationTarget, scopeMembers.Return, scopeMembers.Duration, scopeMembers.Exception, scopeMembers.Members));
                        if (compiledExpressions[i].Errors != null)
                        {
                            (result.Errors ??= new List<EvaluationError>()).AddRange(compiledExpressions[i].Errors);
                        }
                    }
                }
                catch (Exception e)
                {
                    HandleException(ref result, compiledExpressions[i], e.Message);
                }
            }

            result.Template = resultBuilder.ToString();
        }
        catch (Exception e)
        {
            result.Template = e.Message;
            (result.Errors ??= new List<EvaluationError>()).Add(new EvaluationError { Expression = null, Message = e.Message });
        }
        finally
        {
            StringBuilderCache.Release(resultBuilder);
        }
    }

    private void EvaluateCondition(ref ExpressionEvaluationResult result, MethodScopeMembers scopeMembers)
    {
        CompiledExpression<bool> compiledExpression = default;
        try
        {
            EnsureNotNull(_compiledCondition);

            if (!_compiledCondition.Value.HasValue)
            {
                return;
            }

            compiledExpression = _compiledCondition.Value.Value;
            var condition = compiledExpression.Delegate(scopeMembers.InvocationTarget, scopeMembers.Return, scopeMembers.Duration, scopeMembers.Exception, scopeMembers.Members);
            result.Condition = condition;
            if (compiledExpression.Errors != null)
            {
                (result.Errors ??= new List<EvaluationError>()).AddRange(compiledExpression.Errors);
            }
        }
        catch (Exception e)
        {
            HandleException(ref result, compiledExpression, e.Message);
            result.Condition = true;
        }
    }

    private void EvaluateMetric(ref ExpressionEvaluationResult result, MethodScopeMembers scopeMembers)
    {
        CompiledExpression<double> compiledExpression = default;
        try
        {
            EnsureNotNull(_compiledMetric);

            if (!_compiledMetric.Value.HasValue)
            {
                return;
            }

            compiledExpression = _compiledMetric.Value.Value;
            var metric = compiledExpression.Delegate(scopeMembers.InvocationTarget, scopeMembers.Return, scopeMembers.Duration, scopeMembers.Exception, scopeMembers.Members);
            result.Metric = metric;
            if (compiledExpression.Errors != null)
            {
                (result.Errors ??= new List<EvaluationError>()).AddRange(compiledExpression.Errors);
            }
        }
        catch (Exception e)
        {
            HandleException(ref result, compiledExpression, e.Message);
        }
    }

    private CompiledExpression<string>[] CompileTemplates(MethodScopeMembers scopeMembers)
    {
        if (Templates == null)
        {
            return null;
        }

        var compiledExpressions = new CompiledExpression<string>[Templates.Length];
        for (int i = 0; i < Templates.Length; i++)
        {
            var current = Templates[i];
            if (current.Json != null)
            {
                compiledExpressions[i] = ProbeExpressionParser<string>.ParseExpression(current.Json, scopeMembers);
            }
            else
            {
                compiledExpressions[i] = new CompiledExpression<string>(null, null, current.Str, null);
            }
        }

        return compiledExpressions;
    }

    private CompiledExpression<bool>? CompileCondition(MethodScopeMembers scopeMembers)
    {
        if (!Condition.HasValue)
        {
            return null;
        }

        return ProbeExpressionParser<bool>.ParseExpression(Condition.Value.Json, scopeMembers);
    }

    private CompiledExpression<double>? CompileMetric(MethodScopeMembers scopeMembers)
    {
        if (!Metric.HasValue)
        {
            return null;
        }

        return ProbeExpressionParser<double>.ParseExpression(Metric?.Json, scopeMembers);
    }

    private void EnsureNotNull<T>(T value)
        where T : class
    {
        if (value != null)
        {
            return;
        }

        var sw = new SpinWait();
        while (Volatile.Read(ref value) == null)
        {
            sw.SpinOnce();
        }
    }

    private bool IsLiteral(DebuggerExpression expression)
    {
        return string.IsNullOrEmpty(expression.Json);
    }

    private bool IsExpression(DebuggerExpression expression)
    {
        return !string.IsNullOrEmpty(expression.Json) && string.IsNullOrEmpty(expression.Str);
    }

    private void HandleException<T>(ref ExpressionEvaluationResult result, CompiledExpression<T> compiledExpression, string message)
    {
        result.Errors ??= new List<EvaluationError>();
        if (compiledExpression.Errors != null)
        {
            result.Errors.AddRange(compiledExpression.Errors);
        }

        result.Errors.Add(new EvaluationError { Expression = GetRelevantExpression(compiledExpression.ParsedExpression), Message = message });
    }

    private string GetRelevantExpression(Expression parsedExpression)
    {
        const string resultAssignment = "$dd_el_result = ";
        string relevant = null;
        switch (parsedExpression)
        {
            case null:
                return "N/A";
            case LambdaExpression { Body: BlockExpression block }:
                {
                    var expressions = block.Expressions;
                    if (expressions.Count == 0)
                    {
                        return parsedExpression.ToString();
                    }

                    var last = expressions[expressions.Count - 1].ToString();
                    relevant = last.Contains(resultAssignment) ? last : expressions[expressions.Count - 2].ToString();
                    break;
                }
        }

        if (relevant == null || !relevant.Contains(resultAssignment))
        {
            relevant = parsedExpression.ToString();
        }

        int indexToRemove = relevant.IndexOf(resultAssignment, StringComparison.Ordinal);
        if (indexToRemove >= 0)
        {
            relevant = relevant.Substring(indexToRemove + resultAssignment.Length);
        }

        return relevant;
    }
}
