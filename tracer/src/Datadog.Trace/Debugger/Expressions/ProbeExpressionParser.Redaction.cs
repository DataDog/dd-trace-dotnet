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
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Debugger.Expressions;

// Runtime redaction guards. A guard is a bool check attached to an expression that is true when a value the
// expression depends on is redacted at runtime: a value under a sensitive dictionary key, or a value whose runtime type
// is redacted. An operation over a guarded expression yields the redacted default when the check is true, and a member
// or item read from it carries the check on. Every check runs once per entry of its block: the block of the innermost
// predicate whose item the value is read from, or the root block for a value read from outside every item. A check of the
// root block that redacts sets a flag on the evaluation budget that the evaluator reports. One of a predicate's block
// makes that item not match without reporting it, and marks the budget so the evaluator can log the skip once per probe.
// Compile-time redaction (RedactedValue) is handled separately.
internal partial class ProbeExpressionParser<T>
{
    // States of a memoized check: the first guard to run stores the result, the others read it.
    private const int CheckPending = 0;
    private const int CheckedNotRedacted = 1;
    private const int CheckedRedacted = 2;

    private static readonly MethodInfo IsRedactedRuntimeValueMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(ProbeExpressionParserHelper), nameof(ProbeExpressionParserHelper.IsRedactedRuntimeValue), [typeof(object), typeof(Type)]);
    private static readonly MethodInfo MarkRuntimeRedactionMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(EvaluationBudget), nameof(EvaluationBudget.MarkRuntimeRedaction), [typeof(EvaluationBudget).MakeByRefType()]);
    private static readonly MethodInfo MarkSkippedItemMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(EvaluationBudget), nameof(EvaluationBudget.MarkSkippedItem), [typeof(EvaluationBudget).MakeByRefType()]);
    private static readonly MethodInfo IsRedactedStringKeyMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(IsRedactedStringKey), [typeof(string)]);
    private static readonly MethodInfo IsRedactedObjectKeyMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(IsRedactedObjectKey), [typeof(object)]);
    private static readonly MethodInfo IsRedactedEntryKeyMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(IsRedactedEntryKey), [typeof(object)]);

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

    // How a dictionary key that isn't a compile-time constant is checked at runtime, decided from its declared type.
    private enum IndexKeyCheck : byte
    {
        // No value of the type can be a redacted key.
        None,

        // A string, looked up in the redacted keywords.
        Keyword,

        // An enum compared with its members whose names are redacted keywords.
        EnumValue,

        // Any other key, checked by its runtime value.
        ObjectKey
    }

    private RedactionGuardScope RootRedactionGuardScope => _rootRedactionGuardScope ??= new RedactionGuardScope(parent: null, item: null, isDropped: false);

    private RedactionGuardScope CurrentRedactionGuardScope => _predicateRedactionGuardScope ?? RootRedactionGuardScope;

    // The string type itself is checked at compile time.
    [TestingAndPrivateOnly]
    internal static bool IsRedactedStringKey(string key)
    {
        return Redaction.Instance.IsRedactedKeyword(key);
    }

    // An index key whose declared type doesn't decide its check, matched by name. Its runtime type isn't checked: an index
    // key that can hold a redacted runtime type carries its own guard.
    [TestingAndPrivateOnly]
    internal static bool IsRedactedObjectKey(object key)
    {
        if (key is string name)
        {
            return Redaction.Instance.IsRedactedKeyword(name);
        }

        if (key is null)
        {
            return false;
        }

        if (key is Enum)
        {
            return Redaction.Instance.IsRedactedKeyword(key.ToString());
        }

        var type = key.GetType();
        return !IsUncheckedKeyType(type) &&
               Redaction.IsSafeToCallToString(type) &&
               Redaction.Instance.IsRedactedKeyword(key.ToString());
    }

    // The key of an iterated entry whose declared type doesn't decide its check. Nothing guards the key before the entry's
    // value is read, so a key of a redacted runtime type redacts the value too.
    [TestingAndPrivateOnly]
    internal static bool IsRedactedEntryKey(object key)
    {
        return key is not null &&
               (Redaction.Instance.IsRedactedRuntimeType(key.GetType()) || IsRedactedObjectKey(key));
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

    // Keys of these types are not checked: no default keyword is a number, a bool, a single character, a Guid, a date, a
    // time span or a version. A configured keyword of that shape is not matched by runtime index keys or iteration keys.
    private static bool IsUncheckedKeyType(Type type)
    {
        return type.IsPrimitive ||
               type == typeof(decimal) ||
               type == typeof(Guid) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Version);
    }

    // Whether a value of the declared type can be a key that IsRedactedObjectKey matches by name: a string, an enum, or
    // a checked type that is safe to ToString.
    private static bool CanBeNamedKey(Type declaredType)
    {
        if (declaredType.IsAssignableFrom(typeof(string)) || declaredType.IsAssignableFrom(typeof(Enum)))
        {
            return true;
        }

        foreach (var type in Redaction.AllowedTypesSafeToCallToString)
        {
            if (!IsUncheckedKeyType(type) && declaredType.IsAssignableFrom(type))
            {
                return true;
            }
        }

        return false;
    }

    // A key that can't be matched by name, such as a customer class, is redacted only by its type: the declared type is
    // checked at compile time, and the runtime type by the key's own guard or, for an iterated key, by the entry's guard.
    private static IndexKeyCheck GetIndexKeyCheck(Type keyType, out List<object> redactedEnumValues)
    {
        redactedEnumValues = null;
        var type = Nullable.GetUnderlyingType(keyType) ?? keyType;
        if (type == typeof(string))
        {
            return IndexKeyCheck.Keyword;
        }

        if (type.IsEnum)
        {
            // ToString yields a single name only for a value equal to a member: a [Flags] combination ("A, B") or a
            // number never matches a keyword. A value with several names is redacted when any of them is a keyword.
            var names = Enum.GetNames(type);
            var values = Enum.GetValues(type);
            for (var i = 0; i < names.Length; i++)
            {
                var value = values.GetValue(i);
                if (Redaction.Instance.IsRedactedKeyword(names[i]) && redactedEnumValues?.Contains(value) != true)
                {
                    (redactedEnumValues ??= []).Add(value);
                }
            }

            return redactedEnumValues is null ? IndexKeyCheck.None : IndexKeyCheck.EnumValue;
        }

        if (IsUncheckedKeyType(type))
        {
            return IndexKeyCheck.None;
        }

        return CanBeNamedKey(type) ? IndexKeyCheck.ObjectKey : IndexKeyCheck.None;
    }

    // Reads the key once per redacted member.
    private static Expression RedactedEnumValueCheck(Expression key, List<object> redactedValues)
    {
        Expression check = null;
        foreach (var value in redactedValues)
        {
            var isValue = Expression.Equal(key, Expression.Constant(value, key.Type));
            check = check is null ? isValue : Expression.OrElse(check, isValue);
        }

        return check;
    }

    private static Expression CheckKey(Expression key, IndexKeyCheck kind, List<object> redactedEnumValues)
    {
        return kind switch
        {
            IndexKeyCheck.Keyword => Expression.Call(IsRedactedStringKeyMethod, key),
            IndexKeyCheck.EnumValue => RedactedEnumValueCheck(key, redactedEnumValues),
            _ => Expression.Call(IsRedactedObjectKeyMethod, AsObject(key))
        };
    }

    private static Expression AsObject(Expression expression)
    {
        return expression.Type == typeof(object) ? expression : Expression.Convert(expression, typeof(object));
    }

    private static Expression RedactedDefault(Type type)
    {
        if (type == typeof(bool))
        {
            return Expression.Constant(false);
        }

        return type == typeof(string)
                   ? Expression.Constant(ProbeExpressionParserHelper.RedactedPlaceholder)
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
    private void DeriveRedactionGuard(Expression derived, Expression source, Expression other, Expression ownCheck)
    {
        var sourceCheck = CombineRedactionChecks(CombineRedactionChecks(null, source), other);
        if (ownCheck is not null)
        {
            sourceCheck = sourceCheck is null ? ownCheck : Expression.OrElse(sourceCheck, ownCheck);
        }

        DeriveRedactionCheck(derived, sourceCheck);
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
        return GuardRedactedRuntimeType(memberExpression);
    }

    // The value of a dictionary entry is redacted when its key is sensitive. The key is checked as an index key of its
    // declared type, and also by its runtime type, since nothing guards an iterated key before the value is read. A key
    // of a redacted declared type redacts every value, which is known at compile time, so it is reported like other
    // compile-time redactions, inside a predicate too.
    private void GuardByDictionaryKey(Expression expression, Expression dictionaryEntryExpression, RedactionGuardKind kind)
    {
        var key = Expression.Property(dictionaryEntryExpression, nameof(KeyValuePair<int, int>.Key));
        if (Redaction.Instance.IsRedactedType(key.Type))
        {
            AddRedactionError($"{dictionaryEntryExpression}.{nameof(KeyValuePair<int, int>.Key)}");
            TrackRedactionGuard(expression, Expression.Constant(true), kind);
            return;
        }

        var keyKind = GetIndexKeyCheck(key.Type, out var redactedEnumValues);
        if (keyKind == IndexKeyCheck.None && !Redaction.Instance.CanHaveRedactedRuntimeType(key.Type))
        {
            return;
        }

        var scope = GetRedactionGuardScope(dictionaryEntryExpression);
        Expression keyCheck;
        if (keyKind == IndexKeyCheck.None)
        {
            keyCheck = RuntimeTypeCheck(key, key.Type);
        }
        else
        {
            keyCheck = keyKind switch
            {
                IndexKeyCheck.ObjectKey => Expression.Call(IsRedactedEntryKeyMethod, AsObject(key)),

                // An enum key compared with several members is read once.
                IndexKeyCheck.EnumValue when redactedEnumValues.Count > 1 => CheckKeyVariable(scope, key, keyKind, redactedEnumValues, out _),
                _ => CheckKey(key, keyKind, redactedEnumValues)
            };
        }

        TrackRedactionGuard(expression, MemoizedRedactionCheck(scope, keyCheck), kind);
    }

    // The key of an index lookup that isn't a compile-time constant: the check reads it once into the variable that the
    // lookup reads, so the caller guards the lookup with the check, after the key's own guard. Returns the key for the
    // lookup, and a null check when no value of the key type can be a redacted key.
    private Expression CheckIndexKey(Expression key, out Expression check)
    {
        var kind = GetIndexKeyCheck(key.Type, out var redactedEnumValues);
        if (kind == IndexKeyCheck.None)
        {
            check = null;
            return key;
        }

        var scope = GetRedactionGuardScope(key);
        check = MemoizedRedactionCheck(scope, CheckKeyVariable(scope, key, kind, redactedEnumValues, out var keyValue));
        return keyValue;
    }

    // Checks a key read once into a variable, unless it's a parameter already.
    private Expression CheckKeyVariable(RedactionGuardScope scope, Expression key, IndexKeyCheck kind, List<object> redactedEnumValues, out ParameterExpression keyValue)
    {
        if (key is ParameterExpression parameter)
        {
            keyValue = parameter;
            return CheckKey(parameter, kind, redactedEnumValues);
        }

        keyValue = DeclareRedactionGuardVariable(scope, key.Type, "$dd_el_key");
        return Expression.Block(Expression.Assign(keyValue, key), CheckKey(keyValue, kind, redactedEnumValues));
    }

    private Expression GuardDictionaryEntry(Expression dictionaryEntryExpression)
    {
        GuardByDictionaryKey(dictionaryEntryExpression, dictionaryEntryExpression, RedactionGuardKind.DictionaryEntry);
        return dictionaryEntryExpression;
    }

    private MemberExpression GuardDictionaryValue(Expression dictionaryEntryExpression)
    {
        var valueExpression = Expression.Property(dictionaryEntryExpression, nameof(KeyValuePair<int, int>.Value));
        GuardByDictionaryKey(valueExpression, dictionaryEntryExpression, RedactionGuardKind.Value);
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
    private Expression RedactByType(Expression value, Expression source, string name)
    {
        return GuardRedactedRuntimeType(RedactIfRedactedType(value, source, name));
    }

    private Expression RedactByType(Expression value, Expression source, Expression indexOrKey)
    {
        return GuardRedactedRuntimeType(RedactIfRedactedType(value, source, indexOrKey));
    }

    // A value read through a member, an index, an iterator, @exception or @return has only its declared type at compile
    // time. When that type can hold a redacted runtime type, the value is read once into a variable of its scope's block
    // and its runtime type is checked once per entry of that block.
    private Expression GuardRedactedRuntimeType(Expression value)
    {
        if (!Redaction.Instance.CanHaveRedactedRuntimeType(value.Type))
        {
            return value;
        }

        var scope = GetRedactionGuardScope(value);
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

    // The scope of the guard of a value: the innermost predicate whose item the value is read from. A value read from
    // outside every item, such as this.Inner or headers[this.Name], is the same for every item, so the root block checks
    // it once per evaluation and reports it. A dropped predicate keeps all its guards.
    private RedactionGuardScope GetRedactionGuardScope(Expression value)
    {
        var scope = CurrentRedactionGuardScope;
        if (scope.Parent is null)
        {
            return scope;
        }

        var reads = new ParameterReadCollector();
        reads.Visit(value);
        for (; scope.Parent is not null; scope = scope.Parent)
        {
            if (scope.IsDropped || scope.IsReadFromItem(reads.Parameters))
            {
                return scope;
            }
        }

        return scope;
    }

    // Wraps a check so it runs once per entry of its block: consumers are nested, and and/or short-circuits, so which
    // guard runs first isn't known at compile time. A redaction in the root block is reported through the evaluation
    // budget. One in a predicate only makes that item not match, so any skips it, filter drops it and all is false. It
    // marks the budget only so the evaluator can log the skip, and the store runs only when the item is redacted.
    private BinaryExpression MemoizedRedactionCheck(RedactionGuardScope scope, Expression check)
    {
        var state = DeclareRedactionGuardVariable(scope, typeof(int), ProbeExpressionParserHelper.RedactionCheckStateVariableName);
        scope.Resets.Add(Expression.Assign(state, Expression.Constant(CheckPending)));
        var redacted = Expression.Block(
            Expression.Call(scope.Parent is null ? MarkRuntimeRedactionMethod : MarkSkippedItemMethod, _evaluationBudgetParameterExpression),
            Expression.Constant(CheckedRedacted));

        return Expression.Equal(
            Expression.Condition(
                Expression.Equal(state, Expression.Constant(CheckPending)),
                Expression.Assign(state, Expression.Condition(check, redacted, Expression.Constant(CheckedNotRedacted))),
                state),
            Expression.Constant(CheckedRedacted));
    }

    // Values read from a parameter, such as @value, @key, @it.Name or this.Inner, are memoized in the block of their
    // guards, so repeated reads share one guard. Values redacted at compile time are not, so each read still reports its
    // redaction.
    private bool TryGetGuardedMember(ParameterExpression source, string member, out Expression value)
    {
        value = null;
        return GetRedactionGuardScope(source).Members?.TryGetValue(new GuardedMemberKey(source, member), out value) == true;
    }

    private Expression MemoizeGuardedMember(ParameterExpression source, string member, Expression value)
    {
        if (value is GotoExpression || IsRedactedValue(value) || value.Type == ProbeExpressionParserHelper.UndefinedValueType)
        {
            return value;
        }

        (GetRedactionGuardScope(source).Members ??= [])[new GuardedMemberKey(source, member)] = value;
        return value;
    }

    private RedactionGuardScope EnterPredicateRedactionGuardScope(ParameterExpression item, bool isDropped)
    {
        var outerScope = _predicateRedactionGuardScope;
        _predicateRedactionGuardScope = new RedactionGuardScope(CurrentRedactionGuardScope, item, isDropped);
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
    private sealed class RedactionGuardScope(RedactionGuardScope parent, ParameterExpression item, bool isDropped)
    {
        internal RedactionGuardScope Parent { get; } = parent;

        // The predicate's item (@it), null for the root block and for a dropped predicate.
        internal ParameterExpression Item { get; } = item;

        // Whether the predicate is parsed only to be dropped, so its guards stay in its own block and none is left in the
        // blocks around it.
        internal bool IsDropped { get; } = isDropped;

        internal List<ParameterExpression> Variables { get; } = [];

        internal List<Expression> Resets { get; } = [];

        internal Dictionary<ParameterExpression, Expression> ParameterChecks { get; set; }

        internal Dictionary<GuardedMemberKey, Expression> Members { get; set; }

        // Whether an expression that reads these parameters reads the predicate's item, itself or through a variable of
        // this block, which holds a value read from it.
        internal bool IsReadFromItem(HashSet<ParameterExpression> parameters)
        {
            if (parameters.Count == 0)
            {
                return false;
            }

            if (Item is not null && parameters.Contains(Item))
            {
                return true;
            }

            foreach (var variable in Variables)
            {
                if (parameters.Contains(variable))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private sealed class ParameterReadCollector : ExpressionVisitor
    {
        internal HashSet<ParameterExpression> Parameters { get; } = [];

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Parameters.Add(node);
            return node;
        }
    }
}
