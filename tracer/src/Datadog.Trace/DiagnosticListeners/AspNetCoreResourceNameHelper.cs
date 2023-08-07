// <copyright file="AspNetCoreResourceNameHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace Datadog.Trace.DiagnosticListeners;

internal class AspNetCoreResourceNameHelper
{
    private static readonly ConcurrentDictionary<RoutePattern, RoutePatternFormatter> PatternCache = new(new RoutePatternComparer());

    internal static string SimplifyRoutePattern(
        RoutePattern routePattern,
        RouteValueDictionary routeValueDictionary,
        string areaName,
        string controllerName,
        string actionName,
        bool expandRouteParameters)
    {
        var formatter = PatternCache.GetOrAdd(
            routePattern,
            static pattern => BuildRoutePatternFormatString(pattern));

        return formatter.Format(areaName, controllerName, actionName, routeValueDictionary, expandRouteParameters);
    }

    internal static string SimplifyRouteTemplate(
        RouteTemplate routePattern,
        RouteValueDictionary routeValueDictionary,
        string areaName,
        string controllerName,
        string actionName,
        bool expandRouteParameters)
    {
        var maxSize = routePattern.TemplateText.Length
                    + (string.IsNullOrEmpty(areaName) ? 0 : Math.Max(areaName.Length - 4, 0)) // "area".Length
                    + (string.IsNullOrEmpty(controllerName) ? 0 : Math.Max(controllerName.Length - 10, 0)) // "controller".Length
                    + (string.IsNullOrEmpty(actionName) ? 0 : Math.Max(actionName.Length - 6, 0)) // "action".Length
                    + 1; // '/' prefix

        var sb = StringBuilderCache.Acquire(maxSize);

        foreach (var pathSegment in routePattern.Segments)
        {
            var parts = 0;
            foreach (var part in pathSegment.Parts)
            {
                parts++;
                var partName = part.Name;

                if (!part.IsParameter)
                {
                    if (parts == 1)
                    {
                        sb.Append('/');
                    }

                    sb.Append(part.Text);
                }
                else if (partName.Equals("area", StringComparison.OrdinalIgnoreCase))
                {
                    if (parts == 1)
                    {
                        sb.Append('/');
                    }

                    sb.Append(areaName);
                }
                else if (partName.Equals("controller", StringComparison.OrdinalIgnoreCase))
                {
                    if (parts == 1)
                    {
                        sb.Append('/');
                    }

                    sb.Append(controllerName);
                }
                else if (partName.Equals("action", StringComparison.OrdinalIgnoreCase))
                {
                    if (parts == 1)
                    {
                        sb.Append('/');
                    }

                    sb.Append(actionName);
                }
                else
                {
                    var haveParameter = routeValueDictionary.TryGetValue(partName, out var value);
                    if (!part.IsOptional || haveParameter)
                    {
                        if (parts == 1)
                        {
                            sb.Append('/');
                        }

                        if (expandRouteParameters && haveParameter && !IsIdentifierSegment(value, out var valueAsString))
                        {
                            // write the expanded parameter value
                            sb.Append(valueAsString);
                        }
                        else
                        {
                            // write the route template value
                            sb.Append('{');
                            if (part.IsCatchAll)
                            {
                                sb.Append('*');
                            }

                            sb.Append(partName);
                            if (part.IsOptional)
                            {
                                sb.Append('?');
                            }

                            sb.Append('}');
                        }
                    }
                }
            }
        }

        var simplifiedRoute = StringBuilderCache.GetStringAndRelease(sb);

        return string.IsNullOrEmpty(simplifiedRoute) ? "/" : simplifiedRoute.ToLowerInvariant();
    }

    private static bool IsIdentifierSegment(object value, [NotNullWhen(false)] out string valueAsString)
    {
        valueAsString = value as string ?? value?.ToString();
        if (valueAsString is null)
        {
            return false;
        }

        return UriHelpers.IsIdentifierSegment(valueAsString, 0, valueAsString.Length);
    }

