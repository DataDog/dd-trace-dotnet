// <copyright file="TruncatedStringWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb.BsonSerialization;

/// <summary>
/// Derived from StringWriter to stop writing at 5k characters since the UI/Backend would truncate anyways for tag key values:
/// https://docs.datadoghq.com/tracing/troubleshooting/#data-volume-guidelines
/// </summary>
public class TruncatedStringWriter : StringWriter
{
    private const int MaxLength = 5000;

    /// <summary>
    /// Writes a string to the current string.
    /// </summary>
    /// <param name="value">The string to write.</param>
    public override void Write(char value)
    {
        if (GetStringBuilder().Length < MaxLength)
        {
            base.Write(value);
        }
    }

    /// <summary>
    /// Writes a string to the current string.
    /// </summary>
    /// <param name="value">The string to write.</param>
    public override void Write(string value)
    {
        int remainingLength = MaxLength - GetStringBuilder().Length;

        if (remainingLength > 0)
        {
            base.Write(value.Substring(0, Math.Min(value.Length, remainingLength)));
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
        int remainingLength = MaxLength - GetStringBuilder().Length;

        if (remainingLength > 0)
        {
            base.Write(buffer, index, Math.Min(count, remainingLength));
        }
    }
}
