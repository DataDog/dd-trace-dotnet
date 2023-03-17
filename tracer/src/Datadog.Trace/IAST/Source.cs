// <copyright file="Source.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast;

internal class Source
{
    private readonly byte _origin;
    private int _internalId;

    public Source(byte origin, string? name, string? value)
    {
        this._origin = origin;
        this.Name = name;
        this.Value = value;
    }

    public string Origin => SourceType.GetString(_origin);

    public string? Name { get; }

    public string? Value { get; }

    public void SetInternalId(int id)
    {
        _internalId = id;
    }

    public int GetInternalId()
    {
        return _internalId;
    }

    public override int GetHashCode()
    {
        return IastUtils.GetHashCode(_origin, Name, Value);
    }

    public override bool Equals(object? obj) => GetHashCode() == obj?.GetHashCode();
}
