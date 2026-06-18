// <copyright file="ProbeExpressionEvaluator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Debugger.Expressions;

internal sealed class ProbeExpressionEvaluator
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeExpressionEvaluator));
    private static readonly CompiledExpressionDelegate<bool> FalseDelegate = ReturnFalse;
    private static readonly CompiledExpressionDelegate<bool> TrueDelegate = ReturnTrue;

    // Hot-path cache:
    // - Single dictionary lookup per Evaluate()
    // - No per-call allocation for member-type keys (allocations occur only on miss)
    // - Per-bucket entries handle polymorphic member runtime types
    private readonly ConcurrentDictionary<ProbeExpressionsBucketKey, ProbeExpressionsBucket> _cache = new();

    internal ProbeExpressionEvaluator(
        DebuggerExpression?[]? templates,
        DebuggerExpression? condition,
        DebuggerExpression? metric,
        KeyValuePair<DebuggerExpression?, KeyValuePair<string?, DebuggerExpression?[]>[]>[]? spanDecorations,
        CaptureExpressionDefinition[]? captureExpressions,
        int maxEvaluationTimeInMilliseconds = Debugger.DebuggerSettings.DefaultMaxEvaluationTimeInMilliseconds)
    {
        Templates = templates;
        Condition = condition;
        Metric = metric;
        SpanDecorations = spanDecorations;
        CaptureExpressions = captureExpressions;
        MaxEvaluationTimeInMilliseconds = maxEvaluationTimeInMilliseconds;
    }

    /// <summary>
    /// Gets CompiledTemplates for the first cached type, for use in "DebuggerExpressionLanguageTests"
    /// </summary>
    internal CompiledExpression<string>[]? CompiledTemplates
    {
        get
        {
            foreach (var kvp in _cache)
            {
                if (kvp.Value.TryGetFirstEntry(out var entry))
                {
                    if (entry.TryGetCompiled(out var compiled))
                    {
                        return compiled.Templates;
                    }
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Gets CompiledCondition for the first cached type, for use in "DebuggerExpressionLanguageTests"
    /// </summary>
    internal CompiledExpression<bool>? CompiledCondition
    {
        get
        {
            foreach (var kvp in _cache)
            {
                if (kvp.Value.TryGetFirstEntry(out var entry))
                {
                    if (entry.TryGetCompiled(out var compiled))
                    {
                        return compiled.Condition;
                    }
                }
            }

            return null;
        }
    }

    internal CompiledExpression<double>? CompiledMetric
    {
        get
        {
            foreach (var kvp in _cache)
            {
                if (kvp.Value.TryGetFirstEntry(out var entry))
                {
                    if (entry.TryGetCompiled(out var compiled))
                    {
                        return compiled.Metric;
                    }
                }
            }

            return null;
        }
    }

    internal KeyValuePair<CompiledExpression<bool>, KeyValuePair<string?, CompiledExpression<string>[]>[]>[]? CompiledDecorations
    {
        get
        {
            foreach (var kvp in _cache)
            {
                if (kvp.Value.TryGetFirstEntry(out var entry))
                {
                    if (entry.TryGetCompiled(out var compiled))
                    {
                        return compiled.Decorations;
                    }
                }
            }

            return null;
        }
    }

    internal DebuggerExpression?[]? Templates { get; }

    internal DebuggerExpression? Condition { get; }

    internal DebuggerExpression? Metric { get; }

    internal KeyValuePair<DebuggerExpression?, KeyValuePair<string?, DebuggerExpression?[]>[]>[]? SpanDecorations { get; }

    internal CaptureExpressionDefinition[]? CaptureExpressions { get; }

    internal int MaxEvaluationTimeInMilliseconds { get; }

    private static bool ReturnFalse(ScopeMember invocationTarget, ScopeMember returnValue, ScopeMember duration, Exception exception, ScopeMember[] members, ref EvaluationBudget budget)
    {
        budget.ThrowIfExceeded();
        return false;
    }

    private static bool ReturnTrue(ScopeMember invocationTarget, ScopeMember returnValue, ScopeMember duration, Exception exception, ScopeMember[] members, ref EvaluationBudget budget)
    {
        budget.ThrowIfExceeded();
        return true;
    }

    internal ExpressionEvaluationResult Evaluate(MethodScopeMembers scopeMembers)
    {
        return Evaluate(scopeMembers, out _);
    }

    internal ExpressionEvaluationResult Evaluate(MethodScopeMembers scopeMembers, out ProbeExpressionsCacheEntry? entry)
    {
        if (Templates == null && Condition == null && Metric == null && SpanDecorations == null && CaptureExpressions == null)
        {
            entry = null;
            return default;
        }

        entry = GetCacheEntry(scopeMembers);
        var compiled = entry.GetOrCompile(this, scopeMembers);

        ExpressionEvaluationResult result = default;
        var budget = CreateBudget();
        result.HasEvaluationBudget = true;
        EvaluateTemplates(ref result, scopeMembers, compiled.Templates, ref budget);
        EvaluateCondition(ref result, scopeMembers, compiled.Condition, ref budget);
        EvaluateMetric(ref result, scopeMembers, compiled.Metric, ref budget);
        EvaluateSpanDecorations(ref result, scopeMembers, compiled.Decorations, ref budget);
        result.EvaluationBudget = budget;
        return result;
    }

    internal void EvaluateCaptureExpressions(ref ExpressionEvaluationResult result, MethodScopeMembers scopeMembers)
    {
        EvaluateCaptureExpressions(ref result, scopeMembers, entry: null);
    }

    internal void EvaluateCaptureExpressions(ref ExpressionEvaluationResult result, MethodScopeMembers scopeMembers, ProbeExpressionsCacheEntry? entry)
    {
        if (CaptureExpressions == null)
        {
            return;
        }

        entry ??= GetCacheEntry(scopeMembers);
        var compiledExpressions = entry.GetOrCompileCaptureExpressions(this, scopeMembers);
        var budget = result.HasEvaluationBudget ? result.EvaluationBudget : CreateBudget();
        result.HasEvaluationBudget = true;
        EvaluateCaptureExpressionsCore(ref result, scopeMembers, compiledExpressions, CaptureExpressions, ref budget);
        result.EvaluationBudget = budget;
    }

    private ProbeExpressionsCacheEntry GetCacheEntry(MethodScopeMembers scopeMembers)
    {
        // Use runtime types for caching/compilation safety (polymorphic calls can break casts if we compile for declared types).
        var invocationTarget = scopeMembers.InvocationTarget;
        var thisType = invocationTarget.Value?.GetType() ?? invocationTarget.Type ?? typeof(object);

        // Runtime return type matters for methods returning different concrete types.
        var returnValue = scopeMembers.Return;
        var returnRuntimeType = returnValue.Value?.GetType() ?? returnValue.Type;

        // Hot path: single dictionary lookup by (thisType, returnType, memberCount); bucket matches member runtime types without per-call allocations.
        var memberCount = scopeMembers.MemberCount;
        var bucketKey = new ProbeExpressionsBucketKey(thisType, returnRuntimeType, memberCount);
        var bucket = _cache.GetOrAdd(bucketKey, static _ => new ProbeExpressionsBucket(Log));
        return bucket.GetOrAdd(scopeMembers, memberCount);
    }

    private void EvaluateTemplates(ref ExpressionEvaluationResult result, MethodScopeMembers scopeMembers, CompiledExpression<string>[]? compiledExpressions, ref EvaluationBudget budget)
    {
        var resultBuilder = StringBuilderCache.Acquire();
        try
        {
            if (Templates == null)
            {
                return;
            }

            if (compiledExpressions == null)
            {
                return;
            }

            for (int i = 0; i < compiledExpressions.Length; i++)
            {
                try
                {
                    var template = Templates[i];
                    if (template == null)
                    {
                        continue;
                    }

                    if (IsLiteral(template) == true)
                    {
                        resultBuilder.Append(template.Value.Str);
                    }
                    else if (IsExpression(template) == true)
                    {
                        var compiledExpression = compiledExpressions[i];
                        if (compiledExpression.BudgetedDelegate is null)
                        {
                            continue;
                        }

                        resultBuilder.Append(compiledExpression.BudgetedDelegate(scopeMembers.InvocationTarget, scopeMembers.Return, scopeMembers.Duration, scopeMembers.Exception, scopeMembers.Members, ref budget));
                        if (compiledExpression.Errors is { } errors)
                        {
                            (result.Errors ??= new List<EvaluationError>()).AddRange(errors);
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

    private void EvaluateCondition(ref ExpressionEvaluationResult result, MethodScopeMembers scopeMembers, CompiledExpression<bool>? cached, ref EvaluationBudget budget)
    {
        if (Condition == null)
        {
            return;
        }

        CompiledExpression<bool> compiledExpression = default;
        try
        {
            if (!cached.HasValue)
            {
                return;
            }

            compiledExpression = cached.Value;
            if (compiledExpression.BudgetedDelegate is null)
            {
                return;
            }

            var condition = compiledExpression.BudgetedDelegate(scopeMembers.InvocationTarget, scopeMembers.Return, scopeMembers.Duration, scopeMembers.Exception, scopeMembers.Members, ref budget);
            result.Condition = condition;
            if (compiledExpression.Errors != null)
            {
                (result.Errors ??= new List<EvaluationError>()).AddRange(compiledExpression.Errors);
                result.HasConditionError = true;
            }
        }
        catch (Exception e)
        {
            HandleException(ref result, compiledExpression, e);
            result.Condition = true;
            result.HasConditionError = true;
        }
    }

    private void EvaluateMetric(ref ExpressionEvaluationResult result, MethodScopeMembers scopeMembers, CompiledExpression<double>? cached, ref EvaluationBudget budget)
    {
        if (Metric == null)
        {
            return;
        }

        CompiledExpression<double> compiledExpression = default;
        try
        {
            if (!cached.HasValue)
            {
                return;
            }

            compiledExpression = cached.Value;
            if (compiledExpression.BudgetedDelegate is null)
            {
                return;
            }

            var metric = compiledExpression.BudgetedDelegate(scopeMembers.InvocationTarget, scopeMembers.Return, scopeMembers.Duration, scopeMembers.Exception, scopeMembers.Members, ref budget);
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

    private void EvaluateSpanDecorations(ref ExpressionEvaluationResult result, MethodScopeMembers scopeMembers, KeyValuePair<CompiledExpression<bool>, KeyValuePair<string?, CompiledExpression<string>[]>[]>[]? compiledDecorations, ref EvaluationBudget budget)
    {
        if (SpanDecorations == null)
        {
            return;
        }

        List<ExpressionEvaluationResult.DecorationResult>? decorations = null;
        try
        {
            if (compiledDecorations == null)
            {
                Log.Debug($"{nameof(ProbeExpressionEvaluator)}.{nameof(EvaluateSpanDecorations)}: compiled decorations is null");
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
                        if (current.Key.BudgetedDelegate is null)
                        {
                            continue;
                        }

                        var when = current.Key.BudgetedDelegate(scopeMembers.InvocationTarget, scopeMembers.Return, scopeMembers.Duration, scopeMembers.Exception, scopeMembers.Members, ref budget);
                        if (current.Key.Errors is { } whenErrors)
                        {
                            if (Log.IsEnabled(LogEventLevel.Debug))
                            {
                                Log.Debug("{Class}.{Method}: Error when evaluating an expression. {Errors}", nameof(ProbeExpressionEvaluator), nameof(EvaluateSpanDecorations), string.Join(";", whenErrors));
                            }

                            (result.Errors ??= new List<EvaluationError>()).AddRange(whenErrors);
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

                List<EvaluationError>? errors = null;
                var resultBuilder = StringBuilderCache.Acquire();

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
                                    if (compiledExpression.BudgetedDelegate is null)
                                    {
                                        continue;
                                    }

                                    var value = compiledExpression.BudgetedDelegate(scopeMembers.InvocationTarget, scopeMembers.Return, scopeMembers.Duration, scopeMembers.Exception, scopeMembers.Members, ref budget);
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

                        (decorations ??= new List<ExpressionEvaluationResult.DecorationResult>()).Add(
                            new ExpressionEvaluationResult.DecorationResult { TagName = tagAndValues.Key, Value = resultBuilder.ToString(), Errors = errors?.ToArray() });
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

    private void EvaluateCaptureExpressionsCore(ref ExpressionEvaluationResult result, MethodScopeMembers scopeMembers, CompiledExpression<object>[]? compiledExpressions, CaptureExpressionDefinition[] captureExpressions, ref EvaluationBudget budget)
    {
        if (compiledExpressions == null)
        {
            return;
        }

        ExpressionEvaluationResult.CaptureExpressionResult[]? capturedValues = null;
        var capturedValuesCount = 0;
        for (int i = 0; i < compiledExpressions.Length; i++)
        {
            var compiledExpression = compiledExpressions[i];
            try
            {
                var captureExpression = captureExpressions[i];
                // CreateCaptureExpressions guarantees Name is a non-empty, non-null string.
                var name = captureExpression.Name;

                if (!IsExpression(compiledExpression))
                {
                    continue;
                }

                if (compiledExpression.BudgetedDelegate is null)
                {
                    continue;
                }

                var value = compiledExpression.BudgetedDelegate(scopeMembers.InvocationTarget, scopeMembers.Return, scopeMembers.Duration, scopeMembers.Exception, scopeMembers.Members, ref budget);
                if (value is UndefinedValue)
                {
                    if (compiledExpression.Errors != null)
                    {
                        (result.Errors ??= new List<EvaluationError>()).AddRange(compiledExpression.Errors);
                    }

                    continue;
                }

                // Runtime failures can leave slack in the array; CaptureExpressionCount is the authoritative length.
                (capturedValues ??= new ExpressionEvaluationResult.CaptureExpressionResult[compiledExpressions.Length])[capturedValuesCount++] =
                    new ExpressionEvaluationResult.CaptureExpressionResult(
                    name,
                    value,
                    value?.GetType() ?? typeof(object),
                    captureExpression.CaptureLimitInfo);

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

        result.CaptureExpressions = capturedValues;
        result.CaptureExpressionCount = capturedValuesCount;
    }

    private CompiledExpression<string>[]? CompileTemplates(MethodScopeMembers scopeMembers)
    {
        if (Templates == null)
        {
            return null;
        }

        var compiledExpressions = new CompiledExpression<string>[Templates.Length];
        for (int i = 0; i < Templates.Length; i++)
        {
            var current = Templates[i];
            if (current == null)
            {
                continue;
            }

            if (current.Value.Json != null)
            {
                compiledExpressions[i] = ProbeExpressionParser<string>.ParseExpression(current.Value.Json, scopeMembers);
            }
            else
            {
                compiledExpressions[i] = new CompiledExpression<string>(null, null, current.Value.Str, null);
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

    private KeyValuePair<CompiledExpression<bool>, KeyValuePair<string?, CompiledExpression<string>[]>[]>[]? CompileDecorations(MethodScopeMembers scopeMembers)
    {
        if (SpanDecorations == null)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("{Class}.{Method}: {SpanDecorations} is null", nameof(ProbeExpressionEvaluator), nameof(CompileDecorations), nameof(SpanDecorations));
            }

            return null;
        }

        var compiledExpressions = new KeyValuePair<CompiledExpression<bool>, KeyValuePair<string?, CompiledExpression<string>[]>[]>[SpanDecorations.Length];
        for (int i = 0; i < SpanDecorations.Length; i++)
        {
            var current = SpanDecorations[i];
            CompiledExpression<bool> when;
            if (IsLiteral(current.Key) == true)
            {
                when = new CompiledExpression<bool>(
                    FalseDelegate,
                    null,
                    null,
                    new EvaluationError[] { new() { Expression = null, Message = "'when' should be a boolean expression, not a literal" } });
            }
            else
            {
                when = current.Key == null // span decoration doesn't must have a condition
                           ? new CompiledExpression<bool>(TrueDelegate, null, null, null)
                           : ProbeExpressionParser<bool>.ParseExpression(current.Key.Value.Json, scopeMembers);
            }

            var compiledTagsJ = new KeyValuePair<string?, CompiledExpression<string>[]>[current.Value.Length];
            for (int j = 0; j < current.Value.Length; j++)
            {
                var tagName = current.Value[j].Key;
                var compiledTagsK = new CompiledExpression<string>[current.Value[j].Value.Length];
                for (int k = 0; k < current.Value[j].Value.Length; k++)
                {
                    var tag = current.Value[j].Value[k];
                    if (tag == null)
                    {
                        continue;
                    }

                    if (tag.Value.Json != null)
                    {
                        compiledTagsK[k] = ProbeExpressionParser<string>.ParseExpression(tag.Value.Json, scopeMembers);
                    }
                    else
                    {
                        compiledTagsK[k] = new CompiledExpression<string>(null, null, tag.Value.Str, null);
                    }
                }

                compiledTagsJ[j] = new KeyValuePair<string?, CompiledExpression<string>[]>(tagName, compiledTagsK);
            }

            compiledExpressions[i] = new KeyValuePair<CompiledExpression<bool>, KeyValuePair<string?, CompiledExpression<string>[]>[]>(when, compiledTagsJ);
        }

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("{Class}.{Method}: Finished to compile span decorations", nameof(ProbeExpressionEvaluator), nameof(CompileDecorations));
        }

        return compiledExpressions;
    }

    internal CompiledExpression<object>[]? CompileCaptureExpressions(MethodScopeMembers scopeMembers)
    {
        if (CaptureExpressions == null)
        {
            return null;
        }

        // Keep this array index-aligned with CaptureExpressions; evaluation uses the same index to read
        // the capture name and limits for each compiled delegate.
        var compiledExpressions = new CompiledExpression<object>[CaptureExpressions.Length];
        for (int i = 0; i < CaptureExpressions.Length; i++)
        {
            var expression = CaptureExpressions[i].Expression;
            if (expression?.Json != null && IsExpression(expression) == true)
            {
                compiledExpressions[i] = ProbeExpressionParser<object>.ParseCaptureExpression(expression.Value.Json, scopeMembers, CaptureExpressions[i].CaptureLimitInfo);
            }
        }

        return compiledExpressions;
    }

    private bool? IsLiteral(DebuggerExpression? expression)
    {
        if (expression is null)
        {
            return null;
        }

        return StringUtil.IsNullOrEmpty(expression.Value.Json);
    }

    private bool IsLiteral<T>(CompiledExpression<T> expression)
    {
        return expression.BudgetedDelegate == null && expression.ParsedExpression == null && expression.Errors == null && expression.RawExpression != null;
    }

    private bool? IsExpression(DebuggerExpression? expression)
    {
        if (expression is null)
        {
            return null;
        }

        return !StringUtil.IsNullOrEmpty(expression.Value.Json) && StringUtil.IsNullOrEmpty(expression.Value.Str);
    }

    private bool IsExpression<T>(CompiledExpression<T> expression)
    {
        return expression.BudgetedDelegate != null && expression.ParsedExpression != null && expression.RawExpression != null;
    }

    private void HandleException<T>(ref ExpressionEvaluationResult result, CompiledExpression<T> compiledExpression, Exception e)
    {
        result.Errors ??= new List<EvaluationError>();
        if (compiledExpression.Errors != null)
        {
            result.Errors.AddRange(compiledExpression.Errors);
        }

        result.Errors.Add(new EvaluationError { Expression = GetRelevantExpression(compiledExpression), Message = e is EvaluationTimeBudgetExceededException ? EvaluationTimeBudgetExceededException.ErrorMessage : e.Message });
    }

    private EvaluationBudget CreateBudget()
    {
        return EvaluationBudget.Create(MaxEvaluationTimeInMilliseconds);
    }

    private string GetRelevantExpression<T>(CompiledExpression<T> compiledExpression)
    {
        const string resultAssignment = "$dd_el_result = ";
        string? relevant = null;
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

    internal CompiledProbeExpressions CompileAll(MethodScopeMembers scopeMembers)
    {
        return new CompiledProbeExpressions(
            CompileTemplates(scopeMembers),
            CompileCondition(scopeMembers),
            CompileMetric(scopeMembers),
            CompileDecorations(scopeMembers));
    }
}
