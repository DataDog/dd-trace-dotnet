// <copyright file="NormalizedRoute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NETFRAMEWORK
using System;
using System.Text;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Microsoft.AspNetCore.Routing;
using static Datadog.Trace.DiagnosticListeners.AspNetCoreDiagnosticObserver;

namespace Datadog.Trace.AppSec.ApiSec;

/// <summary>
/// Computes the "_dd.appsec.normalized_route" tag, as defined by RFC-1103 (API Security - Normalized HTTP Route):
/// a sequence of slash-separated atomic elements, where each URL segment is either a static constant
/// or a single dynamic parameter "{name}". Several parameters in the same segment are combined as "{a+b}".
/// </summary>
internal static class NormalizedRoute
{
    private const string HexDigits = "0123456789ABCDEF";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(NormalizedRoute));

    internal static void SetTag(Security security, Span span, RoutePattern routePattern, RouteValueDictionary routeValues)
    {
        if (!security.AppsecEnabled || !security.Settings.ApiSecurityEnabled)
        {
            return;
        }

        try
        {
            if (Get(routePattern, routeValues) is { } normalizedRoute)
            {
                span.Tags.SetTag(Tags.AppSecNormalizedRoute, normalizedRoute);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error computing the normalized route for {Route}", routePattern.RawText);
        }
    }

    internal static string? Get(RoutePattern routePattern, RouteValueDictionary routeValues)
    {
        if (routePattern.RawText is null)
        {
            return null;
        }

        var sb = StringBuilderCache.Acquire();
        foreach (var pathSegment in routePattern.PathSegments)
        {
            var segmentStart = sb.Length;
            var hasParameter = false;
            sb.Append('/');

            foreach (var part in pathSegment.DuckCast<RoutePatternPathSegmentStruct>().Parts)
            {
                if (part.TryDuckCast(out RoutePatternContentPartStruct content))
                {
                    // Static text next to a parameter is folded into the parameter (rule 5)
                    if (!hasParameter)
                    {
                        AppendStaticConstant(sb, content.Content);
                    }
                }
                else if (part.TryDuckCast(out RoutePatternParameterPartStruct parameter))
                {
                    routeValues.TryGetValue(parameter.Name, out var value);

                    // Optional elements are resolved against the values the framework matched for this request
                    if ((parameter.IsOptional || parameter.IsCatchAll) && value is null or string { Length: 0 })
                    {
                        continue;
                    }

                    // Conventional routing declares the MVC action as parameters, but they identify the
                    // endpoint, so report them as static constants, as we do for the resource name.
                    if (!hasParameter && IsMvcRouteValue(parameter.Name) && value is string { Length: > 0 } mvcValue)
                    {
                        AppendStaticConstant(sb, mvcValue);
                        continue;
                    }

                    if (hasParameter)
                    {
                        sb.Append('+');
                    }
                    else
                    {
                        sb.Length = segmentStart + 1;
                        sb.Append('{');
                        hasParameter = true;
                    }

                    AppendParameterName(sb, parameter.Name);
                }
            }

            if (hasParameter)
            {
                sb.Append('}');
            }
            else if (sb.Length == segmentStart + 1)
            {
                // Every element of the segment was optional and absent from the request
                sb.Length = segmentStart;
            }
        }

        if (sb.Length == 0)
        {
            sb.Append('/');
        }
        else if (routePattern.RawText.EndsWith("/", StringComparison.Ordinal))
        {
            // The trailing slash is kept only when the route declares it (rule 1)
            sb.Append('/');
        }

        return StringBuilderCache.GetStringAndRelease(sb);
    }

    private static bool IsMvcRouteValue(string name)
        => name.Equals("area", StringComparison.OrdinalIgnoreCase)
        || name.Equals("controller", StringComparison.OrdinalIgnoreCase)
        || name.Equals("action", StringComparison.OrdinalIgnoreCase);

    // Static constants only contain [A-Za-z0-9.-~_]; any other character is percent-encoded (rule 3)
    private static void AppendStaticConstant(StringBuilder sb, string value)
    {
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            var c = (char)b;
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '-' or '~' or '_')
            {
                sb.Append(c);
            }
            else
            {
                AppendPercentEncoded(sb, b);
            }
        }
    }

    // Parameter names may contain any character except "/?#+{}", which are percent-encoded (rule 4)
    private static void AppendParameterName(StringBuilder sb, string name)
    {
        foreach (var c in name)
        {
            if (c is '/' or '?' or '#' or '+' or '{' or '}')
            {
                AppendPercentEncoded(sb, (byte)c);
            }
            else
            {
                sb.Append(c);
            }
        }
    }

    private static void AppendPercentEncoded(StringBuilder sb, byte b)
        => sb.Append('%').Append(HexDigits[b >> 4]).Append(HexDigits[b & 0xF]);
}
#endif
