// <copyright file="AspNetCoreResourceNameHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
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
        string areaName,
        string controllerName,
        string actionName,
        bool expandRouteParameters)
    {
        var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

        foreach (var pathSegment in routePattern.PathSegments)
        {
            var parts = 0;
            foreach (var part in pathSegment.DuckCast<AspNetCoreDiagnosticObserver.RoutePatternPathSegmentStruct>().Parts)
            {
                if (++parts == 1)
                {
                    sb.Append('/');
                }

                if (part.TryDuckCast(out AspNetCoreDiagnosticObserver.RoutePatternContentPartStruct contentPart))
                {
                    sb.Append(contentPart.Content);
                }
                else if (part.TryDuckCast(out AspNetCoreDiagnosticObserver.RoutePatternParameterPartStruct parameter))
                {
                    var parameterName = parameter.Name;

                    if (parameterName.Equals("area", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append(areaName);
                    }
                    else if (parameterName.Equals("controller", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append(controllerName);
                    }
                    else if (parameterName.Equals("action", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append(actionName);
                    }
                    else
                    {
                        var haveParameter = routeValueDictionary.TryGetValue(parameterName, out var value);
                        if (!parameter.IsOptional || haveParameter)
                        {
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
                                    sb.Append('*');
                                    if (parameter.EncodeSlashes)
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
                        else
                        {
                            sb.Remove(sb.Length - 1, 1);
                        }
                    }
                }
            }
        }

        if (sb.Length == 0)
        {
            StringBuilderCache.Release(sb);
            return "/";
        }

        return StringBuilderCache.GetStringAndRelease(sb).ToLowerInvariant();
    }

    internal static string SimplifyRouteTemplate(
        RouteTemplate routePattern,
        RouteValueDictionary routeValueDictionary,
        string areaName,
        string controllerName,
        string actionName,
        bool expandRouteParameters)
    {
        var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

        foreach (var pathSegment in routePattern.Segments)
        {
            var parts = 0;
            foreach (var part in pathSegment.Parts)
            {
                if (++parts == 1)
                {
                    sb.Append('/');
                }

                var partName = part.Name;
                if (!part.IsParameter)
                {
                    sb.Append(part.Text);
                }
                else if (partName.Equals("area", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append(areaName);
                }
                else if (partName.Equals("controller", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append(controllerName);
                }
                else if (partName.Equals("action", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append(actionName);
                }
                else
                {
                    var haveParameter = routeValueDictionary.TryGetValue(partName, out var value);
                    if (!part.IsOptional || haveParameter)
                    {
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
                    else
                    {
                        sb.Remove(sb.Length - 1, 1);
                    }
                }
            }
        }

        if (sb.Length == 0)
        {
            StringBuilderCache.Release(sb);
            return "/";
        }

        return StringBuilderCache.GetStringAndRelease(sb).ToLowerInvariant();
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
}
#endif
