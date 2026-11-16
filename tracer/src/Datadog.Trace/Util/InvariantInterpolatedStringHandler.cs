// <copyright file="InvariantInterpolatedStringHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;
using System.Runtime.CompilerServices;
#if !NET6_0_OR_GREATER
using System.Text;
using Datadog.Trace.Util;
#endif

// ReSharper disable once CheckNamespace - Lives alongside StringUtil.Invariant() in the System namespace
namespace System;

/// <summary>
/// An interpolated string handler that formats every hole using <see cref="CultureInfo.InvariantCulture"/>.
/// Use it via <see cref="StringUtil.Invariant"/>, e.g. <c>StringUtil.Invariant($"http://{host}:{port}")</c>.
/// On .NET 6+ this wraps <c>DefaultInterpolatedStringHandler</c>, so it formats directly into a pooled
/// buffer; on older frameworks it uses a cached <c>StringBuilder</c>.
/// </summary>
[InterpolatedStringHandler]
internal ref struct InvariantInterpolatedStringHandler
{
#if NET6_0_OR_GREATER
    private DefaultInterpolatedStringHandler _handler;

    public InvariantInterpolatedStringHandler(int literalLength, int formattedCount)
    {
        _handler = new DefaultInterpolatedStringHandler(literalLength, formattedCount, CultureInfo.InvariantCulture);
    }

    public void AppendLiteral(string value) => _handler.AppendLiteral(value);

    public void AppendFormatted<T>(T value) => _handler.AppendFormatted(value);

    public void AppendFormatted<T>(T value, string? format) => _handler.AppendFormatted(value, format);

    public void AppendFormatted(string? value) => _handler.AppendFormatted(value);

    public void AppendFormatted(ReadOnlySpan<char> value) => _handler.AppendFormatted(value);

    public string ToStringAndClear() => _handler.ToStringAndClear();
#else
    private readonly StringBuilder _builder;

    public InvariantInterpolatedStringHandler(int literalLength, int formattedCount)
    {
        // Assume each hole is roughly the length of a formatted int
        _builder = StringBuilderCache.Acquire(literalLength + (formattedCount * 11));
    }

    public void AppendLiteral(string value) => _builder.Append(value);

    // Non-generic overloads for the most common holes, so they don't box on .NET Framework
    public void AppendFormatted(int value) => _builder.Append(value.ToString(CultureInfo.InvariantCulture));

    public void AppendFormatted(long value) => _builder.Append(value.ToString(CultureInfo.InvariantCulture));

    public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

    public void AppendFormatted<T>(T value, string? format)
    {
        if (value is IFormattable formattable)
        {
            _builder.Append(formattable.ToString(format, CultureInfo.InvariantCulture));
        }
        else if (value is not null)
        {
            _builder.Append(value.ToString());
        }
    }

    public void AppendFormatted(string? value) => _builder.Append(value);

    public void AppendFormatted(ReadOnlySpan<char> value)
    {
#if NETCOREAPP2_1_OR_GREATER
        _builder.Append(value);
#else
        _builder.Append(value.ToString());
#endif
    }

    public string ToStringAndClear() => StringBuilderCache.GetStringAndRelease(_builder);
#endif
}
