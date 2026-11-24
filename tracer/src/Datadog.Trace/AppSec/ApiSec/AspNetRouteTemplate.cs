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
#if NETFRAMEWORK
    /// <summary>
    /// Sets the normalized route on the span when API Security is enabled, for MVC and web-hosted Web API, whose routing
    /// matches the app-relative path ("~/home/index") followed by the path info.
    /// </summary>
    /// <param name="security">The security instance</param>
    /// <param name="span">The span that has the http.route tag</param>
    /// <param name="routeTemplate">The route template</param>
    /// <param name="routeValues">The route values matched for the request</param>
    /// <param name="request">The request</param>
    internal static void SetTag(Security security, Span span, string? routeTemplate, IDictionary<string, object>? routeValues, System.Web.HttpRequestBase request)
    {
        if (NormalizedRouteBuilder.IsEnabledFor(security))
        {
            SetTag(security, span, routeTemplate, routeValues, request.ApplicationPath, request.AppRelativeCurrentExecutionFilePath, request.PathInfo);
        }
    }

    /// <inheritdoc cref="SetTag(Security, Span, string?, IDictionary{string, object}?, System.Web.HttpRequestBase)"/>
    internal static void SetTag(Security security, Span span, string? routeTemplate, IDictionary<string, object>? routeValues, System.Web.HttpRequest request)
    {
        if (NormalizedRouteBuilder.IsEnabledFor(security))
        {
            SetTag(security, span, routeTemplate, routeValues, request.ApplicationPath, request.AppRelativeCurrentExecutionFilePath, request.PathInfo);
        }
    }

    private static void SetTag(Security security, Span span, string? routeTemplate, IDictionary<string, object>? routeValues, string? applicationPath, string? appRelativePath, string? pathInfo)
        => SetTag(security, span, routeTemplate, routeValues, applicationPath, appRelativePath, GetAppRelativePathStart(appRelativePath), pathInfo);
#endif

    /// <summary>
    /// Sets the normalized route on the span when API Security is enabled, for self-hosted Web API, whose routing matches
    /// the decoded request path without the virtual path root.
    /// </summary>
    /// <param name="security">The security instance</param>
    /// <param name="span">The span that has the http.route tag</param>
    /// <param name="routeTemplate">The route template</param>
    /// <param name="routeValues">The route values matched for the request</param>
    /// <param name="virtualPathRoot">The virtual path root of the request context</param>
    /// <param name="requestUri">The request URI</param>
    internal static void SetTagForSelfHost(Security security, Span span, string? routeTemplate, IDictionary<string, object>? routeValues, string? virtualPathRoot, Uri? requestUri)
    {
        if (!NormalizedRouteBuilder.IsEnabledFor(security))
        {
            return;
        }

        var path = requestUri is null ? null : Uri.UnescapeDataString(requestUri.AbsolutePath);
        SetTag(security, span, routeTemplate, routeValues, virtualPathRoot, path, GetVirtualPathRootLength(path, virtualPathRoot), requestPathSuffix: null);
    }

    /// <summary>
    /// Gets the index where the path relative to the application path starts in an app-relative path, after the "~".
    /// </summary>
    /// <param name="appRelativePath">The app-relative path, for example "~/home/index"</param>
    /// <returns>The index of the relative path</returns>
    internal static int GetAppRelativePathStart(string? appRelativePath)
        => appRelativePath?.StartsWith("~", StringComparison.Ordinal) == true ? 1 : 0;

    /// <summary>
    /// Gets the length of the virtual path root at the start of the path, without its trailing slash.
    /// </summary>
    /// <param name="path">The decoded request path</param>
    /// <param name="virtualPathRoot">The virtual path root</param>
    /// <returns>The index where the path relative to the virtual path root starts</returns>
    internal static int GetVirtualPathRootLength(string? path, string? virtualPathRoot)
    {
        if (path is null || virtualPathRoot is null)
        {
            return 0;
        }

        var rootLength = virtualPathRoot.Length;
        while (rootLength > 0 && virtualPathRoot[rootLength - 1] == '/')
        {
            rootLength--;
        }

        return rootLength > 0 && path.Length >= rootLength && string.Compare(path, 0, virtualPathRoot, 0, rootLength, StringComparison.OrdinalIgnoreCase) == 0
                   ? rootLength
                   : 0;
    }

    private static void SetTag(Security security, Span span, string? routeTemplate, IDictionary<string, object>? routeValues, string? applicationPath, string? requestPath, int requestPathStart, string? requestPathSuffix)
    {
        if (routeTemplate is null || routeValues is null)
        {
            return;
        }

        var normalizedRoute = NormalizedRouteBuilder.Create(security, applicationPath, requestPath, requestPathStart, requestPathSuffix);
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

            // Any parameter may come from a default, which fills the segments that aren't in the request. When the request
            // path is unknown, a parameter with a value is kept, so that the mandatory parameters are never left out.
            var hasRequestPath = normalizedRoute.HasRequestPath;
            normalizedRoute.AddParameter(name, IsOptionalSentinel(value) ? null : value, isOptional: !hasRequestPath, isCatchAll, hasDefault: hasRequestPath);

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
