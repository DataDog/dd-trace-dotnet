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
/// MVC and Web API remove from the route values the optional parameters that the request doesn't contain,
/// so a parameter is part of the normalized route only when the route values contain it.
/// </summary>
internal static class AspNetRouteTemplate
{
    /// <summary>
    /// Sets the normalized route on the span when API Security is enabled.
    /// </summary>
    /// <param name="security">The security instance</param>
    /// <param name="span">The span that has the http.route tag</param>
    /// <param name="routeTemplate">The route template</param>
    /// <param name="routeValues">The route values matched for the request</param>
    /// <param name="applicationPath">The application path, which is the mount prefix of the route</param>
    internal static void SetTag(Security security, Span span, string? routeTemplate, IDictionary<string, object>? routeValues, string? applicationPath)
    {
        if (routeTemplate is null || routeValues is null)
        {
            return;
        }

        var normalizedRoute = NormalizedRouteBuilder.Create(security, applicationPath);
        if (!normalizedRoute.IsEnabled)
        {
            return;
        }

        if (AddRouteTemplate(ref normalizedRoute, routeTemplate, routeValues))
        {
            normalizedRoute.SetTag(span, routeTemplate);
        }
        else
        {
            // Releases the builder without a tag, rather than emitting an inaccurate value
            normalizedRoute.Complete(template: null);
        }
    }

    [TestingAndPrivateOnly]
    internal static string? GetNormalizedRoute(string? routeTemplate, IDictionary<string, object>? routeValues, string? applicationPath = null)
    {
        if (routeTemplate is null || routeValues is null)
        {
            return null;
        }

        var normalizedRoute = NormalizedRouteBuilder.Create(applicationPath);
        return AddRouteTemplate(ref normalizedRoute, routeTemplate, routeValues)
                   ? normalizedRoute.Complete(routeTemplate)
                   : normalizedRoute.Complete(template: null);
    }

    private static bool AddRouteTemplate(ref NormalizedRouteBuilder normalizedRoute, string routeTemplate, IDictionary<string, object> routeValues)
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
            while (nameStart < close && template[nameStart] == '*')
            {
                nameStart++;
            }

            var nameEnd = nameStart;
            while (nameEnd < close && template[nameEnd] is not (':' or '=' or '?'))
            {
                nameEnd++;
            }

            var name = template.Substring(nameStart, nameEnd - nameStart);
            routeValues.TryGetValue(name, out var value);
            normalizedRoute.AddParameter(name, IsOptionalSentinel(value) ? null : value, isOptional: true);

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
