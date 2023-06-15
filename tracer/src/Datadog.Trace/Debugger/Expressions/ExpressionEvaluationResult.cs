// <copyright file="ExpressionEvaluationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Debugger.Models;

namespace Datadog.Trace.Debugger.Expressions;

internal ref struct ExpressionEvaluationResult
{
    internal string Template { get; set; }

    internal bool? Condition { get; set; }

    internal double? Metric { get; set; }

    internal DecorationResult[] Decorations { get; set; }

    internal List<EvaluationError> Errors { get; set; }

    internal bool HasError => Errors is { Count: > 0 };

    internal bool IsNull()
    {
        return Template == null && Condition == null && Metric == null && Decorations?.Length == 0 && Errors == null;
    }

    internal struct DecorationResult
    {
        public string TagName { get; set; }

        public string Value { get; set; }

        public EvaluationError[] Errors { get; set; }
    }
}
