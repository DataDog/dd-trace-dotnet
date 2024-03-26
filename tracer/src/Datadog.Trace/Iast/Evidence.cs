// <copyright file="Evidence.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Iast;

internal readonly struct Evidence
{
    private readonly string _value;
    private readonly Range[]? _ranges;
    private readonly Range[]? _sensitive;

    public Evidence(string value, Range[]? ranges = null, Range[]? sensitive = null)
    {
        this._value = value.SanitizeNulls();
        this._ranges = ranges;
        this._sensitive = sensitive;
    }

    // We only show value in the json when there are no valueParts
    public string? Value => _value;

    public Range[]? Ranges => _ranges;

    internal Range[]? Sensitive => _sensitive;

    public override int GetHashCode()
    {
        return IastUtils.GetHashCode(_value, _ranges);
    }
}
