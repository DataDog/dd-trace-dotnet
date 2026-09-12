// <copyright file="Evaluation.cs" company="Datadog">
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

internal sealed class Evaluation(string flagKey, object? value, EvaluationReason reason, string? variant = null, string? error = null, ErrorCode errorCode = ErrorCode.None, IDictionary<string, string>? metadata = null)
    : IEvaluation
{
    public string FlagKey { get; } = flagKey;

    public object? Value { get; } = value;

    public EvaluationReason Reason { get; } = reason;

    public string? Variant { get; } = variant;

    public string? Error { get; } = error;

    public ErrorCode ErrorCode { get; } = errorCode;

    public IDictionary<string, string>? FlagMetadata { get; } = metadata;
}
