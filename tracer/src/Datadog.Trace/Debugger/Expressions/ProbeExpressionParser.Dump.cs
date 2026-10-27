// <copyright file="ProbeExpressionParser.Dump.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Util;
using static Datadog.Trace.Debugger.Expressions.ProbeExpressionParserHelper;

namespace Datadog.Trace.Debugger.Expressions;

internal partial class ProbeExpressionParser<T>
{
    private const string UndefinedValueDump = nameof(Expressions.UndefinedValue);
    private const string RedactedValueDump = "{redacted}";
    private const string BackingFieldSuffix = ">k__BackingField";

    // A type in CoreLib is never customer code, and a subclass of a type outside CoreLib is never in CoreLib.
    private static readonly Assembly CoreLibAssembly = typeof(object).Assembly;

    // CoreLib exceptions whose Message formats values that can be customer objects: ReflectionTypeLoadException reads
    // each loader exception's Message, and SwitchExpressionException formats the unmatched value. .NET Framework has no
    // SwitchExpressionException, so it's looked up by name and can be null.
    private static readonly Type ReflectionTypeLoadExceptionType = typeof(ReflectionTypeLoadException);
    private static readonly Type SwitchExpressionExceptionType = CoreLibAssembly.GetType("System.Runtime.CompilerServices.SwitchExpressionException");

    // Read as a field, so a Message override never runs.
    private static readonly FieldInfo ExceptionMessageField = typeof(Exception).GetField("_message", BindingFlags.Instance | BindingFlags.NonPublic);

    private static FieldInfo[] GetSafeFieldsForDump(FieldInfo[] fields)
    {
        var writeIndex = 0;
        for (var readIndex = 0; readIndex < fields.Length; readIndex++)
        {
            var field = fields[readIndex];
            if (!field.IsStatic || field.IsLiteral || StaticMemberSafety.CanReadStaticMember(field))
            {
                fields[writeIndex++] = field;
            }
        }

        if (writeIndex == fields.Length)
        {
            return fields;
        }

        var safeFields = new FieldInfo[writeIndex];
        Array.Copy(fields, safeFields, writeIndex);
        return safeFields;
    }

    private static object GetFieldValueForDump(FieldInfo field, object source)
    {
        return field.IsLiteral ? StaticMemberSafety.GetRawConstantValue(field) : field.GetValue(field.IsStatic ? null : source);
    }

    private static bool ShouldRedactDumpValue(string name, Type type)
    {
        if (Redaction.Instance.ShouldRedact(name, type, out _))
        {
            return true;
        }

        var autoPropertyName = GetAutoPropertyOrFieldName(name);
        return !ReferenceEquals(name, autoPropertyName) &&
               Redaction.Instance.IsRedactedKeyword(autoPropertyName);
    }

    private static string GetAutoPropertyOrFieldName(string fieldName)
    {
        if (StringUtil.IsNullOrEmpty(fieldName) || fieldName[0] != '<')
        {
            return fieldName;
        }

        var propertyNameLength = fieldName.Length - BackingFieldSuffix.Length - 1;
        if (propertyNameLength <= 0 ||
            !fieldName.EndsWith(BackingFieldSuffix, StringComparison.Ordinal))
        {
            return fieldName;
        }

        return fieldName.Substring(1, propertyNameLength);
    }

