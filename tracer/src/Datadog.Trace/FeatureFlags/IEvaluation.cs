// <copyright file="IEvaluation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.ComponentModel;

namespace Datadog.Trace.FeatureFlags;

/// <summary>FeatureFlag Evaluation result.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[Browsable(false)]
#if INTERNAL_FFE
internal interface IEvaluation
#else
public partial interface IEvaluation
#endif
{
    /// <summary> Gets the flag key being evaluate </summary>
    string FlagKey { get; }

    /// <summary> Gets the evaluation result </summary>
    object? Value { get; }

    /// <summary> Gets the evaluation result reason </summary>
    EvaluationReason Reason { get; }

    /// <summary> Gets the evaluation result variant </summary>
    string? Variant { get; }

    /// <summary> Gets the evaluation error message </summary>
    string? Error { get; }

    /// <summary> Gets the evaluation error code </summary>
    ErrorCode ErrorCode { get; }

    /// <summary> Gets the evaluation metadata </summary>
    IDictionary<string, string>? FlagMetadata { get; }
}
