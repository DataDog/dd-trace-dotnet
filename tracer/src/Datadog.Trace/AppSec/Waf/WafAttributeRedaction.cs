// <copyright file="WafAttributeRedaction.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
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
    /// </summary>
    internal static string RemoveUrlSecrets(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);
        }

        var authorityStart = url.StartsWith("//", StringComparison.Ordinal) ? 2 : url.IndexOf("://", StringComparison.Ordinal) is var schemeEnd and >= 0 ? schemeEnd + 3 : -1;
        if (authorityStart >= 0)
        {
            // the user-info ends at the last '@' before the path: a password may contain '?' or '#'
            var authorityEnd = url.IndexOfAny(Separators.UrlAuthorityEnd, authorityStart);
            if (authorityEnd < 0)
            {
                authorityEnd = url.Length;
            }

            var userInfoEnd = authorityEnd > authorityStart ? url.LastIndexOf('@', authorityEnd - 1, authorityEnd - authorityStart) : -1;
            if (userInfoEnd >= 0)
            {
                url = url.Remove(authorityStart, userInfoEnd + 1 - authorityStart);
            }
        }

        var end = url.IndexOfAny(Separators.QueryOrFragment);
        return end < 0 ? url : url.Substring(0, end);
    }
}
