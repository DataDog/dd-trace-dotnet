// <copyright file="AspNetRouteTemplate.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.AppSec.ApiSec;

/// <summary>
/// Builds the API Security normalized route of an ASP.NET (System.Web) route template, such as
/// "api/{controller}/{id}", as used by MVC 5 (Route.Url) and Web API 2 (IHttpRoute.RouteTemplate).
/// MVC and Web API remove from the route values the optional parameters that the request doesn't contain, and fill
/// the segments that are missing from the request with the defaults. So a parameter is part of the normalized route
/// only when the route values contain it and its segment is in the request (RFC-1125).
/// </summary>
internal static class AspNetRouteTemplate
{
    /// <summary>
    /// Gets whether the normalized route is computed, so that callers don't compute its inputs otherwise.
    /// </summary>
    /// <param name="security">The security instance</param>
    /// <returns>Whether API Security is enabled</returns>
    internal static bool IsEnabled(Security security) => security.AppsecEnabled && security.Settings.ApiSecurityEnabled;

    /// <summary>
    /// Gets the path that MVC routing matches: the app-relative path ("~/home/index") followed by the path info.
    /// </summary>
    /// <param name="appRelativePath">HttpRequest.AppRelativeCurrentExecutionFilePath</param>
    /// <param name="pathInfo">HttpRequest.PathInfo</param>
    /// <returns>The path relative to the application path, for example "/home/index"</returns>
    internal static string? GetMvcRequestPath(string? appRelativePath, string? pathInfo)
    {
        if (appRelativePath is null)
        {
            return null;
        }

        var path = appRelativePath.StartsWith("~", StringComparison.Ordinal) ? appRelativePath.Substring(1) : appRelativePath;
        return string.IsNullOrEmpty(pathInfo) ? path : path + pathInfo;
    }

    /// <summary>
    /// Gets the path that Web API routing matches: the request path without the virtual path root.
    /// </summary>
    /// <param name="absolutePath">The request URI absolute path</param>
    /// <param name="virtualPathRoot">The virtual path root of the request context</param>
    /// <returns>The path relative to the virtual path root, for example "/api/values"</returns>
    internal static string? GetWebApiRequestPath(string? absolutePath, string? virtualPathRoot)
    {
        if (absolutePath is null)
        {
            return null;
        }

        var root = virtualPathRoot?.TrimEnd('/');
        return !string.IsNullOrEmpty(root) && absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                   ? absolutePath.Substring(root!.Length)
                   : absolutePath;
    }

    /// <summary>
    /// Sets the normalized route on the span when API Security is enabled.
    /// </summary>
    /// <param name="security">The security instance</param>
    /// <param name="span">The span that has the http.route tag</param>
    /// <param name="routeTemplate">The route template</param>
    /// <param name="routeValues">The route values matched for the request</param>
    /// <param name="applicationPath">The application path, which is the mount prefix of the route</param>
    /// <param name="requestPath">The request path that the route matched, relative to <paramref name="applicationPath"/></param>
    internal static void SetTag(Security security, Span span, string? routeTemplate, IDictionary<string, object>? routeValues, string? applicationPath, string? requestPath)
    {
        if (routeTemplate is null || routeValues is null)
        {
            return;
        }

        var normalizedRoute = NormalizedRouteBuilder.Create(security, applicationPath, requestPath);
        if (!normalizedRoute.IsEnabled)
        {
            return;
        }

        // A null template releases the builder without a tag, rather than emitting an inaccurate value
        var isValid = AddRouteTemplate(ref normalizedRoute, routeTemplate, routeValues);
        normalizedRoute.SetTag(span, isValid ? routeTemplate : null);
    }

    /// <summary>
    /// Adds the segments of the route template to the builder.
    /// </summary>
    /// <param name="normalizedRoute">The normalized route builder</param>
    /// <param name="routeTemplate">The route template</param>
    /// <param name="routeValues">The route values matched for the request</param>
    /// <returns>Whether the route template is valid</returns>
    [TestingAndPrivateOnly]
    internal static bool AddRouteTemplate(ref NormalizedRouteBuilder normalizedRoute, string routeTemplate, IDictionary<string, object> routeValues)
    {
        var segmentStart = routeTemplate.StartsWith("~/", StringComparison.Ordinal) ? 2 : 0;
        while (segmentStart < routeTemplate.Length)
        {
            var segmentEnd = routeTemplate.IndexOf('/', segmentStart);
            if (segmentEnd < 0)
            {
                segmentEnd = routeTemplate.Length;
            }

            if (segmentEnd > segmentStart)
            {
                normalizedRoute.StartSegment();
                if (!AddSegment(ref normalizedRoute, routeTemplate, segmentStart, segmentEnd, routeValues))
                {
                    return false;
                }

                normalizedRoute.EndSegment();
            }

            segmentStart = segmentEnd + 1;
        }

        return true;
    }

    private static bool AddSegment(ref NormalizedRouteBuilder normalizedRoute, string template, int start, int end, IDictionary<string, object> routeValues)
    {
        var literalStart = start;
        var i = start;
        while (i < end)
        {
            var c = template[i];
            if (c is not ('{' or '}'))
            {
                i++;
                continue;
            }

            normalizedRoute.AddStaticText(template, literalStart, i);

            // "{{" and "}}" are escaped braces
            if (i + 1 < end && template[i + 1] == c)
            {
                normalizedRoute.AddStaticText(template, i, i + 1);
                i += 2;
                literalStart = i;
                continue;
            }

            var close = c == '{' ? template.IndexOf('}', i + 1, end - i - 1) : -1;
            if (close < 0)
            {
                return false;
            }

            // {name}, {*name}, {name:constraint}, {name=default} or {name?}
            var nameStart = i + 1;
            var isCatchAll = false;
            while (nameStart < close && template[nameStart] == '*')
            {
                isCatchAll = true;
                nameStart++;
            }

            var nameEnd = nameStart;
            while (nameEnd < close && template[nameEnd] is not (':' or '=' or '?'))
            {
                nameEnd++;
            }

            var name = template.Substring(nameStart, nameEnd - nameStart);
            routeValues.TryGetValue(name, out var value);
            // Any parameter may come from a default, which fills the segments that aren't in the request
            normalizedRoute.AddParameter(name, IsOptionalSentinel(value) ? null : value, isOptional: false, isCatchAll, hasDefault: true);

            i = close + 1;
            literalStart = i;
        }

        normalizedRoute.AddStaticText(template, literalStart, end);
        return true;
    }

    // UrlParameter.Optional (MVC) and RouteParameter.Optional (Web API) mark an optional parameter without value
    private static bool IsOptionalSentinel(object? value)
        => value?.GetType().FullName is "System.Web.Mvc.UrlParameter" or "System.Web.Http.RouteParameter";
}
