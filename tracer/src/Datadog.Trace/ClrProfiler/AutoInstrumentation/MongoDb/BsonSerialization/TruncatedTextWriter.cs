// <copyright file="TruncatedTextWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Text;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb.BsonSerialization;

/// <summary>
/// Derived from TextWriter to stop writing at 5k characters since the UI/Backend would truncate anyways for tag key values:
/// https://docs.datadoghq.com/tracing/troubleshooting/#data-volume-guidelines
/// </summary>
internal class TruncatedTextWriter : TextWriter
{
    private static volatile UnicodeEncoding? _encoding;
    private readonly StringBuilder _sb;
    internal const int MaxLength = 5000;

    public TruncatedTextWriter(StringBuilder sb)
    {
        _sb = sb;
    }

    // Based on StringWriter implementation
    public override Encoding Encoding => _encoding ??= new UnicodeEncoding(false, false);

    /// <summary>
    /// Writes a string to the current string.
    /// </summary>
    /// <param name="value">The string to write.</param>
    public override void Write(char value)
    {
        if (_sb.Length < MaxLength)
        {
            _sb.Append(value);
        }
    }

    /// <summary>
    /// Writes a string to the current string.
    /// </summary>
    /// <param name="value">The string to write.</param>
    public override void Write(string? value)
    {
        if (value is not null)
        {
#if NETCOREAPP
            Write(value.AsSpan());
#else
            var remainingLength = MaxLength - _sb.Length;
            if (remainingLength > 0)
            {
                _sb.Append(value, 0, Math.Min(value.Length, remainingLength));
            }
#endif
        }
    }

    /// <summary>
    /// Writes a string to the current string.
    /// </summary>
    /// <param name="buffer">The character array to write data from.</param>
    /// <param name="index">The position in the buffer at which to start reading data.</param>
    /// <param name="count">The maximum number of characters to write.</param>
    public override void Write(char[] buffer, int index, int count)
    {
        var remainingLength = MaxLength - _sb.Length;

        if (remainingLength > 0)
        {
            _sb.Append(buffer, index, Math.Min(count, remainingLength));
        }
    }

#if NETCOREAPP
    public override void Write(ReadOnlySpan<char> buffer)
    {
        var remainingLength = MaxLength - _sb.Length;

        if (remainingLength > 0)
        {
            var charsToWrite = Math.Min(buffer.Length, remainingLength);
            _sb.Append(buffer.Slice(0, charsToWrite));
        }
    }
#endif

    /// <summary>Returns a string containing the characters written to the current <see langword="TruncatedTextWriter" /> so far.</summary>
    /// <returns>The string containing the characters written to the current <see langword="TruncatedTextWriter" />.</returns>
    public override string ToString() => _sb.ToString();
}
