// <copyright file="EncodingHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util;

internal static class EncodingHelpers
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EncodingHelpers));
    internal static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static bool TryGetWellKnownCharset(ReadOnlySpan<char> secondPart, [NotNullWhen(true)] out Encoding? encoding)
    {
        if (secondPart.Equals("utf-8".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            encoding = Utf8NoBom;
            return true;
        }

        if (secondPart.Equals("us-ascii".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            encoding = Encoding.ASCII;
            return true;
        }

        encoding = null;
        return false;
    }

    public static bool TryGetFromCharset(string charset, [NotNullWhen(true)] out Encoding? encoding)
    {
        if (string.IsNullOrEmpty(charset))
        {
            encoding = null;
            return false;
        }

        try
        {
            encoding = Encoding.GetEncoding(charset);
            return true;
        }
        catch (Exception)
        {
            // GetEncoding can throw ArgumentException if the encoding is not supported
            // so just fallback to default
            Log.Debug("Error decoding charset, could not find an encoding for: {Charset}", charset);
            encoding = null;
            return false;
        }
    }
}
