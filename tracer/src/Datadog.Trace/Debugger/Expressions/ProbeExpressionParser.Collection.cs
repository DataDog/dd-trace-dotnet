// <copyright file="ProbeExpressionParser.Collection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Type = System.Type;

namespace Datadog.Trace.Debugger.Expressions;

internal partial class ProbeExpressionParser<T>
{
    private Dictionary<Expression, RedactedDictionaryValueExpression> _redactedDictionaryValues;
    private bool _isBoundedFilterCapture;
    private int _deferredFilterSourceArrayStack = -1;
    private int _boundedFilterMaxCollectionSize;

    private enum PredicateOperation
    {
        Any,
        All,
        Filter
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

    private static Expression RedactedDictionaryOperationDefault(Type type)
    {
        if (type == typeof(bool))
        {
            return Expression.Constant(false);
        }

        return type == typeof(string)
                   ? Expression.Constant("{REDACTED}")
                   : Expression.Default(type);
    }

    private static bool IsDictionaryEntryType(Type type)
    {
        return type == typeof(DictionaryEntry) ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>));
    }

    private Expression HasAny(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        return Predicate(reader, parameters, PredicateOperation.Any, itParameter);
    }

    private Expression HasAll(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        return Predicate(reader, parameters, PredicateOperation.All, itParameter);
    }

    private Expression Filter(JsonTextReader reader, List<ParameterExpression> parameters)
    {
        var shouldDefer = _deferredFilterSourceArrayStack == _arrayStack;
        var filterExpression = ParseFilter(reader, parameters, shouldDefer);
        if (filterExpression is not FilterExpression parsedFilterExpression)
        {
            return filterExpression;
        }

        if (shouldDefer)
        {
            return parsedFilterExpression;
        }

        return _isBoundedFilterCapture
                   ? BoundedFilterExpression(parsedFilterExpression, _boundedFilterMaxCollectionSize)
                   : MaterializeFilterExpression(parsedFilterExpression);
    }

    private Expression Predicate(JsonTextReader reader, List<ParameterExpression> parameters, PredicateOperation operation, ParameterExpression outerItParameter)
    {
        Expression source = null;
        Expression predicateExpression = null;
        try
        {
            source = ParseTree(reader, parameters, outerItParameter);
            if (source is GotoExpression)
            {
                return source;
            }

            if (source.Type == ProbeExpressionParserHelper.UndefinedValueType)
            {
                return ReturnDefaultValueExpression();
            }

            if (!IsSafeCollection(source.Type) && !IsSafeNonGenericDictionary(source.Type))
            {
                throw new InvalidOperationException("Source must be an array or implement ICollection, IReadOnlyCollection, or IDictionary");
            }

            var itParameterType = GetIteratorParameterType(source.Type);
            ParameterExpression itParameter = Expression.Parameter(itParameterType);
            predicateExpression = ParseTree(reader, parameters, itParameter);

            return operation switch
            {
                PredicateOperation.Any => EnumerableAnyExpression(source, itParameter, predicateExpression),
                PredicateOperation.All => EnumerableAllExpression(source, itParameter, predicateExpression),
                PredicateOperation.Filter => EnumerableFilterExpression(source, itParameter, predicateExpression),
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported predicate operation")
            };
        }
        catch (Exception e)
        {
            AddError($"{source?.ToString() ?? "N/A"}[{predicateExpression?.ToString() ?? "N/A"}]", e.Message);
            return ReturnDefaultValueExpression();
        }
    }

    private Expression ParseFilter(JsonTextReader reader, List<ParameterExpression> parameters, bool isDeferredFilterSource = false)
    {
        Expression source = null;
        try
        {
            var previousIsBoundedFilterCapture = _isBoundedFilterCapture;
            var previousDeferredFilterSourceArrayStack = _deferredFilterSourceArrayStack;
            _isBoundedFilterCapture = false;
            _deferredFilterSourceArrayStack = previousIsBoundedFilterCapture || isDeferredFilterSource
                                                  ? _arrayStack + 1
                                                  : -1;
            try
            {
                source = ParseTree(reader, parameters, null);
            }
            finally
            {
                _isBoundedFilterCapture = previousIsBoundedFilterCapture;
                _deferredFilterSourceArrayStack = previousDeferredFilterSourceArrayStack;
            }

            if (source is GotoExpression)
            {
                return source;
            }

            if (source.Type == ProbeExpressionParserHelper.UndefinedValueType)
            {
                return ReturnDefaultValueExpression();
            }

            if (source is not FilterExpression && !IsSafeCollection(source.Type) && !IsSafeNonGenericDictionary(source.Type))
            {
                throw new InvalidOperationException("Source must be an array or implement ICollection, IReadOnlyCollection, or IDictionary");
            }

            var itParameterType = source is FilterExpression sourceFilterExpression
                                      ? sourceFilterExpression.IteratorType
                                      : GetIteratorParameterType(source.Type);
            ParameterExpression itParameter = Expression.Parameter(itParameterType);
            previousIsBoundedFilterCapture = _isBoundedFilterCapture;
            previousDeferredFilterSourceArrayStack = _deferredFilterSourceArrayStack;
            _isBoundedFilterCapture = false;
            _deferredFilterSourceArrayStack = -1;
            Expression predicate;
            try
            {
                predicate = ParseTree(reader, parameters, itParameter);
            }
            finally
            {
                _isBoundedFilterCapture = previousIsBoundedFilterCapture;
                _deferredFilterSourceArrayStack = previousDeferredFilterSourceArrayStack;
            }

            var lambda = Expression.Lambda(predicate, itParameter);
            var isDictionary = source is FilterExpression filterSource
                                   ? filterSource.IsDictionary
                                   : IsSafeNonGenericDictionary(source.Type) || IsSupportedGenericDictionary(source.Type);
            return new FilterExpression(source, lambda, itParameterType, isDictionary);
        }
        catch (Exception e)
        {
            AddError($"{source?.ToString() ?? "N/A"}[filter]", e.Message);
            return ReturnDefaultValueExpression();
        }
    }

    private BlockExpression MaterializeFilterExpression(FilterExpression filterExpression)
    {
        var source = filterExpression.Source is FilterExpression sourceFilterExpression
                         ? MaterializeFilterExpression(sourceFilterExpression)
                         : filterExpression.Source;
        return EnumerableFilterExpression(source, filterExpression.Predicate.Parameters[0], filterExpression.Predicate.Body);
    }

    private MethodCallExpression BoundedFilterExpression(FilterExpression filterExpression, int maxCollectionSize)
    {
        var helperMethod = ProbeExpressionParserHelper.GetMethodByReflection(
            typeof(FilterEvaluationHelpers),
            nameof(FilterEvaluationHelpers.FilterForCapture),
            [typeof(IEnumerable<>), typeof(FilterEvaluationHelpers.FilterPredicate<>), typeof(EvaluationBudget).MakeByRefType(), typeof(int), typeof(bool)],
            [filterExpression.IteratorType]);
        var source = FlattenFilterChain(filterExpression, out var predicate);

        return Expression.Call(
            null,
            helperMethod,
            PredicateSource(source, filterExpression.IteratorType),
            PredicateArgument(predicate, filterExpression.IteratorType),
            _evaluationBudgetParameterExpression,
            Expression.Constant(maxCollectionSize),
            Expression.Constant(filterExpression.IsDictionary));
    }

    private Expression FlattenFilterChain(FilterExpression filterExpression, out LambdaExpression predicate)
    {
        var source = filterExpression.Source;
        predicate = filterExpression.Predicate;
        while (source is FilterExpression sourceFilterExpression && sourceFilterExpression.IteratorType == filterExpression.IteratorType)
        {
            predicate = CombineFilterPredicates(sourceFilterExpression.Predicate, predicate);
            source = sourceFilterExpression.Source;
        }

        return source;
    }

    private LambdaExpression CombineFilterPredicates(LambdaExpression firstPredicate, LambdaExpression secondPredicate)
    {
        var parameter = firstPredicate.Parameters[0];
        var secondPredicateBody = new ParameterReplacingVisitor(secondPredicate.Parameters[0], parameter).Visit(secondPredicate.Body);
        return Expression.Lambda(Expression.AndAlso(firstPredicate.Body, secondPredicateBody), parameter);
    }

    private Expression PredicateArgument(LambdaExpression predicate, Type iteratorType)
    {
        var budgetedPredicate = BudgetedPredicate(predicate, iteratorType);
        return ReferencesOuterParameter(budgetedPredicate)
                   ? budgetedPredicate
                   : CompiledPredicateConstant(budgetedPredicate, iteratorType);
    }

    private LambdaExpression BudgetedPredicate(LambdaExpression predicate, Type iteratorType)
    {
        var itemParameter = predicate.Parameters[0];
        var budgetParameter = Expression.Parameter(typeof(EvaluationBudget).MakeByRefType(), "budget");
        var body = new ParameterReplacingVisitor(_evaluationBudgetParameterExpression, budgetParameter).Visit(predicate.Body);
        return Expression.Lambda(typeof(FilterEvaluationHelpers.FilterPredicate<>).MakeGenericType(iteratorType), body, itemParameter, budgetParameter);
    }

    private bool ReferencesOuterParameter(LambdaExpression predicate)
    {
        var visitor = new OuterParameterReferenceVisitor(predicate.Parameters);
        visitor.Visit(predicate.Body);
        return visitor.Found;
    }

    private ConstantExpression CompiledPredicateConstant(LambdaExpression predicate, Type iteratorType)
    {
        return Expression.Constant(predicate.Compile(), predicate.Type);
    }

    private BlockExpression EnumerableAnyExpression(Expression source, ParameterExpression itParameter, Expression predicate)
    {
        var result = Expression.Variable(typeof(bool), "anyResult");
        var breakLabel = Expression.Label("anyBreak");
        return BuildEnumerableLoop(
            source,
            itParameter,
            [result],
            Expression.Assign(result, Expression.Constant(false)),
            Expression.IfThen(
                predicate,
                Expression.Block(
                    Expression.Assign(result, Expression.Constant(true)),
                    Expression.Break(breakLabel))),
            breakLabel,
            result);
    }

    private BlockExpression EnumerableAllExpression(Expression source, ParameterExpression itParameter, Expression predicate)
    {
        var result = Expression.Variable(typeof(bool), "allResult");
        var breakLabel = Expression.Label("allBreak");
        return BuildEnumerableLoop(
            source,
            itParameter,
            [result],
            Expression.Assign(result, Expression.Constant(true)),
            Expression.IfThen(
                Expression.Not(predicate),
                Expression.Block(
                    Expression.Assign(result, Expression.Constant(false)),
                    Expression.Break(breakLabel))),
            breakLabel,
            result);
    }

    private BlockExpression EnumerableFilterExpression(Expression source, ParameterExpression itParameter, Expression predicate)
    {
        var listType = typeof(List<>).MakeGenericType(itParameter.Type);
        var result = Expression.Variable(listType, "filterResult");
        var breakLabel = Expression.Label("filterBreak");
        var addMethod = ProbeExpressionParserHelper.GetMethodByReflection(listType, nameof(List<int>.Add), [itParameter.Type]);

        return BuildEnumerableLoop(
            source,
            itParameter,
            [result],
            Expression.Assign(result, Expression.New(listType)),
            Expression.IfThen(
                predicate,
                Expression.Call(result, addMethod, itParameter)),
            breakLabel,
            result);
    }

    private BlockExpression BuildEnumerableLoop(
        Expression source,
        ParameterExpression itParameter,
        IEnumerable<ParameterExpression> additionalVariables,
        Expression initializeResult,
        Expression loopBody,
        LabelTarget breakLabel,
        Expression result)
    {
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(itParameter.Type);
        var enumeratorType = typeof(IEnumerator<>).MakeGenericType(itParameter.Type);
        var enumerable = Expression.Variable(enumerableType, "enumerable");
        var enumerator = Expression.Variable(enumeratorType, "enumerator");
        var moveNext = ProbeExpressionParserHelper.GetMethodByReflection(typeof(IEnumerator), nameof(IEnumerator.MoveNext), Type.EmptyTypes);
        var getCurrent = ProbeExpressionParserHelper.GetMethodByReflection(enumeratorType, "get_Current", Type.EmptyTypes);
        var getEnumerator = ProbeExpressionParserHelper.GetMethodByReflection(enumerableType, nameof(IEnumerable.GetEnumerator), Type.EmptyTypes);
        var dispose = ProbeExpressionParserHelper.GetMethodByReflection(typeof(IDisposable), nameof(IDisposable.Dispose), Type.EmptyTypes);

        var variables = new List<ParameterExpression>(additionalVariables)
        {
            enumerable,
            enumerator,
            itParameter
        };

        return Expression.Block(
            variables,
            BudgetCheck(),
            Expression.Assign(enumerable, Expression.Convert(PredicateSource(source, itParameter.Type), enumerableType)),
            Expression.Assign(enumerator, Expression.Call(enumerable, getEnumerator)),
            initializeResult,
            Expression.TryFinally(
                Expression.Loop(
                    Expression.Block(
                        BudgetCheck(),
                        Expression.IfThen(
                            Expression.Not(Expression.Call(enumerator, moveNext)),
                            Expression.Break(breakLabel)),
                        Expression.Assign(itParameter, Expression.Call(enumerator, getCurrent)),
                        loopBody),
                    breakLabel),
                Expression.IfThen(
                    Expression.NotEqual(enumerator, Expression.Constant(null, enumeratorType)),
                    Expression.Call(enumerator, dispose))),
            result);
    }

    private Expression GetItemAtIndex(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        Expression indexOrKey = null, source = null;
        try
        {
            source = ParseTree(reader, parameters, itParameter);
            indexOrKey = ParseTree(reader, parameters, itParameter);
            if (source.Type == ProbeExpressionParserHelper.UndefinedValueType)
            {
                return ReturnDefaultValueExpression();
            }

            if (!IsTypeSupportIndex(source.Type, out var assignableFrom))
            {
                throw new InvalidOperationException("Source must implement IList or IDictionary");
            }

            if (indexOrKey.Type == typeof(string) &&
                indexOrKey is ConstantExpression expr &&
                Redaction.Instance.ShouldRedact(expr.Value?.ToString(), expr.Type, out _))
            {
                AddError($"{source?.ToString() ?? "N/A"}[{indexOrKey?.ToString() ?? "N/A"}]", "The property or field is redacted.");
                return RedactedValue();
            }

            return CallGetItem(source, assignableFrom, indexOrKey);
        }
        catch (Exception e)
        {
            AddError($"{source?.ToString() ?? "N/A"}[{indexOrKey?.ToString() ?? "N/A"}]", e.Message);
            return ReturnDefaultValueExpression();
        }
    }

    private Expression CallGetItem(Expression source, Type assignableFrom, Expression indexOrKey)
    {
        MethodInfo getItemMethod = null;
        Type convertToType = typeof(object);
        var genericTypeArguments = source.Type.GenericTypeArguments;
        if (assignableFrom == typeof(IList) || assignableFrom == typeof(IReadOnlyList<>))
        {
            if (source.Type.IsArray)
            {
                convertToType = source.Type.GetElementType();
            }
            else if (genericTypeArguments.Length > 0)
            {
                convertToType = genericTypeArguments[0];
            }

            getItemMethod = ProbeExpressionParserHelper.GetMethodByReflection(assignableFrom, "get_Item", new[] { typeof(int) });
        }
        else if (assignableFrom == typeof(IDictionary))
        {
            Type keyType;
            switch (genericTypeArguments.Length)
            {
                case 2:
                    keyType = genericTypeArguments[0];
                    convertToType = genericTypeArguments[1];
                    break;
                case 1:
                    keyType = genericTypeArguments[0];
                    break;
                default:
                    keyType = typeof(object);
                    break;
            }

            getItemMethod = ProbeExpressionParserHelper.GetMethodByReflection(assignableFrom, "get_Item", new[] { keyType });
        }

        if (getItemMethod == null)
        {
            throw new InvalidOperationException("Unsupported collection");
        }

        var getItemCall = Expression.Call(source, getItemMethod, indexOrKey);
        Expression result;
        if (getItemCall.Type == convertToType)
        {
            result = getItemCall;
        }
        else
        {
            result = Expression.Convert(getItemCall, convertToType);
        }

        return IsDictionaryEntryType(result.Type)
                   ? TrackRedactedDictionaryEntry(result)
                   : result;
    }

    private Expression Length(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        Expression source = null;
        try
        {
            source = ParseTree(reader, parameters, itParameter);
            if (source.Type == ProbeExpressionParserHelper.UndefinedValueType)
            {
                return ReturnDefaultValueExpression();
            }

            return RedactDictionaryOperation(source, CollectionAndStringLengthExpression(source));
        }
        catch (Exception e)
        {
            AddError($"{source?.ToString() ?? "N/A"}.Count", e.Message);
            return ReturnDefaultValueExpression();
        }
    }

    private MethodCallExpression CollectionAndStringLengthExpression(Expression source)
    {
        if (source?.Type == typeof(string))
        {
            var lengthMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(string), "get_Length", Type.EmptyTypes);
            return Expression.Call(source, lengthMethod);
        }

        if (!IsSafeCollection(source?.Type))
        {
            throw new InvalidOperationException("Source must be an array or implement ICollection or IReadOnlyCollection");
        }

        var countOrLength = ProbeExpressionParserHelper.GetMethodByReflection(source.Type, source.Type.IsArray ? "get_Length" : "get_Count", Type.EmptyTypes);

        return Expression.Call(source, countOrLength);
    }

    private Type GetIteratorParameterType(Type sourceType)
    {
        if (sourceType.IsArray)
        {
            return sourceType.GetElementType();
        }

        var genericDictionaryType = sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                                        ? sourceType
                                        : sourceType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        if (genericDictionaryType != null)
        {
            return typeof(KeyValuePair<,>).MakeGenericType(genericDictionaryType.GetGenericArguments());
        }

        if (typeof(IDictionary).IsAssignableFrom(sourceType))
        {
            return typeof(DictionaryEntry);
        }

        var enumerableType = sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                                 ? sourceType
                                 : sourceType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableType != null)
        {
            return enumerableType.GetGenericArguments()[0];
        }

        throw new InvalidOperationException("Fail to determined the iterator parameter type");
    }

    private Expression PredicateSource(Expression source, Type itParameterType)
    {
        if (itParameterType != typeof(DictionaryEntry) || !IsSafeNonGenericDictionary(source.Type))
        {
            return source;
        }

        var castMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(Enumerable), nameof(Enumerable.Cast), [typeof(IEnumerable)], [typeof(DictionaryEntry)]);
        return Expression.Call(null, castMethod, source);
    }

    private bool TryGetCollectionIteratorProperty(ParameterExpression itParameter, string propertyName, out Expression propertyExpression)
    {
        propertyExpression = null;

        if (itParameter.Type == typeof(DictionaryEntry))
        {
            var property = Expression.Property(itParameter, propertyName);
            propertyExpression = propertyName switch
            {
                nameof(KeyValuePair<int, int>.Key) => property,
                nameof(KeyValuePair<int, int>.Value) when TryRedactDictionaryValueMember(itParameter, out var redactedValue) => redactedValue,
                _ => property,
            };
            return true;
        }

        if (itParameter.Type.IsGenericType && itParameter.Type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            var property = Expression.Property(itParameter, propertyName);
            propertyExpression = propertyName switch
            {
                nameof(KeyValuePair<int, int>.Key) => property,
                nameof(KeyValuePair<int, int>.Value) when TryRedactDictionaryValueMember(itParameter, out var redactedValue) => redactedValue,
                _ => property,
            };
            return true;
        }

        return false;
    }

    private bool TryRedactDictionaryValueMember(Expression dictionaryEntryExpression, out Expression redactedValue)
    {
        redactedValue = null;

        if (!IsDictionaryEntryType(dictionaryEntryExpression.Type))
        {
            return false;
        }

        var valueExpression = Expression.Property(dictionaryEntryExpression, nameof(KeyValuePair<int, int>.Value));
        var keyExpression = Expression.Property(dictionaryEntryExpression, nameof(KeyValuePair<int, int>.Key));
        redactedValue = TrackRedactedDictionaryValue(valueExpression, keyExpression);
        return true;
    }

    private Expression TrackRedactedDictionaryEntry(Expression dictionaryEntryExpression)
    {
        var keyExpression = Expression.Property(dictionaryEntryExpression, nameof(KeyValuePair<int, int>.Key));
        TrackRedactedDictionaryExpression(dictionaryEntryExpression, keyExpression);
        return dictionaryEntryExpression;
    }

    private MemberExpression TrackRedactedDictionaryValue(MemberExpression valueExpression, MemberExpression keyExpression)
    {
        TrackRedactedDictionaryExpression(valueExpression, keyExpression);
        return valueExpression;
    }

    private void TrackRedactedDictionaryExpression(Expression expression, Expression keyExpression)
    {
        var shouldRedactKeyMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(ShouldRedactDictionaryKey), [typeof(object)]);
        var shouldRedactCall = Expression.Call(null, shouldRedactKeyMethod, Expression.Convert(keyExpression, typeof(object)));
        (_redactedDictionaryValues ??= new Dictionary<Expression, RedactedDictionaryValueExpression>())
           .Add(expression, new RedactedDictionaryValueExpression(shouldRedactCall, expression));
    }

    private bool TryGetRedactedDictionaryValue(Expression expression, out RedactedDictionaryValueExpression redactedDictionaryValue)
    {
        while (expression != null)
        {
            if (_redactedDictionaryValues != null && _redactedDictionaryValues.TryGetValue(expression, out redactedDictionaryValue))
            {
                return true;
            }

            if (expression is not UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unaryExpression)
            {
                break;
            }

            expression = unaryExpression.Operand;
        }

        redactedDictionaryValue = default;
        return false;
    }

    private MemberExpression RedactedDictionaryValueMember(RedactedDictionaryValueExpression redactedDictionaryValue, string propertyOrFieldValue)
    {
        var valueExpression = redactedDictionaryValue.ValueExpression;
        var memberExpression = Expression.PropertyOrField(valueExpression, propertyOrFieldValue);
        (_redactedDictionaryValues ??= new Dictionary<Expression, RedactedDictionaryValueExpression>())
           .Add(memberExpression, new RedactedDictionaryValueExpression(redactedDictionaryValue.ShouldRedactExpression, memberExpression));
        return memberExpression;
    }

    private Expression RedactDictionaryBinaryOperation(Expression left, Expression right, Expression comparison)
    {
        Expression shouldRedact = null;
        if (TryGetRedactedDictionaryValue(left, out var leftRedactedDictionaryValue))
        {
            shouldRedact = leftRedactedDictionaryValue.ShouldRedactExpression;
        }

        if (TryGetRedactedDictionaryValue(right, out var rightRedactedDictionaryValue))
        {
            shouldRedact = shouldRedact == null
                               ? rightRedactedDictionaryValue.ShouldRedactExpression
                               : Expression.OrElse(shouldRedact, rightRedactedDictionaryValue.ShouldRedactExpression);
        }

        return RedactDictionaryOperationWithGuard(shouldRedact, comparison);
    }

    private Expression RedactDictionaryOperation(Expression source, Expression operation)
    {
        return TryGetRedactedDictionaryValue(source, out var redactedDictionaryValue)
                   ? RedactDictionaryOperationWithGuard(redactedDictionaryValue.ShouldRedactExpression, operation)
                   : operation;
    }

    private Expression RedactDictionaryOperationWithGuard(Expression shouldRedact, Expression operation)
    {
        if (shouldRedact == null)
        {
            return operation;
        }

        var redactedOperation = Expression.Condition(shouldRedact, RedactedDictionaryOperationDefault(operation.Type), operation);
        (_redactedDictionaryValues ??= new Dictionary<Expression, RedactedDictionaryValueExpression>())
           .Add(redactedOperation, new RedactedDictionaryValueExpression(shouldRedact, redactedOperation));
        return redactedOperation;
    }

    private Expression RedactDictionaryValueForReturn(RedactedDictionaryValueExpression redactedDictionaryValue, Expression finalExpr, List<ParameterExpression> scopeMembers)
    {
        var redactedValue = RedactedValue();
        if (typeof(T) == typeof(object))
        {
            return Expression.Condition(
                redactedDictionaryValue.ShouldRedactExpression,
                Expression.Convert(redactedValue, typeof(object)),
                Expression.Convert(finalExpr, typeof(object)));
        }

        if (typeof(T) == typeof(string))
        {
            var valueAsString = finalExpr.Type == typeof(string)
                                    ? finalExpr
                                    : DumpExpression(finalExpr, scopeMembers);
            return Expression.Condition(redactedDictionaryValue.ShouldRedactExpression, redactedValue, valueAsString);
        }

        if (typeof(T) == typeof(bool) && finalExpr.Type == typeof(bool))
        {
            return Expression.Condition(redactedDictionaryValue.ShouldRedactExpression, Expression.Constant(false), finalExpr);
        }

        if (typeof(T).IsNumeric() && TryConvertToNumericType<T>(finalExpr, out var numericResult))
        {
            return Expression.Condition(redactedDictionaryValue.ShouldRedactExpression, Expression.Constant(default(T), typeof(T)), numericResult);
        }

        return finalExpr;
    }

    private bool IsSafeCollection(Type type)
    {
        if (type == null)
        {
            return false;
        }

        return type.IsArray || (IsMicrosoftType(type) && IsCollection(type));
    }

    private bool IsSafeNonGenericDictionary(Type type)
    {
        return type != null && IsMicrosoftType(type) && typeof(IDictionary).IsAssignableFrom(type);
    }

    private bool IsSupportedGenericDictionary(Type type)
    {
        return type != null &&
               IsMicrosoftType(type) &&
               ((type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>)) ||
                type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)));
    }

    private bool IsIEnumerable(Type type)
    {
        return IsSafeCollection(type) || type.GetInterface(nameof(IEnumerable)) != null;
    }

    private bool IsCollection(Type type)
    {
        return type.GetInterfaces()
                   .Any(i =>
                            i.IsGenericType
                         && (i.GetGenericTypeDefinition() == typeof(ICollection)
                          || i.GetGenericTypeDefinition() == typeof(ICollection<>)
                          || i.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)));
    }

    private bool IsTypeSupportIndex(Type type, out Type assignableFrom)
    {
        if (type.IsArray)
        {
            assignableFrom = typeof(IList);
            return true;
        }

        if (!IsMicrosoftType(type))
        {
            assignableFrom = null;
            return false;
        }

        // Do not use IsInstanceOfType
        if (type == typeof(IList) || type.GetInterface(nameof(IList)) != null)
        {
            assignableFrom = typeof(IList);
            return true;
        }

        if (type == typeof(IDictionary) || type.GetInterface(nameof(IDictionary)) != null)
        {
            assignableFrom = typeof(IDictionary);
            return true;
        }

        if (type == typeof(IReadOnlyList<>) ||
            (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)) ||
            type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)))
        {
            assignableFrom = typeof(IReadOnlyList<>);
            return true;
        }

        assignableFrom = null;
        return false;
    }

    private readonly struct RedactedDictionaryValueExpression(Expression shouldRedactExpression, Expression valueExpression)
    {
        internal Expression ShouldRedactExpression { get; } = shouldRedactExpression;

        internal Expression ValueExpression { get; } = valueExpression;
    }

    private sealed class OuterParameterReferenceVisitor : ExpressionVisitor
    {
        private readonly List<ParameterExpression> _localParameters;

        internal OuterParameterReferenceVisitor(IReadOnlyList<ParameterExpression> localParameters)
        {
            _localParameters = new List<ParameterExpression>(localParameters);
        }

        internal bool Found { get; private set; }

        protected override Expression VisitBlock(BlockExpression node)
        {
            var previousCount = _localParameters.Count;
            foreach (var variable in node.Variables)
            {
                _localParameters.Add(variable);
            }

            Visit(node.Expressions);
            _localParameters.RemoveRange(previousCount, _localParameters.Count - previousCount);
            return node;
        }

        protected override Expression VisitLambda<TDelegate>(Expression<TDelegate> node)
        {
            var previousCount = _localParameters.Count;
            foreach (var parameter in node.Parameters)
            {
                _localParameters.Add(parameter);
            }

            Visit(node.Body);
            _localParameters.RemoveRange(previousCount, _localParameters.Count - previousCount);
            return node;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (!_localParameters.Contains(node))
            {
                Found = true;
            }

            return node;
        }
    }
}
