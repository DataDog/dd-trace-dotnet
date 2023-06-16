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
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Debugger.Expressions;

internal class ProbeExpressionEvaluator
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeExpressionEvaluator));

    private Lazy<CompiledExpression<string>[]> _compiledTemplates;

    private Lazy<CompiledExpression<bool>?> _compiledCondition;

    private Lazy<CompiledExpression<double>?> _compiledMetric;

    private Lazy<KeyValuePair<CompiledExpression<bool>, KeyValuePair<string, CompiledExpression<string>[]>[]>[]> _compiledDecorations;

    private int _expressionsCompiled;

    internal ProbeExpressionEvaluator(
        DebuggerExpression[] templates,
        DebuggerExpression? condition,
        DebuggerExpression? metric,
        KeyValuePair<DebuggerExpression?, KeyValuePair<string, DebuggerExpression[]>[]>[] spanDecorations)
    {
        Templates = templates;
        Condition = condition;
        Metric = metric;
        SpanDecorations = spanDecorations;
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

    internal CompiledExpression<double>? CompiledMetric
    {
        get
        {
            return _compiledMetric.Value;
        }
    }

    internal KeyValuePair<CompiledExpression<bool>, KeyValuePair<string, CompiledExpression<string>[]>[]>[] CompiledDecorations
    {
        get
        {
            return _compiledDecorations.Value;
        }
    }

    internal DebuggerExpression[] Templates { get; }

    internal DebuggerExpression? Condition { get; }

    internal DebuggerExpression? Metric { get; }

    internal KeyValuePair<DebuggerExpression?, KeyValuePair<string, DebuggerExpression[]>[]>[] SpanDecorations { get; }

    internal ExpressionEvaluationResult Evaluate(MethodScopeMembers scopeMembers)
    {
        if (Interlocked.CompareExchange(ref _expressionsCompiled, 1, 0) == 0)
        {
            Interlocked.CompareExchange(ref _compiledTemplates, new Lazy<CompiledExpression<string>[]>(() => CompileTemplates(scopeMembers)), null);
            Interlocked.CompareExchange(ref _compiledCondition, new Lazy<CompiledExpression<bool>?>(() => CompileCondition(scopeMembers)), null);
            Interlocked.CompareExchange(ref _compiledMetric, new Lazy<CompiledExpression<double>?>(() => CompileMetric(scopeMembers)), null);
            Interlocked.CompareExchange(ref _compiledDecorations, new Lazy<KeyValuePair<CompiledExpression<bool>, KeyValuePair<string, CompiledExpression<string>[]>[]>[]>(() => CompileDecorations(scopeMembers)), null);
        }

        ExpressionEvaluationResult result = default;
        EvaluateTemplates(ref result, scopeMembers);
        EvaluateCondition(ref result, scopeMembers);
        EvaluateMetric(ref result, scopeMembers);
        EvaluateSpanDecorations(ref result, scopeMembers);
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
                    if (IsLiteral(Templates[i]) == true)
                    {
                        resultBuilder.Append(Templates[i].Str);
                    }
                    else if (IsExpression(Templates[i]) == true)
                    {
                        resultBuilder.Append(compiledExpressions[i].Delegate(scopeMembers.InvocationTarget, scopeMembers.Return, scopeMembers.Duration, scopeMembers.Exception, scopeMembers.Members));
                        if (compiledExpressions[i].Errors != null)
                        {
                            (result.Errors ??= new List<EvaluationError>()).AddRange(compiledExpressions[i].Errors);
                        }
                    }
                    else
                    {
                        HandleException(ref result, compiledExpressions[i], new ArgumentException("invalid template"));
                    }
                }
                catch (Exception e)
                {
                    HandleException(ref result, compiledExpressions[i], e);
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
            HandleException(ref result, compiledExpression, e);
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
            HandleException(ref result, compiledExpression, e);
        }
    }

    private void EvaluateSpanDecorations(ref ExpressionEvaluationResult result, MethodScopeMembers scopeMembers)
    {
        var decorations = new List<ExpressionEvaluationResult.DecorationResult>();
        try
        {
            EnsureNotNull(_compiledDecorations);

            var compiledDecorations = _compiledDecorations.Value;
            if (compiledDecorations == null)
            {
                Log.Debug($"{nameof(ProbeExpressionEvaluator)}.{nameof(EvaluateSpanDecorations)}: {nameof(_compiledDecorations.Value)} is null");
                return;
            }

            for (int i = 0; i < compiledDecorations.Length; i++)
            {
                var current = compiledDecorations[i];
                try
                {
                    if (current.Key != default || IsExpression(current.Key))
                    {
                        // span decoration has an expression condition
                        var when = current.Key.Delegate(scopeMembers.InvocationTarget, scopeMembers.Return, scopeMembers.Duration, scopeMembers.Exception, scopeMembers.Members);
                        if (compiledDecorations[i].Key.Errors != null)
                        {
                            if (Log.IsEnabled(LogEventLevel.Debug))
                            {
                                Log.Debug("{Class}.{Method}: Error when evaluating an expression. {Errors}", nameof(ProbeExpressionEvaluator), nameof(EvaluateSpanDecorations), string.Join(";", compiledDecorations[i].Key.Errors));
                            }

                            (result.Errors ??= new List<EvaluationError>()).AddRange(current.Key.Errors);
                            continue;
                        }

                        if (!when)
                        {
                            Log.Information("{Class}.{Method}: Skipping span decoration because the `when` expression was `false`. {Expression}", nameof(ProbeExpressionEvaluator), nameof(EvaluateSpanDecorations), current.Key.RawExpression);
                            continue;
                        }
                    }
                    else if (IsLiteral(current.Key))
                    {
                        Log.Information("{Class}.{Method}: Skipping span decoration because the `when` expression is a string literal instead of expression. {Expression}", nameof(ProbeExpressionEvaluator), nameof(EvaluateSpanDecorations), current.Key.RawExpression);
                        continue;
                    }
                }
                catch (Exception e)
                {
                    HandleException(ref result, current.Key, e);
                    continue;
                }

                var tagsAndValues = current.Value;

                List<EvaluationError> errors = null;
                var resultBuilder = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

                for (int j = 0; j < tagsAndValues.Length; j++)
                {
                    // tags[key] = expression[] /* e.g. Name is {name} */
                    var tagAndValues = tagsAndValues[j];
                    try
                    {
                        for (int k = 0; k < tagAndValues.Value.Length; k++)
                        {
                            var compiledExpression = tagAndValues.Value[k];
                            try
                            {
                                if (IsLiteral(compiledExpression))
                                {
                                    resultBuilder.Append(compiledExpression.RawExpression);
                                }
                                else if (IsExpression(compiledExpression))
                                {
                                    var value = compiledExpression.Delegate(scopeMembers.InvocationTarget, scopeMembers.Return, scopeMembers.Duration, scopeMembers.Exception, scopeMembers.Members);
                                    resultBuilder.Append(value);
                                    if (compiledExpression.Errors != null)
                                    {
                                        (errors ??= new List<EvaluationError>()).AddRange(compiledExpression.Errors);
                                        if (Log.IsEnabled(LogEventLevel.Debug))
                                        {
                                            Log.Debug("{Class}.{Method}: Error when evaluating an expression. {Errors}", nameof(ProbeExpressionEvaluator), nameof(EvaluateSpanDecorations), string.Join(";", errors));
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                (errors ??= new List<EvaluationError>()).Add(new() { Message = e.Message, Expression = GetRelevantExpression(compiledExpression) });
                                if (compiledExpression.Errors != null)
                                {
                                    errors.AddRange(compiledExpression.Errors);
                                }

                                if (Log.IsEnabled(LogEventLevel.Debug))
                                {
                                    Log.Debug("{Class}.{Method}: Error when evaluating an expression. {Errors}", nameof(ProbeExpressionEvaluator), nameof(EvaluateSpanDecorations), string.Join(";", errors));
                                }
                            }
                        }

                        decorations.Add(new ExpressionEvaluationResult.DecorationResult { TagName = tagAndValues.Key, Value = resultBuilder.ToString(), Errors = errors?.ToArray() });
                        resultBuilder.Clear();
                        errors = null;
                    }
                    finally
                    {
                        StringBuilderCache.Release(resultBuilder);
                    }
                }
            }

            result.Decorations = decorations?.ToArray();
        }
        catch (Exception e)
        {
            (result.Errors ??= new List<EvaluationError>()).Add(new EvaluationError { Expression = null, Message = e.Message });
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("{Class}.{Method}: Error when evaluating an expression. {Errors}", nameof(ProbeExpressionEvaluator), nameof(EvaluateSpanDecorations), e.Message);
            }
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

    private KeyValuePair<CompiledExpression<bool>, KeyValuePair<string, CompiledExpression<string>[]>[]>[] CompileDecorations(MethodScopeMembers scopeMembers)
    {
        if (SpanDecorations == null)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("{Class}.{Method}: {SpanDecorations} is null", nameof(ProbeExpressionEvaluator), nameof(CompileDecorations), nameof(SpanDecorations));
            }

            return null;
        }

        var compiledExpressions = new KeyValuePair<CompiledExpression<bool>, KeyValuePair<string, CompiledExpression<string>[]>[]>[SpanDecorations.Length];
        for (int i = 0; i < SpanDecorations.Length; i++)
        {
            var current = SpanDecorations[i];
            CompiledExpression<bool> when;
            if (IsLiteral(current.Key) == true)
            {
                when = new CompiledExpression<bool>(
                    (_, _, _, _, _) => false,
                    null,
                    null,
                    new EvaluationError[] { new() { Expression = null, Message = "'when' should be a boolean expression, not a literal" } });
            }
            else
            {
                when = current.Key == null // span decoration doesn't must to have a condition
                           ? new CompiledExpression<bool>((_, _, _, _, _) => true, null, null, null)
                           : ProbeExpressionParser<bool>.ParseExpression(current.Key.Value.Json, scopeMembers);
            }

            var compiledTagsJ = new KeyValuePair<string, CompiledExpression<string>[]>[current.Value.Length];
            for (int j = 0; j < current.Value.Length; j++)
            {
                var tagName = current.Value[j].Key;
                var compiledTagsK = new CompiledExpression<string>[current.Value[j].Value.Length];
                for (int k = 0; k < current.Value[j].Value.Length; k++)
                {
                    var tag = current.Value[j].Value[k];
                    if (tag.Json != null)
                    {
                        compiledTagsK[k] = ProbeExpressionParser<string>.ParseExpression(tag.Json, scopeMembers);
                    }
                    else
                    {
                        compiledTagsK[k] = new CompiledExpression<string>(null, null, tag.Str, null);
                    }
                }

                compiledTagsJ[j] = new KeyValuePair<string, CompiledExpression<string>[]>(tagName, compiledTagsK);
            }

            compiledExpressions[i] = new KeyValuePair<CompiledExpression<bool>, KeyValuePair<string, CompiledExpression<string>[]>[]>(when, compiledTagsJ);
        }

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("{Class}.{Method}: Finished to compile span decorations", nameof(ProbeExpressionEvaluator), nameof(CompileDecorations));
        }

        return compiledExpressions;
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

    private bool? IsLiteral(DebuggerExpression? expression)
    {
        if (expression is null)
        {
            return null;
        }

        return string.IsNullOrEmpty(expression.Value.Json);
    }

    private bool IsLiteral<T>(CompiledExpression<T> expression)
    {
        return expression.Delegate == null && expression.ParsedExpression == null && expression.Errors == null && expression.RawExpression != null;
    }

    private bool? IsExpression(DebuggerExpression? expression)
    {
        if (expression is null)
        {
            return null;
        }

        return !string.IsNullOrEmpty(expression.Value.Json) && string.IsNullOrEmpty(expression.Value.Str);
    }

    private bool IsExpression<T>(CompiledExpression<T> expression)
    {
        return expression.Delegate != null && expression.ParsedExpression != null && expression.RawExpression != null;
    }

    private void HandleException<T>(ref ExpressionEvaluationResult result, CompiledExpression<T> compiledExpression, Exception e)
    {
        Log.Information(e, "Failed to parse probe expression: {Expression}", compiledExpression.RawExpression);
        result.Errors ??= new List<EvaluationError>();
        if (compiledExpression.Errors != null)
        {
            result.Errors.AddRange(compiledExpression.Errors);
        }

        result.Errors.Add(new EvaluationError { Expression = GetRelevantExpression(compiledExpression), Message = e.Message });
    }

    private string GetRelevantExpression<T>(CompiledExpression<T> compiledExpression)
    {
        const string resultAssignment = "$dd_el_result = ";
        string relevant = null;
        switch (compiledExpression.ParsedExpression)
        {
            case null:
                return compiledExpression.RawExpression ?? "N/A";
            case LambdaExpression { Body: BlockExpression block }:
                {
                    var expressions = block.Expressions;
                    if (expressions.Count == 0)
                    {
                        return compiledExpression.ParsedExpression.ToString();
                    }

                    var last = expressions[expressions.Count - 1].ToString();
                    relevant = last.Contains(resultAssignment) ? last : expressions[expressions.Count - 2].ToString();
                    break;
                }
        }

        if (relevant == null || !relevant.Contains(resultAssignment))
        {
            relevant = compiledExpression.ParsedExpression.ToString();
        }

        int indexToRemove = relevant.IndexOf(resultAssignment, StringComparison.Ordinal);
        if (indexToRemove >= 0)
        {
            relevant = relevant.Substring(indexToRemove + resultAssignment.Length);
        }

        return relevant;
    }
}
