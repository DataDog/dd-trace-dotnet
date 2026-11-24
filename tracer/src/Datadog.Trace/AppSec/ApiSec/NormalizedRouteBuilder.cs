// <copyright file="NormalizedRouteBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.AppSec.ApiSec;

/// <summary>
/// Builds the "_dd.appsec.normalized_route" tag, as defined by RFC-1103 (API Security - Normalized HTTP Route)
/// and its OpenAPI compliance amendment RFC-1125: the effective route of the request as an OpenAPI path template.
/// Each segment keeps its static text and its parameters, e.g. "/users/user-{id}" or "/photos/{id}.{format}".
/// The default value is disabled, and ignores every call.
/// </summary>
internal struct NormalizedRouteBuilder
{
    // Reused per thread: a builder is created and completed in the same synchronous call
    [ThreadStatic]
    private static List<string>? _declaredNamesCache;
    [ThreadStatic]
    private static List<NameSpan>? _replacedNamesCache;

    private readonly StringBuilder? _sb;

    // Number of segments of the request path (relative to the path base), or -1 when unknown
    private readonly int _requestSegmentCount;
    private readonly bool _requestEndsWithSlash;
    private int _segmentStart;
    private int _segmentIndex;
    private string? _pendingSeparator;
    private bool _absentTrailingCatchAll;

    // Parameter names, for the collision suffixes of RFC-1125
    private List<string>? _declaredNames;
    private List<NameSpan>? _replacedNames;

    private NormalizedRouteBuilder(StringBuilder sb, int requestSegmentCount, bool requestEndsWithSlash)
    {
        _sb = sb;
        _requestSegmentCount = requestSegmentCount;
        _requestEndsWithSlash = requestEndsWithSlash;
        _segmentStart = 0;
        _segmentIndex = -1;
        _pendingSeparator = null;
        _absentTrailingCatchAll = false;
        _declaredNames = null;
        _replacedNames = null;
    }

    public readonly bool IsEnabled => _sb is not null;

    /// <summary>
    /// Creates a builder when API Security is enabled, and a disabled builder otherwise.
    /// </summary>
    /// <param name="security">The security instance</param>
    /// <param name="pathBase">The mount prefix of the route, for example the ASP.NET Core PathBase</param>
    /// <param name="requestPath">The request path, relative to <paramref name="pathBase"/></param>
    public static NormalizedRouteBuilder Create(Security security, string? pathBase, string? requestPath)
        => security.AppsecEnabled && security.Settings.ApiSecurityEnabled ? Create(pathBase, requestPath) : default;

    /// <summary>
    /// Creates an enabled builder.
    /// </summary>
    /// <param name="pathBase">The mount prefix of the route, for example the ASP.NET Core PathBase</param>
    /// <param name="requestPath">The request path, relative to <paramref name="pathBase"/>, or null when unknown</param>
    [TestingAndPrivateOnly]
    public static NormalizedRouteBuilder Create(string? pathBase, string? requestPath = null)
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

        var requestSegmentCount = -1;
        var requestEndsWithSlash = false;
        if (requestPath is not null)
        {
            // The framework matches a request with or without the trailing slash, and fills the missing trailing
            // segments with the parameter defaults, so a segment is in the request when its index is below this count.
            var trimmed = requestPath.TrimEnd('/');
            requestEndsWithSlash = trimmed.Length < requestPath.Length;
            requestSegmentCount = 0;
            foreach (var c in trimmed)
            {
                if (c == '/')
                {
                    requestSegmentCount++;
                }
            }

            if (trimmed.Length > 0 && trimmed[0] != '/')
            {
                requestSegmentCount++;
            }
        }

