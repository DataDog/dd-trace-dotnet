// <copyright file="WafAttributeRedaction.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using Datadog.Trace.Util;

namespace Datadog.Trace.AppSec.Waf;

/// <summary>
/// Redacts the WAF output attributes that copy a URL the application called or was redirected to.
/// The shared ruleset copies them raw, so credentials in them would reach the span (APPSEC-71557).
/// </summary>
internal static class WafAttributeRedaction
{
    internal static object? Redact(string key, object? value)
        => value is string url && IsUrlAttribute(key) ? RemoveUrlSecrets(url) : value;

    internal static bool IsUrlAttribute(string key)
        => key is "api.security.large_response.url" or "api.security.redirection.move_target" or "api.security.redirection.redirect_target";

    /// <summary>
    /// Removes the user-info, query and fragment of a URL, which may be relative (e.g. a Location header).
    /// When the user-info can't be told apart from the rest of the authority, everything from the authority on is dropped.
    /// </summary>
    internal static string RemoveUrlSecrets(string url)
    {
        var authorityStart = GetAuthorityStart(url);
        if (authorityStart >= 0)
        {
            var authorityEnd = url.IndexOfAny(Separators.UrlAuthorityEnd, authorityStart);
            if (authorityEnd < 0)
            {
                if (MayBeTruncated(url))
                {
                    return url.Substring(0, authorityStart);
                }

                authorityEnd = url.Length;
            }

            var userInfoEnd = authorityEnd > authorityStart ? url.LastIndexOf('@', authorityEnd - 1, authorityEnd - authorityStart) : -1;
            if (userInfoEnd >= 0)
            {
                // a '?' or '#' before the '@' is either a raw one in a password or a query/fragment holding an '@':
                // neither reading is safe to keep, so drop the authority and everything after it
                if (url.IndexOfAny(Separators.QueryOrFragment, authorityStart, userInfoEnd - authorityStart) >= 0)
                {
                    return url.Substring(0, authorityStart);
                }

                url = url.Remove(authorityStart, userInfoEnd + 1 - authorityStart);
            }
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);
        }

        var end = url.IndexOfAny(Separators.QueryOrFragment);
        return end < 0 ? url : url.Substring(0, end);
    }

    /// <summary>
    /// Returns the index the authority starts at, or -1 for a URL without one.
    /// Like browsers (WHATWG), it accepts '\' for '/', and for http(s) any run of slashes, tabs and newlines, even none.
    /// Other schemes and scheme-relative URLs need at least two slashes.
    /// </summary>
    private static int GetAuthorityStart(string url)
    {
        var schemeEnd = 0;
        while (schemeEnd < url.Length && IsSchemeChar(url[schemeEnd], first: schemeEnd == 0))
        {
            schemeEnd++;
        }

        var start = schemeEnd > 0 && schemeEnd < url.Length && url[schemeEnd] == ':' ? schemeEnd + 1 : 0;
        if (start > 0 && IsHttpScheme(url, schemeEnd))
        {
            return SkipSlashes(url, start);
        }

        return start + 1 < url.Length && IsSlash(url[start]) && IsSlash(url[start + 1]) ? SkipSlashes(url, start) : -1;
    }

    private static int SkipSlashes(string url, int index)
    {
        while (index < url.Length && (IsSlash(url[index]) || url[index] is '\t' or '\n' or '\r'))
        {
            index++;
        }

        return index;
    }

    private static bool IsHttpScheme(string url, int schemeEnd)
        => (schemeEnd == 4 || (schemeEnd == 5 && url[4] is 's' or 'S'))
        && string.Compare(url, 0, "http", 0, 4, StringComparison.OrdinalIgnoreCase) == 0;

    private static bool IsSchemeChar(char c, bool first)
        => char.IsAsciiLetter(c) || (!first && (char.IsAsciiDigit(c) || c is '+' or '-' or '.'));

    private static bool IsSlash(char c) => c is '/' or '\\';

    /// <summary>
    /// Whether the encoder may have cut the string at <see cref="WafConstants.MaxStringLength"/>, counted in chars or in UTF-8 bytes.
    /// A cut inside the authority may leave the start of the user-info with no '@' to mark where it ends.
    /// </summary>
    private static bool MayBeTruncated(string url)
        => url.Length >= WafConstants.MaxStringLength
        || (url.Length * 3 >= WafConstants.MaxStringLength && Encoding.UTF8.GetByteCount(url) >= WafConstants.MaxStringLength);
}
