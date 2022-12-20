// <copyright file="CompiledExpression.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq.Expressions;

namespace Datadog.Trace.Debugger.Conditions
{
    internal readonly record struct CompiledExpression<T>
    {
        public CompiledExpression(Func<ScopeMember, ScopeMember[], T> @delegate, Expression parsedExpression)
        {
            Delegate = @delegate;
            ParsedExpression = parsedExpression;
        }

        internal Func<ScopeMember, ScopeMember[], T> Delegate { get; }

        internal Expression ParsedExpression { get; }
    }
}
