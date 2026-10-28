// <copyright file="ProbeExpressionParser.Redaction.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Snapshots;

namespace Datadog.Trace.Debugger.Expressions;

// Runtime redaction guards. A guard is a bool check attached to an expression that is true when a value the
// expression depends on is redacted at runtime: a value under a sensitive dictionary key, or a value whose runtime type
// is redacted. An operation over a guarded expression yields the redacted default when the check is true, and a member
// or item read from it carries the check on. Compile-time redaction (RedactedValue) is handled separately.
internal partial class ProbeExpressionParser<T>
{
    // States of a memoized check: the first guard to run stores the result, the others read it.
    private const int CheckPending = 0;
    private const int CheckedNotRedacted = 1;
    private const int CheckedRedacted = 2;

    private static readonly MethodInfo IsRedactedRuntimeValueMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(ProbeExpressionParserHelper), nameof(ProbeExpressionParserHelper.IsRedactedRuntimeValue), [typeof(object), typeof(Type)]);
    private static readonly MethodInfo ShouldRedactDictionaryKeyMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(ShouldRedactDictionaryKey), [typeof(object)]);

    private Dictionary<Expression, RedactionGuard> _redactionGuards;
    private RedactionGuardScope _rootRedactionGuardScope;
    private RedactionGuardScope _predicateRedactionGuardScope;

    private enum RedactionGuardKind : byte
    {
        // A guarded value: an operation over it must check the guard.
        Value,

        // A dictionary entry guarded by its own key, whose Key member stays readable.
        DictionaryEntry,

        // The result of an operation that already yields the redacted default when the guard is true.
        RedactedOperation
    }

    private RedactionGuardScope RootRedactionGuardScope => _rootRedactionGuardScope ??= new RedactionGuardScope(parent: null);

    private RedactionGuardScope CurrentRedactionGuardScope => _predicateRedactionGuardScope ?? RootRedactionGuardScope;

    private static bool ShouldRedactDictionaryKey(object key)
    {
        var type = key?.GetType() ?? typeof(object);
        var name = key switch
        {
            string stringKey => stringKey,
            null => null,
            _ when Redaction.IsSafeToCallToString(type) => key.ToString(),
            _ => null
        };

        return Redaction.Instance.ShouldRedact(name, type, out _);
    }

    private static Expression RedactedDefault(Type type)
    {
        if (type == typeof(bool))
        {
            return Expression.Constant(false);
        }

        return type == typeof(string)
                   ? Expression.Constant(RedactedValueDump)
                   : Expression.Default(type);
    }

    private static MethodCallExpression RuntimeTypeCheck(Expression read, Type declaredType)
    {
        return Expression.Call(
            IsRedactedRuntimeValueMethod,
            declaredType == typeof(object) ? read : Expression.Convert(read, typeof(object)),
            Expression.Constant(declaredType, typeof(Type)));
    }

    private bool TryGetRedactionGuard(Expression expression, out RedactionGuard guard)
    {
        while (expression != null)
        {
            if (_redactionGuards != null && _redactionGuards.TryGetValue(expression, out guard))
            {
                return true;
            }

            if (expression is ParameterExpression parameter && TryGetParameterCheck(parameter, out var parameterCheck))
            {
                guard = new RedactionGuard(parameterCheck, parameter, RedactionGuardKind.Value);
                return true;
            }

            if (expression is not UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unaryExpression)
            {
                break;
            }

            expression = unaryExpression.Operand;
        }

        guard = default;
        return false;
    }

    // A parameter (@it, @exception, @return) is guarded by its scope. The scopes enclosing the predicate being parsed are
    // searched, so a predicate's guard is out of reach once its block ends.
    private bool TryGetParameterCheck(ParameterExpression parameter, out Expression check)
    {
        for (var scope = CurrentRedactionGuardScope; scope is not null; scope = scope.Parent)
        {
            if (scope.ParameterChecks is not null && scope.ParameterChecks.TryGetValue(parameter, out check))
            {
                return true;
            }
        }

        check = null;
        return false;
    }

    private void TrackRedactionGuard(Expression expression, Expression check, RedactionGuardKind kind = RedactionGuardKind.Value, Expression sourceCheck = null)
    {
        (_redactionGuards ??= [])[expression] = new RedactionGuard(check, expression, kind, sourceCheck);
    }

    private Expression CombineRedactionChecks(Expression check, Expression operand)
    {
        if (!TryGetRedactionGuard(operand, out var guard))
        {
            return check;
        }

        return check is null ? guard.Check : Expression.OrElse(check, guard.Check);
    }

    // A value read from guarded values without an operation, such as a member or an item, is only evaluated after their
    // checks, so it carries them on, before its own.
    private void DeriveRedactionGuard(Expression derived, Expression source, Expression other = null)
    {
        DeriveRedactionCheck(derived, CombineRedactionChecks(CombineRedactionChecks(null, source), other));
    }

    private void DeriveRedactionCheck(Expression derived, Expression sourceCheck)
    {
        if (sourceCheck is null)
        {
            return;
        }

        if (!TryGetRedactionGuard(derived, out var derivedGuard))
        {
            TrackRedactionGuard(derived, sourceCheck);
            return;
        }

        var check = Expression.OrElse(sourceCheck, derivedGuard.Check);
        if (derivedGuard.Kind != RedactionGuardKind.DictionaryEntry)
        {
            TrackRedactionGuard(derived, check);
            return;
        }

        // An entry keeps its key check apart, so its Key member, which that check doesn't redact, only carries the
        // checks of the values the entry was read from.
        var entrySourceCheck = derivedGuard.SourceCheck is null ? sourceCheck : Expression.OrElse(sourceCheck, derivedGuard.SourceCheck);
        TrackRedactionGuard(derived, check, RedactionGuardKind.DictionaryEntry, entrySourceCheck);
    }

    private Expression RedactOperation(Expression source, Expression operation)
    {
        return RedactOperationWithCheck(CombineRedactionChecks(null, source), operation);
    }

    private Expression RedactOperation(Expression first, Expression second, Expression operation)
    {
        return RedactOperationWithCheck(CombineRedactionChecks(CombineRedactionChecks(null, first), second), operation);
    }

    private Expression RedactUndefinedOperation(Expression source)
    {
        return RedactOperation(source, ReturnDefaultValueExpression());
    }

    private Expression RedactOperationWithCheck(Expression check, Expression operation)
    {
        if (check == null)
        {
            return operation;
        }

        var redactedOperation = Expression.Condition(check, RedactedDefault(operation.Type), operation);
        TrackRedactionGuard(redactedOperation, check, RedactionGuardKind.RedactedOperation);
        return redactedOperation;
    }

    // An and/or operand or a predicate that uses a guarded value is false when redacted. Only the operand is guarded,
    // not the and/or result, so checks stay behind the short circuit: and(x != null, x.y == 1) never reads x.y early.
    private Expression RedactBooleanOperand(Expression operand)
    {
        if (!TryGetRedactionGuard(operand, out var guard) || guard.YieldsRedactedDefault(operand))
        {
            return operand;
        }

        return RedactOperationWithCheck(guard.Check, operand);
    }

    private Expression RedactGuardedValueForReturn(RedactionGuard guard, Expression finalExpr, List<ParameterExpression> scopeMembers)
    {
        if (guard.YieldsRedactedDefault(finalExpr) &&
            finalExpr.Type == typeof(T) &&
            (typeof(T) == typeof(bool) || typeof(T) == typeof(string)))
        {
            return finalExpr;
        }

        var redactedValue = RedactedValue();
        if (typeof(T) == typeof(object))
        {
            return Expression.Condition(
                guard.Check,
                Expression.Convert(redactedValue, typeof(object)),
                Expression.Convert(finalExpr, typeof(object)));
        }

        if (typeof(T) == typeof(string))
        {
            var valueAsString = finalExpr.Type == typeof(string)
                                    ? finalExpr
                                    : DumpExpression(finalExpr, scopeMembers);
            return Expression.Condition(guard.Check, redactedValue, valueAsString);
        }

        if (typeof(T) == typeof(bool) && finalExpr.Type == typeof(bool))
        {
            return Expression.Condition(guard.Check, Expression.Constant(false), finalExpr);
        }

        if (typeof(T).IsNumeric() && TryConvertToNumericType<T>(finalExpr, out var numericResult))
        {
            return Expression.Condition(guard.Check, Expression.Constant(default(T), typeof(T)), numericResult);
        }

        return finalExpr;
    }

    private Expression GuardedValueMember(RedactionGuard guard, string propertyOrFieldValue)
    {
        var valueExpression = guard.Value;
        if (!TryResolveSafeMemberExpression(valueExpression, propertyOrFieldValue, out var memberExpression, out var reason))
        {
            AddError($"{valueExpression}.{propertyOrFieldValue}", reason);
            var undefinedValue = UndefinedValue();
            TrackRedactionGuard(undefinedValue, guard.Check);
            return undefinedValue;
        }

        var redactedMember = RedactIfRedactedType(memberExpression, valueExpression, propertyOrFieldValue);
        if (IsRedactedValue(redactedMember))
        {
            return redactedMember;
        }

        TrackRedactionGuard(memberExpression, guard.Check);
        return GuardRedactedRuntimeType(memberExpression, CurrentRedactionGuardScope);
    }

    // The value of a dictionary entry is redacted when its key is sensitive.
    private void GuardByDictionaryKey(Expression expression, Expression keyExpression, RedactionGuardKind kind = RedactionGuardKind.Value)
    {
        var check = Expression.Call(null, ShouldRedactDictionaryKeyMethod, Expression.Convert(keyExpression, typeof(object)));
        TrackRedactionGuard(expression, check, kind);
    }

    private Expression GuardDictionaryEntry(Expression dictionaryEntryExpression)
    {
        GuardByDictionaryKey(dictionaryEntryExpression, Expression.Property(dictionaryEntryExpression, nameof(KeyValuePair<int, int>.Key)), RedactionGuardKind.DictionaryEntry);
        return dictionaryEntryExpression;
    }

    private MemberExpression GuardDictionaryValue(Expression dictionaryEntryExpression)
    {
        var valueExpression = Expression.Property(dictionaryEntryExpression, nameof(KeyValuePair<int, int>.Value));
        GuardByDictionaryKey(valueExpression, Expression.Property(dictionaryEntryExpression, nameof(KeyValuePair<int, int>.Key)));
        return valueExpression;
    }

    // Compile-time check: exact for root scope members (compiled per runtime type), declared type for anything read from
    // them. GuardRedactedRuntimeType covers the runtime type of the latter.
    private Expression RedactIfRedactedType(Expression value, Expression source, string name)
    {
        if (!Redaction.Instance.IsRedactedType(value.Type))
        {
            return value;
        }

        AddRedactionError(source is null ? name : $"{source}.{name}");
        return RedactedValue();
    }

    private Expression RedactIfRedactedType(Expression value, Expression source, Expression indexOrKey)
    {
        if (!Redaction.Instance.IsRedactedType(value.Type))
        {
            return value;
        }

        AddRedactionError($"{source}[{indexOrKey}]");
        return RedactedValue();
    }

    // Redacts a value of a redacted declared type, and guards one whose declared type can hold a redacted runtime type.
    private Expression RedactByType(Expression value, Expression source, string name, RedactionGuardScope scope)
    {
        return GuardRedactedRuntimeType(RedactIfRedactedType(value, source, name), scope);
    }

    private Expression RedactByType(Expression value, Expression source, Expression indexOrKey, RedactionGuardScope scope)
    {
        return GuardRedactedRuntimeType(RedactIfRedactedType(value, source, indexOrKey), scope);
    }

    // A value read through a member, an index, an iterator, @exception or @return has only its declared type at compile
    // time. When that type can hold a redacted runtime type, the value is read once into a variable of the scope's block
    // and its runtime type is checked once per entry of that block. A root value (@exception, @return) belongs to the
    // root scope, so a predicate doesn't check it again on every item.
    private Expression GuardRedactedRuntimeType(Expression value, RedactionGuardScope scope)
    {
        if (!Redaction.Instance.CanHaveRedactedRuntimeType(value.Type))
        {
            return value;
        }

        if (value is ParameterExpression parameter)
        {
            // A parameter is only ever guarded by its own check, which is shared within its scope.
            if (scope.ParameterChecks is null || !scope.ParameterChecks.ContainsKey(parameter))
            {
                (scope.ParameterChecks ??= []).Add(parameter, MemoizedRedactionCheck(scope, RuntimeTypeCheck(parameter, parameter.Type)));
            }

            return parameter;
        }

        var hasParentGuard = TryGetRedactionGuard(value, out var parent);
        var variable = DeclareRedactionGuardVariable(scope, value.Type, "$dd_el_value");
        var check = MemoizedRedactionCheck(scope, RuntimeTypeCheck(Expression.Assign(variable, value), value.Type));
        TrackRedactionGuard(variable, hasParentGuard ? Expression.OrElse(parent.Check, check) : check);
        return variable;
    }

    private ParameterExpression DeclareRedactionGuardVariable(RedactionGuardScope scope, Type type, string name)
    {
        var variable = Expression.Variable(type, name);
        scope.Variables.Add(variable);
        return variable;
    }

    // Wraps a check so it runs once per entry of its block: consumers are nested, and and/or short-circuits, so which
    // guard runs first isn't known at compile time.
    private BinaryExpression MemoizedRedactionCheck(RedactionGuardScope scope, Expression check)
    {
        var state = DeclareRedactionGuardVariable(scope, typeof(int), ProbeExpressionParserHelper.RedactionCheckStateVariableName);
        scope.Resets.Add(Expression.Assign(state, Expression.Constant(CheckPending)));
        return Expression.Equal(
            Expression.Condition(
                Expression.Equal(state, Expression.Constant(CheckPending)),
                Expression.Assign(state, Expression.Condition(check, Expression.Constant(CheckedRedacted), Expression.Constant(CheckedNotRedacted))),
                state),
            Expression.Constant(CheckedRedacted));
    }

    // Values read from a parameter, such as @value, @key or @it.Name, are memoized per block, so repeated reads share
    // one guard. Values redacted at compile time are not, so each read still reports its redaction.
    private bool TryGetGuardedMember(ParameterExpression source, string member, out Expression value)
    {
        value = null;
        return CurrentRedactionGuardScope.Members?.TryGetValue(new GuardedMemberKey(source, member), out value) == true;
    }

    private Expression MemoizeGuardedMember(ParameterExpression source, string member, Expression value)
    {
        if (value is GotoExpression || IsRedactedValue(value) || value.Type == ProbeExpressionParserHelper.UndefinedValueType)
        {
            return value;
        }

        (CurrentRedactionGuardScope.Members ??= [])[new GuardedMemberKey(source, member)] = value;
        return value;
    }

    private RedactionGuardScope EnterPredicateRedactionGuardScope()
    {
        var outerScope = _predicateRedactionGuardScope;
        _predicateRedactionGuardScope = new RedactionGuardScope(CurrentRedactionGuardScope);
        return outerScope;
    }

    // Declares the predicate's guard variables in a block that resets their checks on every item: a block that a loop
    // re-enters keeps its variables' values.
    private Expression ScopePredicateRedactionGuards(Expression predicate)
    {
        var scope = _predicateRedactionGuardScope;
        if (predicate is null || scope.Variables.Count == 0)
        {
            return predicate;
        }

        List<Expression> expressions = [.. scope.Resets, predicate];
        return Expression.Block(scope.Variables, expressions);
    }

    private void ExitPredicateRedactionGuardScope(RedactionGuardScope outerScope)
    {
        _predicateRedactionGuardScope = outerScope;
    }

    private void AddRootRedactionGuards(List<ParameterExpression> scopeMembers, List<Expression> expressions)
    {
        if (_rootRedactionGuardScope is null)
        {
            return;
        }

        scopeMembers.AddRange(_rootRedactionGuardScope.Variables);
        expressions.AddRange(_rootRedactionGuardScope.Resets);
    }

    private readonly struct RedactionGuard(Expression check, Expression value, RedactionGuardKind kind, Expression sourceCheck = null)
    {
        // True when the value is redacted.
        internal Expression Check { get; } = check;

        internal Expression Value { get; } = value;

        internal RedactionGuardKind Kind { get; } = kind;

        // The part of Check that comes from the values this one was read from. An entry leaves out its own key check, and
        // an entry read from unguarded values has none.
        internal Expression SourceCheck { get; } = kind == RedactionGuardKind.DictionaryEntry ? sourceCheck : check;

        internal bool YieldsRedactedDefault(Expression expression)
        {
            return Kind == RedactionGuardKind.RedactedOperation && ReferenceEquals(Value, expression);
        }
    }

    private readonly record struct GuardedMemberKey(ParameterExpression Source, string Member);

    // The guard variables of one block (the root block or a predicate's) and the resets of their checks. A predicate's
    // scope is nested in the scope of the block that encloses it.
    private sealed class RedactionGuardScope(RedactionGuardScope parent)
    {
        internal RedactionGuardScope Parent { get; } = parent;

        internal List<ParameterExpression> Variables { get; } = [];

        internal List<Expression> Resets { get; } = [];

        internal Dictionary<ParameterExpression, Expression> ParameterChecks { get; set; }

        internal Dictionary<GuardedMemberKey, Expression> Members { get; set; }
    }
}
