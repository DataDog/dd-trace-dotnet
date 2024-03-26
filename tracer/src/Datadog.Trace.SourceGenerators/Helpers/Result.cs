// <copyright file="Result.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.SourceGenerators.Helpers;

internal sealed record Result<TValue>
    where TValue : IEquatable<TValue>?
{
    public Result(TValue value, EquatableArray<DiagnosticInfo> errors)
    {
        Value = value;
        Errors = errors;
    }

    public TValue Value { get; }

    public EquatableArray<DiagnosticInfo> Errors { get; }
}
