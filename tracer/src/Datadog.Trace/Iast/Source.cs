// <copyright file="Source.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Util;

namespace Datadog.Trace.Iast;

internal class Source
{
    private const string RedactedSensitiveBuffer = "****************";
    private const string RedactedSourceBuffer = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly SourceType _origin;
    private int _internalId;
    private string? _value;
    private bool _sensitive;
    private bool _redacted;
    private string? _redactedValue;

    public Source(byte origin, string? name, string? value)
        : this((SourceType)origin, name, value)
    {
    }

    public Source(SourceType origin, string? name, string? value)
    {
        this._origin = origin;
        this.Name = name;
        this._value = value.SanitizeNulls();
        this._redacted = false;
    }

    public SourceType Origin => _origin;

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

    internal static string RedactString(string value)
    {
        return NewString(ComputeLength(value), RedactedSensitiveBuffer);
    }

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
        var result = StringBuilderCache.Acquire(length);
        int remaining = length;
        while (remaining > 0)
        {
            int next = Math.Min(remaining, buffer.Length);
            result.Append(buffer, 0, next);
            remaining -= next;
        }

        return StringBuilderCache.GetStringAndRelease(result);
    }
}
