// <copyright file="NormalizedRouteBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Text;
using Datadog.Trace.Util;

namespace Datadog.Trace.AppSec.ApiSec;

/// <summary>
/// Builds the "_dd.appsec.normalized_route" tag, as defined by RFC-1103 (API Security - Normalized HTTP Route):
/// a sequence of slash-separated atomic elements, where each URL segment is either a static constant or a single
/// dynamic parameter "{name}". Several parameters in the same segment are combined as "{a+b}".
/// The default value is disabled, and ignores every call.
/// </summary>
internal struct NormalizedRouteBuilder
{
    private readonly StringBuilder? _sb;
    private int _segmentStart;
    private bool _hasParameter;

    private NormalizedRouteBuilder(StringBuilder sb)
    {
        _sb = sb;
        _segmentStart = 0;
        _hasParameter = false;
    }

    public readonly bool IsEnabled => _sb is not null;

    /// <summary>
    /// Creates a builder when API Security is enabled, and a disabled builder otherwise.
    /// </summary>
    /// <param name="security">The security instance</param>
    /// <param name="pathBase">The mount prefix of the route, for example the ASP.NET Core PathBase</param>
    public static NormalizedRouteBuilder Create(Security security, string? pathBase)
        => security.AppsecEnabled && security.Settings.ApiSecurityEnabled ? Create(pathBase) : default;

    /// <summary>
    /// Creates an enabled builder.
    /// </summary>
    /// <param name="pathBase">The mount prefix of the route, for example the ASP.NET Core PathBase</param>
    public static NormalizedRouteBuilder Create(string? pathBase)
    {
        var sb = StringBuilderCache.Acquire();

        // The normalized route is the path as served to the caller, so it includes the mount prefix.
        // The PathBase keeps the escapes of the request path, so don't encode them again.
        // Empty elements are not permitted (rule 2), so "//" in the PathBase is reported as "/".
        if (pathBase is not null)
        {
            var start = 0;
            for (var i = 0; i <= pathBase.Length; i++)
            {
                if (i == pathBase.Length || pathBase[i] == '/')
                {
                    if (i > start)
                    {
                        sb.Append('/');
                        AppendStaticConstant(sb, pathBase, start, i, preserveEscapes: true);
                    }

                    start = i + 1;
                }
            }
        }

        return new NormalizedRouteBuilder(sb);
    }

    public void StartSegment()
    {
        if (_sb is null)
        {
            return;
        }

        _segmentStart = _sb.Length;
        _hasParameter = false;
        _sb.Append('/');
    }

    /// <summary>
    /// Adds static text to the current segment. Static text next to a parameter is folded into the parameter (rule 5).
    /// </summary>
    /// <param name="text">The static text</param>
    public void AddStaticText(string? text)
    {
        if (_sb is null || _hasParameter || text is null)
        {
            return;
        }

        AppendStaticConstant(_sb, text, 0, text.Length, preserveEscapes: false);
    }

    /// <summary>
    /// Adds a dynamic parameter to the current segment. An optional parameter is resolved against the value
    /// the framework matched for the request, and is left out when the request doesn't contain it.
    /// </summary>
    /// <param name="name">The parameter name</param>
    /// <param name="value">The value matched for the request</param>
    /// <param name="isOptional">Whether the request may not contain the parameter, e.g. an optional or catch-all parameter</param>
    public void AddParameter(string name, object? value, bool isOptional)
    {
        if (_sb is null || (isOptional && value is null or string { Length: 0 }))
        {
            return;
        }

        if (_hasParameter)
        {
            _sb.Append('+');
        }
        else
        {
            _sb.Length = _segmentStart + 1;
            _sb.Append('{');
            _hasParameter = true;
        }

        // Parameter names may contain any character except "/?#+{}", which are percent-encoded (rule 4)
        foreach (var c in name)
        {
            if (c is '/' or '?' or '#' or '+' or '{' or '}')
            {
                AppendPercentEncoded(_sb, c);
            }
            else
            {
                _sb.Append(c);
            }
        }
    }

    public void EndSegment()
    {
        if (_sb is null)
        {
            return;
        }

        if (_hasParameter)
        {
            _sb.Append('}');
        }
        else if (_sb.Length == _segmentStart + 1)
        {
            // Every element of the segment was optional and absent from the request
            _sb.Length = _segmentStart;
        }
    }

    /// <summary>
    /// Sets the normalized route on the span, and releases the builder.
    /// </summary>
    /// <param name="span">The span</param>
    /// <param name="template">The route template, as declared</param>
    public readonly void SetTag(Span span, string? template)
    {
        if (Complete(template) is { } normalizedRoute)
        {
            span.Tags.SetTag(Tags.AppSecNormalizedRoute, normalizedRoute);
        }
    }

    /// <summary>
    /// Gets the normalized route, and releases the builder.
    /// </summary>
    /// <param name="template">The route template, as declared</param>
    /// <returns>The normalized route, or null when the builder is disabled or there is no template</returns>
    public readonly string? Complete(string? template)
    {
        if (_sb is null)
        {
            return null;
        }

        if (template is null)
        {
            StringBuilderCache.Release(_sb);
            return null;
        }

        if (_sb.Length == 0)
        {
            _sb.Append('/');
        }
        else if (template.EndsWith("/", StringComparison.Ordinal))
        {
            // The trailing slash is kept only when the route declares it (rule 1)
            _sb.Append('/');
        }

        return StringBuilderCache.GetStringAndRelease(_sb);
    }

    // Static constants only contain [A-Za-z0-9.-~_]; any other character is percent-encoded (rule 3)
    private static void AppendStaticConstant(StringBuilder sb, string value, int start, int end, bool preserveEscapes)
    {
        for (var i = start; i < end; i++)
        {
            var c = value[i];
            if (char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '~' or '_')
            {
                sb.Append(c);
            }
            else if (preserveEscapes && c == '%' && i + 2 < end && char.IsAsciiHexDigit(value[i + 1]) && char.IsAsciiHexDigit(value[i + 2]))
            {
                sb.Append('%').Append(char.ToUpperInvariant(value[i + 1])).Append(char.ToUpperInvariant(value[i + 2]));
                i += 2;
            }
            else if (c < 0x80)
            {
                AppendPercentEncoded(sb, c);
            }
            else
            {
                // Encode the run of non-ASCII characters as UTF-8
                var runEnd = i + 1;
                while (runEnd < end && value[runEnd] >= 0x80)
                {
                    runEnd++;
                }

                AppendPercentEncodedUtf8(sb, value, i, runEnd - i);
                i = runEnd - 1;
            }
        }
    }

    private static void AppendPercentEncodedUtf8(StringBuilder sb, string value, int start, int length)
    {
#if NETCOREAPP3_1_OR_GREATER
        var chars = value.AsSpan(start, length);
        var maxByteCount = Encoding.UTF8.GetMaxByteCount(length);
        if (maxByteCount <= 256)
        {
            Span<byte> bytes = stackalloc byte[maxByteCount];
            var byteCount = Encoding.UTF8.GetBytes(chars, bytes);
            foreach (var b in bytes.Slice(0, byteCount))
            {
                AppendPercentEncoded(sb, b);
            }

            return;
        }
#endif

        foreach (var b in Encoding.UTF8.GetBytes(value.Substring(start, length)))
        {
            AppendPercentEncoded(sb, b);
        }
    }

    private static void AppendPercentEncoded(StringBuilder sb, int b)
        => sb.Append('%').Append(HexConverter.ToCharUpper(b >> 4)).Append(HexConverter.ToCharUpper(b));
}