    // IDictionary<,> and IReadOnlyDictionary<,> don't implement IDictionary, so IsTypeSupportIndex doesn't see them.
    private static bool IsGenericDictionaryInterface(Type type)
    {
        if (!type.IsInterface || !type.IsGenericType)
        {
            return false;
        }

        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>);
    }

    // The declared type was already checked, so only a different runtime type needs another lookup.
    private static bool IsRedactedRuntimeValue(object value, Type declaredType)
    {
        if (value is null)
        {
            return false;
        }

        var runtimeType = value.GetType();
        return runtimeType != declaredType && ShouldRedactDumpValue(null, runtimeType);
    }

    // Only called when the declared type is safe to call ToString on. A value type or an exact match is
    // decided by the declared type. No subclass of a non-sealed allowed type is allowed itself, so any
    // other runtime type is a subclass whose override must not run.
    private static bool CanCallToStringOnRuntimeType(object value, Type declaredType)
    {
        return value.GetType() == declaredType || declaredType.IsValueType;
    }

    // Message and StackTrace are virtual, so they are only called on a CoreLib exception whose Message doesn't
    // format customer values.
    private static bool CanCallExceptionMembers(Type exceptionType)
    {
        return ReferenceEquals(exceptionType.Assembly, CoreLibAssembly) &&
               !ReferenceEquals(exceptionType, ReflectionTypeLoadExceptionType) &&
               !ReferenceEquals(exceptionType, SwitchExpressionExceptionType);
    }

    private static string DumpException(Exception exception, string prefix)
    {
        var runtimeType = exception.GetType();
        return CanCallExceptionMembers(runtimeType)
                   ? $"{prefix}{runtimeType.FullName}, {exception.Message}, {exception.StackTrace}"
                   : DumpExceptionMessageField(exception, prefix);
    }

    // Shows the runtime type and the message the exception was constructed with, without calling any member.
    private static string DumpExceptionMessageField(Exception exception, string prefix)
    {
        var typeName = exception.GetType().FullName;
        return ExceptionMessageField?.GetValue(exception) is string message
                   ? $"{prefix}{typeName}, {message}"
                   : $"{prefix}{typeName}";
    }

    private static Expression IsNullExpression(Expression expression)
    {
        // Expression.Equal could bind to a customer equality operator
        if (!expression.Type.IsValueType)
        {
            return Expression.ReferenceEqual(expression, Expression.Constant(null));
        }

        return Nullable.GetUnderlyingType(expression.Type) is null
                   ? Expression.Constant(false)
                   : Expression.Not(Expression.Property(expression, nameof(Nullable<int>.HasValue)));
    }

    private Expression DumpExpression(Expression expression, List<ParameterExpression> scopeMembers)
    {
        var type = expression.Type;
        if (ShouldRedactDumpValue(null, type))
        {
            return Expression.Constant(RedactedValueDump);
        }

        if (type == ProbeExpressionParserHelper.UndefinedValueType)
        {
            return Expression.Constant(UndefinedValueDump);
        }

        // A sealed or value type is its own runtime type, so its dump is decided here and needs no runtime check.
        var isExactType = type.IsValueType || type.IsSealed;
        var getTypeMethod = GetMethodByReflection(typeof(object), nameof(object.GetType), Type.EmptyTypes);
        var toStringMethod = GetMethodByReflection(typeof(object), nameof(object.ToString), Type.EmptyTypes);

        if (Datadog.Trace.Debugger.Helpers.TypeExtensions.IsSimple(type) ||
            Redaction.AllowedTypesSafeToCallToString.Contains(type))
        {
            if (isExactType)
            {
                return Expression.Call(expression, toStringMethod);
            }

            // No subclass of a non-sealed allowed type is allowed itself, so only an exact match may run ToString.
            return DumpRuntimeValueExpression(
                expression,
                Expression.Constant("null"),
                value => DumpIfRuntimeTypeAllows(
                    Expression.ReferenceEqual(Expression.Call(value, getTypeMethod), Expression.Constant(type, typeof(Type))),
                    Expression.Call(value, toStringMethod),
                    value,
                    scopeMembers),
                scopeMembers);
        }

        if (typeof(Exception).IsAssignableFrom(type))
        {
            var typeNameExpression = Expression.Constant(type.FullName, typeof(string));

            // Message and StackTrace are virtual, so DumpException calls them only when CanCallExceptionMembers allows it.
            // A type outside CoreLib has no subclass in CoreLib, so that check only runs for a non-sealed CoreLib type.
            if (isExactType && CanCallExceptionMembers(type))
            {
                var stringConcat = GetMethodByReflection(typeof(string), nameof(string.Concat), new[] { typeof(object[]) });
                var exceptionAsString = Expression.Call(
                   stringConcat,
                   Expression.NewArrayInit(
                       typeof(string),
                       typeNameExpression,
                       Expression.Constant(", "),
                       Expression.Property(expression, nameof(Exception.Message)),
                       Expression.Constant(", "),
                       Expression.Property(expression, nameof(Exception.StackTrace))));

                return Expression.Condition(IsNullExpression(expression), typeNameExpression, exceptionAsString);
            }

            var dumpExceptionMethod = GetMethodByReflection(
                typeof(ProbeExpressionParser<T>),
                !isExactType && ReferenceEquals(type.Assembly, CoreLibAssembly) ? nameof(DumpException) : nameof(DumpExceptionMessageField),
                [typeof(Exception), typeof(string)]);
            Func<Expression, Expression> dumpException = exception => Expression.Call(
                dumpExceptionMethod,
                Expression.Convert(exception, typeof(Exception)),
                Expression.Constant(null, typeof(string)));

            return isExactType
                       ? Expression.Condition(IsNullExpression(expression), typeNameExpression, dumpException(expression))
                       : DumpRuntimeValueExpression(expression, typeNameExpression, dumpException, scopeMembers);
        }

        if (IsDictionaryDumpType(type))
        {
            return Expression.Condition(IsNullExpression(expression), Expression.Constant("null"), DumpDictionaryExpression(expression, scopeMembers));
        }

        if (type.IsArray)
        {
            // An array's runtime type is always an array, whose enumerator can't be customer code.
            return Expression.Condition(IsNullExpression(expression), Expression.Constant("null"), DumpCollectionExpression(expression, scopeMembers));
        }

        if (IsSafeCollection(type))
        {
            // Enumerate only the collection types snapshot capture supports, so a customer enumerator never runs.
            if (isExactType)
            {
                return Expression.Condition(
                    IsNullExpression(expression),
                    Expression.Constant("null"),
                    Redaction.IsSupportedCollection(type) ? DumpCollectionExpression(expression, scopeMembers) : DumpFieldsExpression(expression, scopeMembers));
            }

            // Every supported collection type derives directly from object, so a declared class can only hold a
            // supported runtime type by holding exactly itself. Only an interface needs the runtime allowlist.
            if (!type.IsInterface && !Redaction.IsSupportedCollection(type))
            {
                return DumpRuntimeValueExpression(expression, Expression.Constant("null"), value => DumpFieldsExpression(value, scopeMembers), scopeMembers);
            }

            return DumpRuntimeValueExpression(
                expression,
                Expression.Constant("null"),
                value => DumpIfRuntimeTypeAllows(
                    type.IsInterface
                        ? Expression.Call(GetMethodByReflection(typeof(Redaction), nameof(Redaction.IsSupportedCollection), [typeof(object)]), value)
                        : Expression.ReferenceEqual(Expression.Call(value, getTypeMethod), Expression.Constant(type, typeof(Type))),
                    DumpCollectionExpression(Expression.Convert(value, type), scopeMembers),
                    value,
                    scopeMembers),
                scopeMembers);
        }

        return isExactType
                   ? Expression.Condition(IsNullExpression(expression), Expression.Constant("null"), DumpFieldsExpression(expression, scopeMembers))
                   : DumpRuntimeValueExpression(expression, Expression.Constant("null"), value => DumpFieldsExpression(value, scopeMembers), scopeMembers);
    }

    // Reads the value once and redacts it when its runtime type, which a non-sealed declared type can't tell, is
    // redacted. IsRedactedRuntimeValue skips the lookup when the runtime type is the declared type.
    private BlockExpression DumpRuntimeValueExpression(
        Expression expression,
        Expression nullDump,
        Func<ParameterExpression, Expression> dumpValue,
        List<ParameterExpression> scopeMembers)
    {
        var value = Expression.Variable(typeof(object), "dumpValue");
        scopeMembers.Add(value);
        var isRedacted = Expression.Call(
            GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(IsRedactedRuntimeValue), [typeof(object), typeof(Type)]),
            value,
            Expression.Constant(expression.Type, typeof(Type)));

        return Expression.Block(
            Expression.Assign(value, Expression.Convert(expression, typeof(object))),
            Expression.Condition(
                Expression.ReferenceEqual(value, Expression.Constant(null)),
                nullDump,
                Expression.Condition(isRedacted, Expression.Constant(RedactedValueDump), dumpValue(value))));
    }

    // Any runtime type that isAllowed rejects gets its fields dumped, the same output a root local of that type gets.
    private ConditionalExpression DumpIfRuntimeTypeAllows(Expression isAllowed, Expression dumpAllowed, Expression value, List<ParameterExpression> scopeMembers)
    {
        return Expression.Condition(isAllowed, dumpAllowed, DumpFieldsExpression(value, scopeMembers));
    }

    private BlockExpression DumpDictionaryExpression(Expression dictionary, List<ParameterExpression> scopeMembers)
    {
        // The declared type may be an interface over a customer dictionary, a wrapper such as ReadOnlyDictionary,
        // or a redacted type. Read the value once, redact from the runtime type, then enumerate only the
        // dictionary types snapshot capture supports.
        var value = Expression.Variable(typeof(object), "dictionaryDumpValue");
        scopeMembers.Add(value);

        var assignValue = Expression.Assign(value, Expression.Convert(dictionary, typeof(object)));
        var isNull = Expression.ReferenceEqual(value, Expression.Constant(null));
        var isRedacted = Expression.Call(
            GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(IsRedactedRuntimeValue), [typeof(object), typeof(Type)]),
            value,
            Expression.Constant(dictionary.Type, typeof(Type)));
        var isSupportedDictionary = Expression.Call(
            GetMethodByReflection(typeof(Redaction), nameof(Redaction.IsSupportedDictionary), [typeof(object)]),
            value);
        var dumpEntries = Expression.Call(
            Expression.Constant(this),
            GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(DumpCollection), [typeof(object), typeof(Type)]),
            value,
            Expression.Constant(typeof(IDictionary)));
        var dumpFields = DumpFieldsExpression(Expression.Convert(value, dictionary.Type), scopeMembers);
        var dump = Expression.Condition(isSupportedDictionary, dumpEntries, dumpFields);

        return Expression.Block(
            assignValue,
            Expression.Condition(
                isNull,
                Expression.Constant("null"),
                Expression.Condition(isRedacted, Expression.Constant(RedactedValueDump), dump)));
    }

    // DumpCollectionExpression takes the first generic argument as the item type, which is the key type for a
    // dictionary, so any declared dictionary type goes through DumpDictionaryExpression instead.
    private bool IsDictionaryDumpType(Type type)
    {
        return IsSafeNonGenericDictionary(type) ||
               (IsMicrosoftType(type) &&
                (IsGenericDictionaryInterface(type) || type.GetInterfaces().Any(IsGenericDictionaryInterface)));
    }

    private BlockExpression DumpCollectionExpression(Expression collection, List<ParameterExpression> scopeMembers)
    {
        var loopItemType = collection.Type.IsGenericType ? collection.Type.GetGenericArguments()[0] : typeof(object);
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(loopItemType);
        var enumeratorType = typeof(IEnumerator<>).MakeGenericType(loopItemType);
        var stringBuilderAppend = GetMethodByReflection(typeof(StringBuilder), nameof(StringBuilder.Append), new[] { typeof(string) });
        var getEnumeratorCall = Expression.Call(collection, GetMethodByReflection(enumerableType, nameof(IEnumerable.GetEnumerator), Type.EmptyTypes));

        var expressions = new List<Expression>();
        var loopItem = Expression.Variable(loopItemType, "loopItem");
        scopeMembers.Add(loopItem);

        var enumeratorVar = Expression.Variable(enumeratorType, "enumerator");
        scopeMembers.Add(enumeratorVar);
        expressions.Add(enumeratorVar);
        var moveNextCall = Expression.Call(enumeratorVar, GetMethodByReflection(typeof(IEnumerator), nameof(IEnumerator.MoveNext), Type.EmptyTypes));

        expressions.Add(Expression.Assign(enumeratorVar, getEnumeratorCall));

        var index = Expression.Variable(typeof(int), "index");
        scopeMembers.Add(index);
        expressions.Add(Expression.Assign(index, Expression.Constant(0)));

        var result = Expression.Variable(typeof(StringBuilder), "itemValues");
        scopeMembers.Add(result);
        expressions.Add(Expression.Assign(result, Expression.New(typeof(StringBuilder).GetConstructor(Type.EmptyTypes))));

        expressions.Add(Expression.Call(result, stringBuilderAppend, Expression.Constant("[")));

        var dumpObjectCallExpression = Expression.Call(
            result,
            stringBuilderAppend,
            Expression.Call(
                Expression.Constant(this),
                GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(DumpObject), new[] { typeof(object), typeof(Type), typeof(string), typeof(int) }),
                loopItem,
                Expression.Constant(loopItem.Type),
                Expression.Constant(string.Empty),
                Expression.Constant(1) /* no nested collection */));

        var condition = Expression.AndAlso(
            Expression.Equal(moveNextCall, Expression.Constant(true)),
            Expression.LessThan(index, Expression.Constant(3)));

        var breakLabel = Expression.Label("loopBreak");
        var loopBodyExpression = Expression.IfThenElse(
            Expression.Equal(moveNextCall, Expression.Constant(true)),
            Expression.Block(
                new[] { loopItem },
                BudgetCheck(),
                Expression.IfThenElse(
                    Expression.LessThan(index, Expression.Constant(3)),
                    Expression.Block(
                        Expression.IfThen(
                            Expression.GreaterThan(index, Expression.Constant(0)),
                            Expression.Call(result, stringBuilderAppend, Expression.Constant(", "))),
                        Expression.Assign(loopItem, Expression.Property(enumeratorVar, "Current")),
                        dumpObjectCallExpression,
                        Expression.PostIncrementAssign(index)),
                    Expression.Block(
                        Expression.Call(result, stringBuilderAppend, Expression.Constant(", ...")),
                        Expression.Break(breakLabel)))),
            Expression.Break(breakLabel));

        expressions.Add(Expression.Loop(loopBodyExpression, breakLabel));
        expressions.Add(Expression.Call(result, stringBuilderAppend, Expression.Constant("]")));
        expressions.Add(Expression.Call(result, GetMethodByReflection(typeof(StringBuilder), nameof(StringBuilder.ToString), Type.EmptyTypes)));
        return Expression.Block(expressions);
    }

    private BlockExpression DumpFieldsExpression(Expression expression, List<ParameterExpression> scopeMembers)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var getTypeMethod = GetMethodByReflection(typeof(object), nameof(object.GetType), Type.EmptyTypes);
        var getFieldsMethod = GetMethodByReflection(typeof(Type), nameof(Type.GetFields), [typeof(BindingFlags)]);
        var getSafeFieldsMethod = GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(GetSafeFieldsForDump), [typeof(FieldInfo[])]);
        var orderByMethod = GetMethodByReflection(typeof(System.Linq.Enumerable), nameof(System.Linq.Enumerable.OrderBy), [typeof(IEnumerable<>), typeof(Func<,>)], [typeof(FieldInfo), typeof(int)]);
        var toArray = GetMethodByReflection(typeof(System.Linq.Enumerable), nameof(System.Linq.Enumerable.ToArray), [typeof(IEnumerable<>)], [typeof(FieldInfo)]);

        ParameterExpression parameterExp = Expression.Parameter(typeof(FieldInfo), "fieldInfo");
        MemberExpression propertyExp = Expression.Property(parameterExp, "MetadataToken");
        Expression<Func<FieldInfo, int>> lambdaExp = Expression.Lambda<Func<FieldInfo, int>>(propertyExp, parameterExp);
        var fieldInfoArray = Expression.Call(null, getSafeFieldsMethod, Expression.Call(Expression.Call(expression, getTypeMethod), getFieldsMethod, Expression.Constant(flags)));
        var fieldInfoOrderedArray = Expression.Call(null, toArray, Expression.Call(null, orderByMethod, fieldInfoArray, lambdaExp));
        var stringBuilderAppend = GetMethodByReflection(typeof(StringBuilder), nameof(StringBuilder.Append), [typeof(string)]);
        var expressions = new List<Expression>();

        var fields = Expression.Variable(typeof(FieldInfo[]), "fieldsArray");
        scopeMembers.Add(fields);
        expressions.Add(Expression.Assign(fields, fieldInfoOrderedArray));

        var result = Expression.Variable(typeof(StringBuilder), "fieldValues");
        scopeMembers.Add(result);
        expressions.Add(Expression.Assign(result, Expression.New(typeof(StringBuilder).GetConstructor(Type.EmptyTypes))));

        var index = Expression.Variable(typeof(int), "index");
        scopeMembers.Add(index);
        expressions.Add(Expression.Assign(index, Expression.Constant(0)));

        // Loop Content
        var fieldAtIndex = Expression.ArrayIndex(fields, index);
        var fieldTypeExpression = Expression.Property(fieldAtIndex, typeof(FieldInfo), nameof(FieldInfo.FieldType));
        var fieldNameExpression = Expression.Property(fieldAtIndex, typeof(FieldInfo), nameof(FieldInfo.Name));

        var fieldGetValueExpression =
           Expression.Call(
               null,
               GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(GetFieldValueForDump), [typeof(FieldInfo), typeof(object)]),
               fieldAtIndex,
               Expression.Convert(expression, typeof(object)));

        var dumpObjectCallExpression = Expression.Call(
            result,
            stringBuilderAppend,
            Expression.Call(
                Expression.Constant(this),
                GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(DumpObject), new[] { typeof(Expression), typeof(Type), typeof(string), typeof(int) }),
                fieldGetValueExpression,
                fieldTypeExpression,
                fieldNameExpression,
                Expression.Constant(0)));

        // End Loop Content

        var breakLabel = Expression.Label("loopBreak");
        var condition = Expression.AndAlso(
            Expression.LessThan(index, Expression.Property(fields, nameof(Array.Length))),
            Expression.LessThan(index, Expression.Constant(5)));

        var loopBodyExpression = Expression.IfThenElse(
           condition,
           Expression.Block(
               BudgetCheck(),
               dumpObjectCallExpression,
               Expression.PostIncrementAssign(index),
               Expression.IfThen(
                   condition,
                   Expression.Call(result, stringBuilderAppend, Expression.Constant(", ")))),
           Expression.Block(
               Expression.IfThen(
                   Expression.LessThan(index, Expression.Property(fields, nameof(Array.Length))),
                   Expression.Call(result, stringBuilderAppend, Expression.Constant(", ..."))),
               Expression.Break(breakLabel)));

        expressions.Add(Expression.Loop(loopBodyExpression, breakLabel));
        expressions.Add(Expression.Call(result, GetMethodByReflection(typeof(StringBuilder), nameof(StringBuilder.ToString), Type.EmptyTypes)));

        return Expression.Block(expressions);
    }

    private string DumpObject(object value, Type type, string name, int depth = 0)
    {
        var hasName = !StringUtil.IsNullOrEmpty(name);
        if (ShouldRedactDumpValue(name, type))
        {
            return hasName ? $"{name}={RedactedValueDump}" : RedactedValueDump;
        }

        if (type == null)
        {
            return hasName ? $"{name}=" : string.Empty;
        }

        // only one level depth of collection
        // The declared type doesn't tell which collection the field holds, so enumerate only the runtime types
        // snapshot capture supports and a customer or wrapped enumerator never runs. Those allow lists are
        // cheap, so check them before the declared type, whose check looks up interfaces.
        if (depth == 0 &&
            (value is null || Redaction.IsSupportedDictionary(value) || Redaction.IsSupportedCollection(value)) &&
            (IsTypeSupportIndex(type, out var assignableFrom) || IsGenericDictionaryInterface(type)))
        {
            // The declared type may be an interface. Redact from the runtime type before enumerating.
            if (IsRedactedRuntimeValue(value, type))
            {
                return hasName ? $"{name}={RedactedValueDump}" : RedactedValueDump;
            }

            return DumpCollection(value, assignableFrom ?? typeof(IDictionary));
        }

        if (hasName)
        {
            name += "=";
        }

        if (type == ProbeExpressionParserHelper.UndefinedValueType)
        {
            return $"{name}{UndefinedValueDump}";
        }

        if (typeof(Exception).IsAssignableFrom(type))
        {
            if (value is not Exception ex)
            {
                return $"{name}{type.FullName}";
            }

            // The declared type may be a non-sealed exception. Redact from the runtime type before reading the message.
            return IsRedactedRuntimeValue(value, type) ? $"{name}{RedactedValueDump}" : DumpException(ex, name);
        }

        return value is not null && Redaction.IsSafeToCallToString(type) && CanCallToStringOnRuntimeType(value, type) ?
                   $"{name}{value.ToString()}" :
                   $"{name}{type?.FullName}";
    }

    private string DumpCollection(object value, Type assignableFrom)
    {
        var sb = StringBuilderCache.Acquire();
        try
        {
            if (value == null)
            {
                sb.Append("null");
                return sb.ToString();
            }

            int count = 0;
            if (assignableFrom == typeof(IList))
            {
                sb.Append('[');
                foreach (var item in (value as IList))
                {
                    sb.Append(DumpObject(item, item?.GetType(), null, 1));
                    if (++count == 3)
                    {
                        sb.Append(", ...");
                        break;
                    }

                    sb.Append(", ");
                }

                sb.Append(']');
                return sb.ToString();
            }
            else if (assignableFrom == typeof(IReadOnlyList<>))
            {
                sb.Append('[');
                foreach (var item in (value as IEnumerable))
                {
                    sb.Append(DumpObject(item, item?.GetType(), null, 1));
                    if (++count == 3)
                    {
                        sb.Append(", ...");
                        break;
                    }

                    sb.Append(", ");
                }

                sb.Append(']');
                return sb.ToString();
            }
            else if (assignableFrom == typeof(IDictionary))
            {
                sb.Append('{');
                foreach (DictionaryEntry entry in (value as IDictionary))
                {
                    sb.Append('[');
                    sb.Append(DumpObject(entry.Key, entry.Key?.GetType(), null, 1));
                    sb.Append(", ");
                    sb.Append(ShouldRedactDictionaryKey(entry.Key) ? RedactedValueDump : DumpObject(entry.Value, entry.Value?.GetType(), null, 1));
                    sb.Append(']');
                    if (++count == 3)
                    {
                        sb.Append(", ...");
                        break;
                    }

                    sb.Append(", ");
                }

                sb.Append('}');
                return sb.ToString();
            }

            return sb.ToString();
        }
        finally
        {
            StringBuilderCache.Release(sb);
        }
    }
}
