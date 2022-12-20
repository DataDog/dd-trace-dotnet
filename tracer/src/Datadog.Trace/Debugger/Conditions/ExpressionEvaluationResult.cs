// <copyright file="ExpressionEvaluationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.Conditions;

internal readonly struct ExpressionEvaluationResult
{
    public ExpressionEvaluationResult(bool succeeded, string expression = null, bool? condition = null, string[] errors = null)
    {
        Succeeded = succeeded;
        Expression = expression;
        Condition = condition;
        Errors = errors;
    }

    public string Expression { get; }

    public bool? Condition { get; }

    public bool Succeeded { get; }

    public string[] Errors { get; }
}
