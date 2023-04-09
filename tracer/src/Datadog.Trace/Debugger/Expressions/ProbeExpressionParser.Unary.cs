// <copyright file="ProbeExpressionParser.Unary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq.Expressions;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Expressions;

internal partial class ProbeExpressionParser<T>
{
    private Expression Not(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        var ex = ParseTree(reader, parameters, itParameter);
        return Expression.Not(ex);
    }
}
