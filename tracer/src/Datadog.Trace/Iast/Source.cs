// <copyright file="Source.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;

namespace Datadog.Trace.Iast;

internal class Source
{
    private const string RedactedSensitiveBuffer = "****************";
    private const string RedactedSourceBuffer = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly byte _origin;
    private int _internalId;
    private string? _value;
    private bool _sensitive;
    private bool _redacted;
    private string? _redactedValue;

    public Source(byte origin, string? name, string? value)
    {
        this._origin = origin;
        this.Name = name;
        this._value = value;
        this._redacted = false;
    }

    public byte OriginByte => _origin;

    public string Origin => SourceType.GetString(_origin);

    public string? Name { get; }

    public string? Value => _value;

    internal bool IsSensitive => _sensitive;

    internal bool IsRedacted => _redacted;

    public string? RedactedValue => _redactedValue;

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

    internal void MarkAsSensitive()
    {
        _sensitive = true;
        MarkAsRedacted();
    }

    internal void MarkAsRedacted()
    {
        if (_redacted) { return; }
        _redacted = true;
        if (_value != null)
        {
            _redactedValue = NewString(ComputeLength(_value), RedactedSourceBuffer);
        }
    }

    internal string RedactString(string value)
    {
        return NewString(ComputeLength(value), RedactedSensitiveBuffer);
    }

    private static int ComputeLength(string value)
    {
        if (value == null || value == string.Empty)
        {
            return 0;
        }

        int size = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!char.IsHighSurrogate(c))
            {
                size++;
            }
        }

        return size;
    }

    private static string NewString(int length, string buffer)
    {
        var result = new StringBuilder(length);
        int remaining = length;
        while (remaining > 0)
        {
            int next = Math.Min(remaining, buffer.Length);
            result.Append(buffer, 0, next);
            remaining -= next;
        }

        return result.ToString();
    }
}
