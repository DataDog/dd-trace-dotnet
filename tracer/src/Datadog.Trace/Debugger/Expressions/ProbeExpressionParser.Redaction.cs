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
// expression depends on is redacted at runtime, such as a value under a sensitive dictionary key. An operation over a
// guarded expression yields the redacted default when the check is true, and a member or item read from it carries the
// check on. Compile-time redaction (RedactedValue) is handled separately.
internal partial class ProbeExpressionParser<T>
{
    private static readonly MethodInfo ShouldRedactDictionaryKeyMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(ShouldRedactDictionaryKey), [typeof(object)]);

    private Dictionary<Expression, RedactionGuard> _redactionGuards;

    private enum RedactionGuardKind : byte
    {
        // A guarded value: an operation over it must check the guard.
        Value,

        // A dictionary entry guarded by its own key, whose Key member stays readable.
        DictionaryEntry,

        // The result of an operation that already yields the redacted default when the guard is true.
        RedactedOperation
    }

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

    private bool TryGetRedactionGuard(Expression expression, out RedactionGuard guard)
    {
        while (expression != null)
        {
            if (_redactionGuards != null && _redactionGuards.TryGetValue(expression, out guard))
            {
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
        return memberExpression;
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
}
