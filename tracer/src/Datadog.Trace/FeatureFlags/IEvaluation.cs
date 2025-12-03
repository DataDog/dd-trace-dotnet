// <copyright file="IEvaluation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Datadog.Trace.FeatureFlags;

/// <summary>FeatureFlag Evaluation result.</summary>
public partial interface IEvaluation
{
    /// <summary> Gets the evaluation result </summary>
    object? Value { get; }

    /// <summary> Gets the evaluation result reason </summary>
    EvaluationReason Reason { get; }

    /// <summary> Gets the evaluation result variant </summary>
    string? Variant { get; }

    /// <summary> Gets the evaluation error </summary>
    string? Error { get; }

    /// <summary> Gets the evaluation metadata </summary>
    IDictionary<string, string>? Metadata { get; }
}
