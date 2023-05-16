// <copyright file="ValuePart.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast;

internal readonly struct ValuePart
{
    public ValuePart(int? source)
    {
        this.Value = null; // Redacted value
        this.Source = source;
    }

    public ValuePart(string value, int? source)
    {
        this.Value = value;
        this.Source = source;
    }

    public string? Value { get; }

    public int? Source { get; }
}
