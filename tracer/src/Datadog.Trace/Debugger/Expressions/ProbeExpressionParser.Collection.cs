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
    private bool _isBoundedFilterCapture;
    private int _deferredFilterSourceArrayStack = -1;
    private int _boundedFilterMaxCollectionSize;

    private enum PredicateOperation
    {
        Any,
        All,
        Filter
    }

    private static bool IsDictionaryEntryType(Type type)
    {
        return type == typeof(DictionaryEntry) ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>));
    }

    private static bool IsImplicitlyConvertibleToInt32(Type type)
    {
        return type == typeof(sbyte) ||
               type == typeof(byte) ||
               type == typeof(short) ||
               type == typeof(ushort) ||
               type == typeof(char);
    }

    private static bool TryFoldStringKey(Expression key, out ConstantExpression constant)
    {
        constant = null;
        try
        {
            var visitor = new FoldableKeyVisitor();
            var body = visitor.Visit(key);
            if (visitor.IsRejected)
            {
                return false;
            }

            constant = Expression.Constant(Expression.Lambda<Func<string>>(body).Compile()(), typeof(string));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static Expression InnermostFilterSource(FilterExpression filterExpression)
    {
        var source = filterExpression.Source;
        while (source is FilterExpression sourceFilterExpression)
        {
            source = sourceFilterExpression.Source;
        }

        return source;
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

        Expression filtered = _isBoundedFilterCapture
                                  ? BoundedFilterExpression(parsedFilterExpression, _boundedFilterMaxCollectionSize)
                                  : MaterializeFilterExpression(parsedFilterExpression);
        return RedactOperation(InnermostFilterSource(parsedFilterExpression), filtered);
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

            if (IsRedactedValue(source))
            {
                SkipPredicateOfRedactedSource(reader, parameters);
                return Expression.Constant(false);
            }

            if (!IsSafeCollection(source.Type) && !IsSafeNonGenericDictionary(source.Type))
            {
                throw new InvalidOperationException("Source must be an array or implement ICollection, IReadOnlyCollection, or IDictionary");
            }

            var itParameterType = GetIteratorParameterType(source.Type);
            ParameterExpression itParameter = Expression.Parameter(itParameterType);
            predicateExpression = ParsePredicate(reader, parameters, itParameter, outsideFilterCapture: false);

            Expression loop = operation switch
            {
                PredicateOperation.Any => EnumerableAnyExpression(source, itParameter, predicateExpression),
                PredicateOperation.All => EnumerableAllExpression(source, itParameter, predicateExpression),
                PredicateOperation.Filter => EnumerableFilterExpression(source, itParameter, predicateExpression),
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported predicate operation")
            };
            return RedactOperation(source, loop);
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
            var sourceDeferredFilterArrayStack = _isBoundedFilterCapture || isDeferredFilterSource ? _arrayStack + 1 : -1;
            source = ParseTreeOutsideFilterCapture(reader, parameters, itParameter: null, sourceDeferredFilterArrayStack);

            if (source is GotoExpression)
            {
                return source;
            }

            if (source.Type == ProbeExpressionParserHelper.UndefinedValueType)
            {
                return ReturnDefaultValueExpression();
            }

            if (IsRedactedValue(source))
            {
                SkipPredicateOfRedactedSource(reader, parameters);
                return source;
            }

            if (source is not FilterExpression && !IsSafeCollection(source.Type) && !IsSafeNonGenericDictionary(source.Type))
            {
                throw new InvalidOperationException("Source must be an array or implement ICollection, IReadOnlyCollection, or IDictionary");
            }

            var itParameterType = source is FilterExpression sourceFilterExpression
                                      ? sourceFilterExpression.IteratorType
                                      : GetIteratorParameterType(source.Type);
            ParameterExpression itParameter = Expression.Parameter(itParameterType);
            var predicate = ParsePredicate(reader, parameters, itParameter, outsideFilterCapture: true);

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

    private Expression ParseTreeOutsideFilterCapture(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter, int deferredFilterSourceArrayStack)
    {
        var previousIsBoundedFilterCapture = _isBoundedFilterCapture;
        var previousDeferredFilterSourceArrayStack = _deferredFilterSourceArrayStack;
        _isBoundedFilterCapture = false;
        _deferredFilterSourceArrayStack = deferredFilterSourceArrayStack;
        try
        {
            return ParseTree(reader, parameters, itParameter);
        }
        finally
        {
            _isBoundedFilterCapture = previousIsBoundedFilterCapture;
            _deferredFilterSourceArrayStack = previousDeferredFilterSourceArrayStack;
        }
    }

    // Parses and drops the predicate of an operation over a redacted value, so the reader stays aligned and the
    // predicate adds no errors about the placeholder that replaced the value.
    private void SkipPredicateOfRedactedSource(JsonTextReader reader, List<ParameterExpression> parameters)
    {
        var errorCount = _errors?.Count ?? 0;
        try
        {
            ParsePredicate(reader, parameters, itParameter: null, outsideFilterCapture: true);
        }
        finally
        {
            _errors?.RemoveRange(errorCount, _errors.Count - errorCount);
        }
    }

    // A predicate declares the variables of its own runtime-type guards, so a filter predicate compiled on its own
    // doesn't close over the outer block.
    private Expression ParsePredicate(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter, bool outsideFilterCapture)
    {
        var outerScope = EnterPredicateRedactionGuardScope();
        try
        {
            return ScopePredicateRedactionGuards(
                RedactBooleanOperand(
                    outsideFilterCapture
                        ? ParseTreeOutsideFilterCapture(reader, parameters, itParameter, deferredFilterSourceArrayStack: -1)
                        : ParseTree(reader, parameters, itParameter)));
        }
        finally
        {
            ExitPredicateRedactionGuardScope(outerScope);
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

            if (IsRedactedValue(source) || IsRedactedValue(indexOrKey))
            {
                return RedactedValue();
            }

            var redactedSource = RedactIfRedactedType(source, source, indexOrKey);
            if (IsRedactedValue(redactedSource))
            {
                return redactedSource;
            }

            if (!IsTypeSupportIndex(source.Type, out var assignableFrom))
            {
                throw new InvalidOperationException("Source must implement IList or IDictionary");
            }

            // Fold string keys computed only from constants so they are redaction-checked like literal keys.
            if (indexOrKey?.Type == typeof(string) &&
                indexOrKey is not ConstantExpression &&
                TryFoldStringKey(indexOrKey, out var foldedKey))
            {
                indexOrKey = foldedKey;
            }

            if (indexOrKey.Type == typeof(string) &&
                indexOrKey is ConstantExpression expr &&
                Redaction.Instance.ShouldRedact(expr.Value?.ToString(), expr.Type, out _))
            {
                AddRedactionError($"{source?.ToString() ?? "N/A"}[{indexOrKey?.ToString() ?? "N/A"}]");
                return RedactedValue();
            }

            var item = CallGetItem(source, assignableFrom, indexOrKey);
            DeriveRedactionGuard(item, source, indexOrKey);
            return RedactByType(item, source, indexOrKey, CurrentRedactionGuardScope);
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
            if (genericTypeArguments.Length == 2)
            {
                convertToType = genericTypeArguments[1];
                if (convertToType.IsValueType && Nullable.GetUnderlyingType(convertToType) is null)
                {
                    convertToType = typeof(Nullable<>).MakeGenericType(convertToType);
                }
            }

            // IDictionary.get_Item takes object. Looking up get_Item(TKey) does not match
            // that signature, and Expression.Call cannot box value-type keys such as int.
            getItemMethod = ProbeExpressionParserHelper.GetMethodByReflection(assignableFrom, "get_Item", new[] { typeof(object) });
        }

        if (getItemMethod == null)
        {
            throw new InvalidOperationException("Unsupported collection");
        }

        indexOrKey = ConvertIndexerArgument(indexOrKey, getItemMethod);
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
                   ? GuardDictionaryEntry(result)
                   : result;
    }

    private Expression ConvertIndexerArgument(Expression indexOrKey, MethodInfo getItemMethod)
    {
        var parameterType = getItemMethod.GetParameters()[0].ParameterType;
        if (indexOrKey.Type == parameterType)
        {
            return indexOrKey;
        }

        // Expression.Call does not apply C# implicit conversions, including boxing.
        if (indexOrKey.Type.IsValueType &&
            (parameterType.IsAssignableFrom(indexOrKey.Type) ||
             (parameterType == typeof(int) && IsImplicitlyConvertibleToInt32(indexOrKey.Type))))
        {
            return Expression.Convert(indexOrKey, parameterType);
        }

        return indexOrKey;
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

            if (IsRedactedValue(source))
            {
                return source;
            }

            return RedactOperation(source, CollectionAndStringLengthExpression(source));
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

    // @key and @value share one memoized guard with getmember(@it, "Key") and getmember(@it, "Value").
    private bool TryGetIteratorEntryMember(ParameterExpression itParameter, string propertyName, string readerValue, out Expression member)
    {
        // Checked first: getmember(@it, "Key") on an item that isn't an entry may have memoized a member of that name.
        if (!IsDictionaryEntryType(itParameter.Type))
        {
            member = null;
            return false;
        }

        if (TryGetGuardedMember(itParameter, propertyName, out member))
        {
            return true;
        }

        if (!TryGetDictionaryEntryMember(itParameter, propertyName, out member))
        {
            return false;
        }

        member = MemoizeGuardedMember(itParameter, propertyName, RedactByType(member, null, readerValue, CurrentRedactionGuardScope));
        return true;
    }

    private bool TryGetDictionaryEntryMember(Expression dictionaryEntryExpression, string propertyName, out Expression memberExpression)
    {
        memberExpression = null;

        if (!IsDictionaryEntryType(dictionaryEntryExpression.Type))
        {
            return false;
        }

        switch (propertyName)
        {
            case nameof(KeyValuePair<int, int>.Key):
                memberExpression = Expression.Property(dictionaryEntryExpression, propertyName);
                return true;
            case nameof(KeyValuePair<int, int>.Value):
                memberExpression = GuardDictionaryValue(dictionaryEntryExpression);
                return true;
            default:
                return false;
        }
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

    /// <summary>
    /// Checks that a key can be folded at compile time and strips the parser budget checks from it.
    /// Accepts only: string, primitive or null constants; blocks without variables; unary and binary nodes
    /// over primitives without an operator method (including conversions between primitives); conditionals;
    /// instance members declared on <see cref="string"/>; and methods declared on <see cref="string"/>.
    /// Anything else rejects the key.
    /// </summary>
    private sealed class FoldableKeyVisitor : ExpressionVisitor
    {
        internal bool IsRejected { get; private set; }

        public override Expression Visit(Expression node)
        {
            if (IsRejected || node is null)
            {
                return node;
            }

            if (node is MethodCallExpression call && (call.Method == ThrowIfExceededMethod || call.Method == ThrowIfExceededImmediatelyMethod))
            {
                return Expression.Empty();
            }

            if (!IsFoldable(node))
            {
                IsRejected = true;
                return node;
            }

            return base.Visit(node);
        }

        private static bool IsFoldable(Expression node)
        {
            return node switch
            {
                ConstantExpression constant => constant.Value is null or string || constant.Value.GetType().IsPrimitive,
                BlockExpression block => block.Variables.Count == 0,
                UnaryExpression unary => unary.Method is null && unary.Operand is not null && unary.Operand.Type.IsPrimitive && unary.Type.IsPrimitive,
                BinaryExpression binary => binary.Method is null && binary.Conversion is null && binary.Left.Type.IsPrimitive && binary.Right.Type.IsPrimitive,
                ConditionalExpression => true,
                MemberExpression member => member.Expression is not null && member.Member.DeclaringType == typeof(string),
                MethodCallExpression call => call.Method.DeclaringType == typeof(string),
                _ => false
            };
        }
    }
}
