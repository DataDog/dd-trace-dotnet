// <copyright file="NormalizedRouteBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
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
    private static List<Parameter>? _parametersCache;

    private readonly StringBuilder? _sb;

    // Number of segments of the request path (relative to the path base), or -1 when unknown
    private readonly int _requestSegmentCount;
    private readonly bool _requestEndsWithSlash;
    private int _segmentStart;
    private int _segmentIndex;
    private string? _pendingSeparator;
    private bool _absentTrailingCatchAll;

    // Every declared parameter, for the collision suffixes of RFC-1125
    private List<Parameter>? _parameters;
    private bool _hasReplacedName;

    private NormalizedRouteBuilder(StringBuilder sb, int requestSegmentCount, bool requestEndsWithSlash)
    {
        _sb = sb;
        _requestSegmentCount = requestSegmentCount;
        _requestEndsWithSlash = requestEndsWithSlash;
        _segmentStart = 0;
        _segmentIndex = -1;
        _pendingSeparator = null;
        _absentTrailingCatchAll = false;
        _parameters = null;
        _hasReplacedName = false;
    }

    public readonly bool IsEnabled => _sb is not null;

    /// <summary>
    /// Gets whether the normalized route is computed, so that callers don't compute its inputs otherwise.
    /// </summary>
    /// <param name="security">The security instance</param>
    /// <returns>Whether AppSec and API Security are enabled</returns>
    public static bool IsEnabledFor(Security security) => security.AppsecEnabled && security.Settings.ApiSecurityEnabled;

    /// <summary>
    /// Creates a builder when API Security is enabled, and a disabled builder otherwise.
    /// </summary>
    /// <param name="security">The security instance</param>
    /// <param name="pathBase">The mount prefix of the route, for example the ASP.NET Core PathBase</param>
    /// <param name="requestPath">The request path, relative to <paramref name="pathBase"/></param>
    public static NormalizedRouteBuilder Create(Security security, string? pathBase, string? requestPath)
        => IsEnabledFor(security) ? Create(pathBase, requestPath, requestPathStart: 0, requestPathSuffix: null) : default;

    /// <summary>
    /// Creates a builder when API Security is enabled, and a disabled builder otherwise.
    /// The request path is <paramref name="requestPath"/> from <paramref name="requestPathStart"/>, followed by
    /// <paramref name="requestPathSuffix"/>, so that callers don't allocate it.
    /// </summary>
    /// <param name="security">The security instance</param>
    /// <param name="pathBase">The mount prefix of the route</param>
    /// <param name="requestPath">The string that contains the request path, or null when unknown</param>
    /// <param name="requestPathStart">The index where the request path, relative to <paramref name="pathBase"/>, starts</param>
    /// <param name="requestPathSuffix">The rest of the request path, for example the ASP.NET PathInfo</param>
    public static NormalizedRouteBuilder Create(Security security, string? pathBase, string? requestPath, int requestPathStart, string? requestPathSuffix)
        => IsEnabledFor(security) ? Create(pathBase, requestPath, requestPathStart, requestPathSuffix) : default;

    /// <summary>
    /// Creates an enabled builder.
    /// </summary>
    /// <param name="pathBase">The mount prefix of the route, for example the ASP.NET Core PathBase</param>
    /// <param name="requestPath">The request path, relative to <paramref name="pathBase"/>, or null when unknown</param>
    [TestingAndPrivateOnly]
    public static NormalizedRouteBuilder Create(string? pathBase, string? requestPath = null)
        => Create(pathBase, requestPath, requestPathStart: 0, requestPathSuffix: null);

    private static NormalizedRouteBuilder Create(string? pathBase, string? requestPath, int requestPathStart, string? requestPathSuffix)
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
            CountSegments(requestPath, requestPathStart, requestPathSuffix ?? string.Empty, out requestSegmentCount, out requestEndsWithSlash);
        }

        return new NormalizedRouteBuilder(sb, requestSegmentCount, requestEndsWithSlash);
    }

    // The frameworks match a request with or without its trailing slash, and fill the missing trailing segments with
    // the parameter defaults, so a route segment is in the request when its index is below the segment count.
    // The path is path[start..] followed by suffix.
    private static void CountSegments(string path, int start, string suffix, out int segmentCount, out bool endsWithSlash)
    {
        var pathLength = Math.Max(path.Length - start, 0);
        var length = pathLength + suffix.Length;
        var end = length;
        while (end > 0 && CharAt(path, start, pathLength, suffix, end - 1) == '/')
        {
            end--;
        }

        endsWithSlash = end < length;
        segmentCount = end > 0 && CharAt(path, start, pathLength, suffix, 0) != '/' ? 1 : 0;
        for (var i = 0; i < end; i++)
        {
            if (CharAt(path, start, pathLength, suffix, i) == '/')
            {
                segmentCount++;
            }
        }

        static char CharAt(string path, int start, int pathLength, string suffix, int index)
            => index < pathLength ? path[start + index] : suffix[index - pathLength];
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
        if (text is not null)
        {
            AddStaticText(text, 0, text.Length);
        }
    }

    /// <summary>
    /// Adds the static text between <paramref name="start"/> (inclusive) and <paramref name="end"/> (exclusive) to the current segment.
    /// </summary>
    /// <param name="text">The text that contains the static text</param>
    /// <param name="start">The index of the first character</param>
    /// <param name="end">The index after the last character</param>
    public void AddStaticText(string text, int start, int end)
    {
        if (_sb is null || start >= end)
        {
            return;
        }

        AppendPendingSeparator(_sb);
        AppendStaticConstant(_sb, text, start, end, preserveEscapes: false);
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
    public void AddParameter(string name, object? value, bool isOptional, bool isCatchAll, bool hasDefault)
    {
        if (_sb is null)
        {
            return;
        }

        if (_parameters is null)
        {
            _parameters = _parametersCache ??= [];
            _parameters.Clear();
        }

        var isReplaced = !IsValidName(name);
        _hasReplacedName |= isReplaced;

        if (!IsInRequest(value, isOptional, isCatchAll, hasDefault))
        {
            // The collision suffixes are resolved over every declared parameter, so that a parameter keeps its name
            // whether the previous ones are in the request or not
            _parameters.Add(new Parameter(name, isReplaced, start: -1));
            _pendingSeparator = null;
            _absentTrailingCatchAll = isCatchAll;
            return;
        }

        AppendPendingSeparator(_sb);
        _sb.Append('{');
        _parameters.Add(new Parameter(name, isReplaced, _sb.Length));
        AppendName(_sb, name);
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

        if (_hasReplacedName && _parameters is { } parameters)
        {
            ResolveNameCollisions(_sb, parameters);
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

    // Parameter names only contain [a-zA-Z0-9_]; each other code point is replaced with "_" (RFC-1125).
    // A replaced name that equals another name of the route gets the smallest numeric suffix not used in the route,
    // in path order: "/a/{user-id}/{user_id}" is "/a/{user_id_2}/{user_id}". Names that are already valid keep them.
    private static void ResolveNameCollisions(StringBuilder sb, List<Parameter> parameters)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            if (!parameter.IsReplaced)
            {
                used.Add(parameter.Name);
            }
        }

        // Positions in the builder and suffixes of the parameters that are in the request and get a suffix
        var insertions = new List<KeyValuePair<int, string>>();
        foreach (var parameter in parameters)
        {
            if (!parameter.IsReplaced)
            {
                continue;
            }

            var name = SanitizeName(parameter.Name);
            if (used.Add(name))
            {
                continue;
            }

            for (var n = 2; ; n++)
            {
                var suffix = "_" + n.ToString(CultureInfo.InvariantCulture);
                if (used.Add(name + suffix))
                {
                    if (parameter.Start >= 0)
                    {
                        insertions.Add(new KeyValuePair<int, string>(parameter.Start + name.Length, suffix));
                    }

                    break;
                }
            }
        }

        // Insert the suffixes from the end, so that the earlier positions stay valid
        for (var i = insertions.Count - 1; i >= 0; i--)
        {
            sb.Insert(insertions[i].Key, insertions[i].Value);
        }
    }

    private static bool IsValidName(string name)
    {
        foreach (var c in name)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static void AppendName(StringBuilder sb, string name)
    {
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsAsciiLetterOrDigit(c) || c == '_')
            {
                sb.Append(c);
                continue;
            }

            sb.Append('_');
            if (char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
            {
                i++;
            }
        }
    }

    private static string SanitizeName(string name)
    {
        var sb = StringBuilderCache.Acquire();
        AppendName(sb, name);
        return StringBuilderCache.GetStringAndRelease(sb);
    }

    // Every catch-all is optional, and absent when it matched an empty value or only slashes (RFC-1125)
    private static bool IsEmptyCatchAllValue(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string s)
        {
            return false;
        }

        foreach (var c in s)
        {
            if (c != '/')
            {
                return false;
            }
        }

        return true;
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
        // A value that only comes from the default is absent. When the request path is unknown, presence can't be
        // determined, so the parameter is left out (RFC-1125 fallback).
        if (hasDefault && _segmentIndex >= _requestSegmentCount)
        {
            return false;
        }

        if (isCatchAll)
        {
            return !IsEmptyCatchAllValue(value);
        }

        return !(isOptional || hasDefault) || value is not (null or string { Length: 0 });
    }

    private void AppendPendingSeparator(StringBuilder sb)
    {
        if (_pendingSeparator is not null)
        {
            AppendStaticConstant(sb, _pendingSeparator, 0, _pendingSeparator.Length, preserveEscapes: false);
            _pendingSeparator = null;
        }
    }

    private readonly struct Parameter
    {
        public readonly string Name;
        public readonly bool IsReplaced;

        // Position of the name in the builder, or -1 when the parameter is not in the request
        public readonly int Start;

        public Parameter(string name, bool isReplaced, int start)
        {
            Name = name;
            IsReplaced = isReplaced;
            Start = start;
        }
    }
}
