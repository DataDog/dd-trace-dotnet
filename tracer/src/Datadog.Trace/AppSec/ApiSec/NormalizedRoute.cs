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
using Microsoft.AspNetCore.Routing.Template;
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

    internal static bool IsEnabled(Security security) => security.AppsecEnabled && security.Settings.ApiSecurityEnabled;

    internal static void SetTag(Span span, RoutePattern routePattern, RouteValueDictionary routeValues, string? pathBase)
    {
        try
        {
            SetTag(span, Get(routePattern, routeValues, pathBase));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error computing the normalized route for {Route}", routePattern.RawText);
        }
    }

    internal static void SetTag(Span span, RouteTemplate routeTemplate, RouteValueDictionary routeValues, string? pathBase)
    {
        try
        {
            SetTag(span, Get(routeTemplate, routeValues, pathBase));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error computing the normalized route for {Route}", routeTemplate.TemplateText);
        }
    }

    internal static string? Get(RoutePattern routePattern, RouteValueDictionary routeValues, string? pathBase = null)
    {
        if (routePattern.RawText is null)
        {
            return null;
        }

        var builder = new Builder(routeValues, pathBase);
        foreach (var pathSegment in routePattern.PathSegments)
        {
            builder.StartSegment();
            foreach (var part in pathSegment.DuckCast<RoutePatternPathSegmentStruct>().Parts)
            {
                if (part.TryDuckCast(out RoutePatternContentPartStruct content))
                {
                    builder.AddStaticText(content.Content);
                }
                else if (part.TryDuckCast(out RoutePatternParameterPartStruct parameter))
                {
                    builder.AddParameter(parameter.Name, parameter.IsOptional || parameter.IsCatchAll);
                }
            }

            builder.EndSegment();
        }

        return builder.Complete(routePattern.RawText);
    }

    internal static string? Get(RouteTemplate routeTemplate, RouteValueDictionary routeValues, string? pathBase = null)
    {
        if (routeTemplate.TemplateText is null)
        {
            return null;
        }

        var builder = new Builder(routeValues, pathBase);
        foreach (var segment in routeTemplate.Segments)
        {
            builder.StartSegment();
            foreach (var part in segment.Parts)
            {
                if (part.IsParameter)
                {
                    builder.AddParameter(part.Name, part.IsOptional || part.IsCatchAll);
                }
                else
                {
                    builder.AddStaticText(part.Text);
                }
            }

            builder.EndSegment();
        }

        return builder.Complete(routeTemplate.TemplateText);
    }

    private static void SetTag(Span span, string? normalizedRoute)
    {
        if (normalizedRoute is not null)
        {
            span.Tags.SetTag(Tags.AppSecNormalizedRoute, normalizedRoute);
        }
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

    private struct Builder
    {
        private readonly StringBuilder _sb;
        private readonly RouteValueDictionary _routeValues;
        private int _segmentStart;
        private bool _hasParameter;

        public Builder(RouteValueDictionary routeValues, string? pathBase)
        {
            _sb = StringBuilderCache.Acquire();
            _routeValues = routeValues;
            _segmentStart = 0;
            _hasParameter = false;

            // The normalized route is the path as served to the caller, so it includes the mount prefix
            if (pathBase is not null)
            {
                var start = 0;
                for (var i = 0; i <= pathBase.Length; i++)
                {
                    if (i == pathBase.Length || pathBase[i] == '/')
                    {
                        if (i > start)
                        {
                            _sb.Append('/');
                            AppendStaticConstant(_sb, pathBase.Substring(start, i - start));
                        }

                        start = i + 1;
                    }
                }
            }
        }

        public void StartSegment()
        {
            _segmentStart = _sb.Length;
            _hasParameter = false;
            _sb.Append('/');
        }

        public void AddStaticText(string text)
        {
            // Static text next to a parameter is folded into the parameter (rule 5)
            if (!_hasParameter)
            {
                AppendStaticConstant(_sb, text);
            }
        }

        public void AddParameter(string name, bool isOptional)
        {
            _routeValues.TryGetValue(name, out var value);

            // Optional elements are resolved against the values the framework matched for this request
            if (isOptional && value is null or string { Length: 0 })
            {
                return;
            }

            // Conventional routing declares the MVC action as parameters, but they identify the
            // endpoint, so report them as static constants, as we do for the resource name.
            if (!_hasParameter && IsMvcRouteValue(name) && value is string { Length: > 0 } mvcValue)
            {
                AppendStaticConstant(_sb, mvcValue);
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

            AppendParameterName(_sb, name);
        }

        public void EndSegment()
        {
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

        public string Complete(string template)
        {
            if (_sb.Length == 0)
            {
                _sb.Append('/');
            }
            else if (template.Length > 1 && template.EndsWith("/", StringComparison.Ordinal))
            {
                // The trailing slash is kept only when the route declares it (rule 1)
                _sb.Append('/');
            }

            return StringBuilderCache.GetStringAndRelease(_sb);
        }
    }
}
#endif
