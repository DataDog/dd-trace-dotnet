// <copyright file="ExpressionEvaluationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Debugger.Models;

namespace Datadog.Trace.Debugger.Expressions;

internal ref struct ExpressionEvaluationResult
{
    public string Template { get; set; }

    public bool? Condition { get; set; }

    public double? Metric { get; set; }

    public List<EvaluationError> Errors { get; set; }
}