    private static RoutePatternFormatter BuildRoutePatternFormatString(RoutePattern routePattern)
    {
        var sb = StringBuilderCache.Acquire(routePattern.RawText.Length + 1);
        List<Replacements> routeValues = null;
        var argNumber = 3; // we always count controller, action, area

        foreach (var pathSegment in routePattern.PathSegments)
        {
            var parts = 0;
            foreach (var part in pathSegment.DuckCast<AspNetCoreDiagnosticObserver.RoutePatternPathSegmentStruct>().Parts)
            {
                parts++;
                if (part.TryDuckCast(out AspNetCoreDiagnosticObserver.RoutePatternContentPartStruct contentPart))
                {
                    if (parts == 1)
                    {
                        sb.Append('/');
                    }

                    sb.Append(contentPart.Content);
                }
                else if (part.TryDuckCast(out AspNetCoreDiagnosticObserver.RoutePatternParameterPartStruct parameter))
                {
                    var parameterName = parameter.Name;
                    if (parameterName.Equals("area", StringComparison.OrdinalIgnoreCase))
                    {
                        if (parts == 1)
                        {
                            sb.Append('/');
                        }

                        sb.Append("{0}");
                    }
                    else if (parameterName.Equals("controller", StringComparison.OrdinalIgnoreCase))
                    {
                        if (parts == 1)
                        {
                            sb.Append('/');
                        }

                        sb.Append("{1}");
                    }
                    else if (parameterName.Equals("action", StringComparison.OrdinalIgnoreCase))
                    {
                        if (parts == 1)
                        {
                            sb.Append('/');
                        }

                        sb.Append("{2}");
                    }
                    else
                    {
                        bool prependSlash = parts == 1;
                        bool isOptional = parameter.IsOptional;
                        string alternative = $"{{{(parameter.IsCatchAll ? (parameter.EncodeSlashes ? "**" : "*") : string.Empty)}{parameterName.ToLowerInvariant()}{(parameter.IsOptional ? "?" : string.Empty)}}}";

                        routeValues ??= new();
                        routeValues.Add(new(parameterName, alternative, prependSlash, isOptional));
                        if (prependSlash)
                        {
                            sb.Append('{').Append(argNumber).Append('}');
                            argNumber++;
                        }

                        sb.Append('{').Append(argNumber).Append('}');
                        argNumber++;
                    }
                }
            }
        }

        var simplifiedRoute = StringBuilderCache.GetStringAndRelease(sb);

        return string.IsNullOrEmpty(simplifiedRoute)
                   ? RoutePatternFormatter.NullFormatter
                   : new RoutePatternFormatter(simplifiedRoute.ToLowerInvariant(), routeValues, argNumber);
    }

    private readonly struct Replacements
    {
        public readonly string ParameterName;
        public readonly string ReplacementIfMissing;
        public readonly bool PrependSlash;
        public readonly bool IsOptional;

        public Replacements(string parameterName, string replacementIfMissing, bool prependSlash, bool isOptional)
        {
            ParameterName = parameterName;
            ReplacementIfMissing = replacementIfMissing;
            PrependSlash = prependSlash;
            IsOptional = isOptional;
        }
    }

    public class RoutePatternComparer : IEqualityComparer<RoutePattern>
    {
        public bool Equals(RoutePattern x, RoutePattern y)
            => StringComparer.Ordinal.Equals(x.RawText, y.RawText);

        public int GetHashCode(RoutePattern obj)
            => StringComparer.Ordinal.GetHashCode(obj.RawText);
    }

    private class RoutePatternFormatter
    {
        private readonly string _formatString;
        private readonly List<Replacements> _replacements;
        private readonly int _totalArgs;

        public RoutePatternFormatter(string formatString, List<Replacements> replacements, int totalArgs)
        {
            _formatString = formatString;
            _replacements = replacements;
            _totalArgs = totalArgs;
        }

        public static RoutePatternFormatter NullFormatter { get; } = new("/", null, 3);

        public string Format(
            string area,
            string controller,
            string action,
            RouteValueDictionary routeValues,
            bool expandRouteParameters)
        {
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
            if (_replacements is null)
            {
                sb.AppendFormat(_formatString, area, controller, action);
            }
            else
            {
                // TODO: use inline arrays once we can
                var objects = new object[_totalArgs];
                objects[0] = area;
                objects[1] = controller;
                objects[2] = action;
                var argNumber = 3;
                for (var i = 0; i < _replacements.Count; i++)
                {
                    var replacement = _replacements[i];
                    var haveParameter = routeValues.TryGetValue(replacement.ParameterName, out var value);
                    if (replacement.IsOptional && !haveParameter)
                    {
                        // remove it
                        if (replacement.PrependSlash)
                        {
                            objects[argNumber] = string.Empty;
                            argNumber++;
                        }

                        objects[argNumber] = string.Empty;
                        argNumber++;
                        continue;
                    }

                    if (replacement.PrependSlash)
                    {
                        objects[argNumber] = "/";
                        argNumber++;
                    }

                    if (expandRouteParameters && haveParameter && !IsIdentifierSegment(value, out var valueAsString))
                    {
                        objects[argNumber] = valueAsString?.ToLowerInvariant();
                        argNumber++;
                    }
                    else
                    {
                        objects[argNumber] = replacement.ReplacementIfMissing;
                        argNumber++;
                    }
                }

                sb.AppendFormat(_formatString, objects);
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }
    }
}
#endif
