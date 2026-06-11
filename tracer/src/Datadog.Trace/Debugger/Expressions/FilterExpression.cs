// <copyright file="FilterExpression.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Datadog.Trace.Debugger.Expressions;

internal sealed class FilterExpression : Expression
{
    internal FilterExpression(Expression source, LambdaExpression predicate, Type iteratorType, bool isDictionary)
    {
        Source = source;
        Predicate = predicate;
        IteratorType = iteratorType;
        IsDictionary = isDictionary;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;

    public override Type Type => typeof(IEnumerable<>).MakeGenericType(IteratorType);

    public override bool CanReduce => true;

    internal Expression Source { get; }

    internal LambdaExpression Predicate { get; }

    internal Type IteratorType { get; }

    internal bool IsDictionary { get; }

    public override Expression Reduce()
    {
        var genericWhere = ProbeExpressionParserHelper.GetMethodByReflection(typeof(Enumerable), nameof(Enumerable.Where), [typeof(IEnumerable<>), typeof(Func<,>)], [IteratorType]);
        return Expression.Call(null, genericWhere, Source, Predicate);
    }
}