        return new NormalizedRouteBuilder(sb, requestSegmentCount, requestEndsWithSlash);
    }

    public void StartSegment()
    {
        if (_sb is null)
        {
            return;
        }

        _segmentIndex++;
        _segmentStart = _sb.Length;
        _pendingSeparator = null;
        _sb.Append('/');
    }

    /// <summary>
    /// Adds static text to the current segment. The text is kept next to the parameters of the segment (RFC-1125).
    /// </summary>
    /// <param name="text">The static text</param>
    public void AddStaticText(string? text)
    {
        if (_sb is null || text is null)
        {
            return;
        }

        AppendPendingSeparator(_sb);
        AppendStaticConstant(_sb, text, 0, text.Length, preserveEscapes: false);
    }

    /// <summary>
    /// Adds the separator that precedes an optional parameter, e.g. the "." in "{filename}.{ext?}".
    /// The separator is kept only when the request contains the parameter that follows it.
    /// </summary>
    /// <param name="separator">The separator text</param>
    public void AddSeparator(string? separator)
    {
        if (_sb is null || separator is null)
        {
            return;
        }

        AppendPendingSeparator(_sb);
        _pendingSeparator = separator;
    }

    /// <summary>
    /// Adds a dynamic parameter "{name}" to the current segment. A parameter that the request may not contain is
    /// resolved against the request, and is left out, with its separator, when the request doesn't contain it (RFC-1125).
    /// </summary>
    /// <param name="name">The parameter name</param>
    /// <param name="value">The value matched for the request</param>
    /// <param name="isOptional">Whether the parameter is optional</param>
    /// <param name="isCatchAll">Whether the parameter is a catch-all</param>
    /// <param name="hasDefault">Whether the parameter has a default value, used when its segment is not in the request</param>
    public void AddParameter(string name, object? value, bool isOptional, bool isCatchAll = false, bool hasDefault = false)
    {
        if (_sb is null)
        {
            return;
        }

        if (_declaredNames is null)
        {
            _declaredNames = _declaredNamesCache ??= new List<string>();
            _declaredNames.Clear();
        }

        _declaredNames.Add(name);

        if (!IsInRequest(value, isOptional, isCatchAll, hasDefault))
        {
            _pendingSeparator = null;
            _absentTrailingCatchAll = isCatchAll;
            return;
        }

        AppendPendingSeparator(_sb);

        // Parameter names only contain [a-zA-Z0-9_], each other code point is replaced with "_" (RFC-1125)
        _sb.Append('{');
        var start = _sb.Length;
        var replaced = false;
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsAsciiLetterOrDigit(c) || c == '_')
            {
                _sb.Append(c);
                continue;
            }

            replaced = true;
            _sb.Append('_');
            if (char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
            {
                i++;
            }
        }

        if (replaced)
        {
            if (_replacedNames is null)
            {
                _replacedNames = _replacedNamesCache ??= new List<NameSpan>();
                _replacedNames.Clear();
            }

            _replacedNames.Add(new NameSpan(start, _sb.Length - start));
        }

        _sb.Append('}');
    }

    public void EndSegment()
    {
        if (_sb is null)
        {
            return;
        }

        _pendingSeparator = null;
        if (_sb.Length == _segmentStart + 1)
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
    [TestingAndPrivateOnly]
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

        if (_replacedNames is not null)
        {
            ResolveNameCollisions(_sb, _declaredNames!, _replacedNames);
        }

        if (_sb.Length == 0)
        {
            _sb.Append('/');
        }
        else if (template.EndsWith("/", StringComparison.Ordinal)
              || (_absentTrailingCatchAll && _requestEndsWithSlash))
        {
            // The trailing slash is kept only when the route declares it, or when the route ends with an empty
            // catch-all that follows a slash in the request: "/files/{*path}" hit with "/files/" is "/files/" (RFC-1125)
            _sb.Append('/');
        }

        return StringBuilderCache.GetStringAndRelease(_sb);
    }

    // A replaced name that equals another name of the route gets the smallest numeric suffix not used in the route,
    // in path order: "/a/{user-id}/{user_id}" is "/a/{user_id_2}/{user_id}" (RFC-1125)
    private static void ResolveNameCollisions(StringBuilder sb, List<string> declaredNames, List<NameSpan> replacedNames)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declared in declaredNames)
        {
            used.Add(declared);
        }

        var suffixes = new string?[replacedNames.Count];
        for (var i = 0; i < replacedNames.Count; i++)
        {
            var name = sb.ToString(replacedNames[i].Start, replacedNames[i].Length);
            if (used.Add(name))
            {
                continue;
            }

            for (var n = 2; ; n++)
            {
                var suffix = "_" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (used.Add(name + suffix))
                {
                    suffixes[i] = suffix;
                    break;
                }
            }
        }

        // Insert the suffixes from the end, so that the earlier offsets stay valid
        for (var i = replacedNames.Count - 1; i >= 0; i--)
        {
            if (suffixes[i] is { } suffix)
            {
                sb.Insert(replacedNames[i].Start + replacedNames[i].Length, suffix);
            }
        }
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

    private readonly bool IsInRequest(object? value, bool isOptional, bool isCatchAll, bool hasDefault)
    {
        if (isCatchAll)
        {
            // Every catch-all is optional, and absent when it matched an empty value or only "/" (RFC-1125)
            return value is not (null or string { Length: 0 } or "/");
        }

        if (isOptional)
        {
            return value is not (null or string { Length: 0 });
        }

        if (hasDefault)
        {
            // A value that only comes from the default is absent. When the request path is unknown, presence can't be
            // determined, so the parameter is left out (RFC-1125 fallback).
            return _segmentIndex < _requestSegmentCount && value is not (null or string { Length: 0 });
        }

        return true;
    }

    private void AppendPendingSeparator(StringBuilder sb)
    {
        if (_pendingSeparator is not null)
        {
            AppendStaticConstant(sb, _pendingSeparator, 0, _pendingSeparator.Length, preserveEscapes: false);
            _pendingSeparator = null;
        }
    }

    private readonly struct NameSpan
    {
        public readonly int Start;
        public readonly int Length;

        public NameSpan(int start, int length)
        {
            Start = start;
            Length = length;
        }
    }
}
