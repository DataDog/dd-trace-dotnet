// <copyright file="CompiledExpression.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq.Expressions;
using Datadog.Trace.Debugger.Models;

namespace Datadog.Trace.Debugger.Expressions
{
    internal readonly record struct CompiledExpression<T>
    {
        internal CompiledExpression(
            Func<ScopeMember, ScopeMember, ScopeMember, Exception, ScopeMember[], T> @delegate,
            Expression parsedExpression,
            string rawExpression,
            EvaluationError[] errors)
        {
            Delegate = @delegate;
            ParsedExpression = parsedExpression;
            RawExpression = rawExpression;
            Errors = errors;
        }

        internal Func<ScopeMember, ScopeMember, ScopeMember, Exception, ScopeMember[], T> Delegate { get; }

        internal Expression ParsedExpression { get; }

        internal string RawExpression { get; }

        internal EvaluationError[] Errors { get; }
    }
}
