// <copyright file="AspNetCoreResourceNameHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace Datadog.Trace.DiagnosticListeners;

internal class AspNetCoreResourceNameHelper
{
    internal static string SimplifyRoutePattern(
        RoutePattern routePattern,
        RouteValueDictionary routeValueDictionary,
        string? areaName,
        string? controllerName,
        string? actionName,
        bool expandRouteParameters)
    {
        var maxSize = (routePattern.RawText?.Length ?? 0)
                    + (string.IsNullOrEmpty(areaName) ? 0 : Math.Max(areaName!.Length - 4, 0)) // "area".Length
                    + (string.IsNullOrEmpty(controllerName) ? 0 : Math.Max(controllerName!.Length - 10, 0)) // "controller".Length
                    + (string.IsNullOrEmpty(actionName) ? 0 : Math.Max(actionName!.Length - 6, 0)) // "action".Length
                    + 1; // '/' prefix

        var sb = StringBuilderCache.Acquire(maxSize);

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

                        sb.Append(areaName);
                    }
                    else if (parameterName.Equals("controller", StringComparison.OrdinalIgnoreCase))
                    {
                        if (parts == 1)
                        {
                            sb.Append('/');
                        }

                        sb.Append(controllerName);
                    }
                    else if (parameterName.Equals("action", StringComparison.OrdinalIgnoreCase))
                    {
                        if (parts == 1)
                        {
                            sb.Append('/');
                        }

                        sb.Append(actionName);
                    }
                    else
                    {
                        var haveParameter = routeValueDictionary.TryGetValue(parameterName, out var value);
                        if (!parameter.IsOptional || haveParameter)
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
                                if (parameter.IsCatchAll)
                                {
                                    if (parameter.EncodeSlashes)
                                    {
                                        sb.Append("**");
                                    }
                                    else
                                    {
                                        sb.Append('*');
                                    }
                                }

                                sb.Append(parameterName);
                                if (parameter.IsOptional)
                                {
                                    sb.Append('?');
                                }

                                sb.Append('}');
                            }
                        }
                    }
                }
            }
        }

        var simplifiedRoute = StringBuilderCache.GetStringAndRelease(sb);

        return string.IsNullOrEmpty(simplifiedRoute) ? "/" : simplifiedRoute.ToLowerInvariant();
    }

    internal static string SimplifyRouteTemplate(
        RouteTemplate routePattern,
        RouteValueDictionary routeValueDictionary,
        string? areaName,
        string? controllerName,
        string? actionName,
        bool expandRouteParameters)
    {
        var maxSize = (routePattern.TemplateText?.Length ?? 0)
                    + (string.IsNullOrEmpty(areaName) ? 0 : Math.Max(areaName!.Length - 4, 0)) // "area".Length
                    + (string.IsNullOrEmpty(controllerName) ? 0 : Math.Max(controllerName!.Length - 10, 0)) // "controller".Length
                    + (string.IsNullOrEmpty(actionName) ? 0 : Math.Max(actionName!.Length - 6, 0)) // "action".Length
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

    private static bool IsIdentifierSegment(object? value, [NotNullWhen(true)] out string? valueAsString)
    {
        valueAsString = value as string ?? value?.ToString();
        if (valueAsString is null)
        {
            return false;
        }

        return UriHelpers.IsIdentifierSegment(valueAsString, 0, valueAsString.Length);
    }
}
#endif
