// <copyright file="AspNetCoreResourceNameHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace Datadog.Trace.DiagnosticListeners;

internal static class AspNetCoreResourceNameHelper
{
#if NET6_0_OR_GREATER
    internal static string SimplifyRoutePattern(
        RoutePattern routePattern,
        RouteValueDictionary routeValueDictionary,
        bool expandRouteParameters)
    {
        var sb = routePattern.RawText?.Length < 512
                     ? new ValueStringBuilder(stackalloc char[512])
                     : new ValueStringBuilder(); // too big to use stackallocation, so use array builder

        foreach (var pathSegment in routePattern.PathSegments)
        {
            var addedPart = false;
            foreach (var part in pathSegment.DuckCast<AspNetCoreDiagnosticObserver.RoutePatternPathSegmentStruct>().Parts)
            {
                if (part.TryDuckCast(out AspNetCoreDiagnosticObserver.RoutePatternContentPartStruct contentPart))
                {
                    if (!addedPart)
                    {
                        sb.Append('/');
                        addedPart = true;
                    }

                    sb.AppendAsLowerInvariant(contentPart.Content);
                }
                else if (part.TryDuckCast(out AspNetCoreDiagnosticObserver.RoutePatternParameterPartStruct parameter))
                {
                    var parameterName = parameter.Name;
                    var haveParameter = routeValueDictionary.TryGetValue(parameterName, out var value);
                    if (!parameter.IsOptional || haveParameter)
                    {
                        if (!addedPart)
                        {
                            sb.Append('/');
                            addedPart = true;
                        }

                        // Is this parameter an identifier segment? we assume non-strings _are_ identifiers
                        // so never expand them. This avoids an allocating ToString() call, but means that
                        // some parameters which maybe _should_ be expanded (e.g. Enum)s currently are not
                        if (haveParameter
                         && (expandRouteParameters
                          || parameterName.Equals("area", StringComparison.OrdinalIgnoreCase)
                          || parameterName.Equals("controller", StringComparison.OrdinalIgnoreCase)
                          || parameterName.Equals("action", StringComparison.OrdinalIgnoreCase))
                         && (value is null ||
                             (value is string valueAsString
                           && !UriHelpers.IsIdentifierSegment(valueAsString, 0, valueAsString.Length))))
                        {
                            // write the expanded parameter value
                            sb.AppendAsLowerInvariant(value as string);
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

                            sb.AppendAsLowerInvariant(parameterName);
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

        // We never added anything, or we just added the first `/`, no need for explicit ToString()
        if (sb.Length <= 1)
        {
            sb.Dispose();
            return "/";
        }

        return sb.ToString();
    }
#endif

    internal static string SimplifyRoutePattern(
        RoutePattern routePattern,
        IReadOnlyDictionary<string, object?> routeValueDictionary,
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
                        if (areaName is null && parameter.IsOptional)
                        {
                            // don't append optional suffixes when no value is provided
                            continue;
                        }

                        if (parts == 1)
                        {
                            sb.Append('/');
                        }

                        sb.Append(areaName ?? "{area}");
                    }
                    else if (parameterName.Equals("controller", StringComparison.OrdinalIgnoreCase))
                    {
                        if (controllerName is null && parameter.IsOptional)
                        {
                            // don't append optional suffixes when no value is provided
                            continue;
                        }

                        if (parts == 1)
                        {
                            sb.Append('/');
                        }

                        sb.Append(controllerName ?? "{controller}");
                    }
                    else if (parameterName.Equals("action", StringComparison.OrdinalIgnoreCase))
                    {
                        if (actionName is null && parameter.IsOptional)
                        {
                            // don't append optional suffixes when no value is provided
                            continue;
                        }

                        if (parts == 1)
                        {
                            sb.Append('/');
                        }

                        sb.Append(actionName ?? "{action}");
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
        // note that this is not accurate if expandRouteParameters=true, but we don't have a good fallback for that
        var maxSize = (routePattern.TemplateText?.Length ?? 0)
                    + (string.IsNullOrEmpty(areaName) ? 0 : Math.Max(areaName!.Length - 4, 0)) // "area".Length
                    + (string.IsNullOrEmpty(controllerName) ? 0 : Math.Max(controllerName!.Length - 10, 0)) // "controller".Length
                    + (string.IsNullOrEmpty(actionName) ? 0 : Math.Max(actionName!.Length - 6, 0)) // "action".Length
                    + 1; // '/' prefix

#if NETCOREAPP
        var sb = maxSize < 512
                     ? new ValueStringBuilder(stackalloc char[512])
                     : new ValueStringBuilder(); // too big to use stackallocation, so use array builder
#else
        // In .NET Core 2.1, the ValueStringBuilder doesn't actually improve anything
        var sb = StringBuilderCache.Acquire(maxSize);
#endif

        // Remove the boxing of the enumerator
        // In all versions of .NET, this is implemented as a List<TemplateSegment>
        // https://github.com/aspnet/Routing/blob/release/2.1/src/Microsoft.AspNetCore.Routing/Template/RouteTemplate.cs
        // https://github.com/aspnet/Routing/blob/release/2.2/src/Microsoft.AspNetCore.Routing/Template/RouteTemplate.cs
        // https://github.com/dotnet/aspnetcore/blob/v3.0.0/src/Http/Routing/src/Template/RouteTemplate.cs
        // https://github.com/dotnet/aspnetcore/blob/v3.1.0/src/Http/Routing/src/Template/RouteTemplate.cs
        // https://github.com/dotnet/aspnetcore/blob/v5.0.0/src/Http/Routing/src/Template/RouteTemplate.cs
        // https://github.com/dotnet/aspnetcore/blob/v6.0.0/src/Http/Routing/src/Template/RouteTemplate.cs
        // https://github.com/dotnet/aspnetcore/blob/v7.0.0/src/Http/Routing/src/Template/RouteTemplate.cs
        // https://github.com/dotnet/aspnetcore/blob/v8.0.0/src/Http/Routing/src/Template/RouteTemplate.cs
        // https://github.com/dotnet/aspnetcore/blob/v9.0.0/src/Http/Routing/src/Template/RouteTemplate.cs
        // https://github.com/dotnet/aspnetcore/blob/main/src/Http/Routing/src/Template/RouteTemplate.cs
        foreach (var pathSegment in (List<TemplateSegment>)routePattern.Segments)
        {
            var addedPart = false;
            foreach (var part in pathSegment.Parts)
            {
                if (!part.IsParameter)
                {
                    if (!addedPart)
                    {
                        sb.Append('/');
                        addedPart = true;
                    }

                    sb.AppendAsLowerInvariant(part.Text);
                }
                else
                {
                    var parameterName = part.Name;

                    // Avoiding the dictionary lookup as we already did it
                    var shouldExpand = expandRouteParameters;
                    var haveParameter = true;
                    object? value;
                    if (parameterName.Equals("area", StringComparison.OrdinalIgnoreCase))
                    {
                        shouldExpand = true;
                        value = areaName;
                    }
                    else if (parameterName.Equals("controller", StringComparison.OrdinalIgnoreCase))
                    {
                        shouldExpand = true;
                        value = controllerName;
                    }
                    else if (parameterName.Equals("action", StringComparison.OrdinalIgnoreCase))
                    {
                        shouldExpand = true;
                        value = actionName;
                    }
                    else if (routeValueDictionary.TryGetValue(parameterName, out value))
                    {
                        haveParameter = true;
                    }
                    else
                    {
                        haveParameter = false;
                        value = null;
                    }

                    if (!part.IsOptional || haveParameter)
                    {
                        if (!addedPart)
                        {
                            sb.Append('/');
                            addedPart = true;
                        }
                    }

                    // Is this parameter an identifier segment? we assume non-strings _are_ identifiers
                    // so never expand them. This avoids an allocating ToString() call, but means that
                    // some parameters which maybe _should_ be expanded (e.g. Enum)s currently are not
                    if (haveParameter
                     && shouldExpand
                     && (value is null ||
                         (value is string valueAsString
                       && !UriHelpers.IsIdentifierSegment(valueAsString, 0, valueAsString.Length))))
                    {
                        // write the expanded parameter value
                        sb.AppendAsLowerInvariant(value as string);
                    }
                    else
                    {
                        // write the route template value
                        sb.Append('{');

                        if (part.IsCatchAll)
                        {
                            sb.Append('*');
                        }

                        sb.AppendAsLowerInvariant(parameterName);
                        if (part.IsOptional)
                        {
                            sb.Append('?');
                        }

                        sb.Append('}');
                    }
                }
            }
        }

        // We never added anything, or we just added the first `/`, no need for explicit ToString()
#if NETCOREAPP
        if (sb.Length <= 1)
        {
            sb.Dispose();
            return "/";
        }

        return sb.ToString();
#else
        return StringBuilderCache.GetStringAndRelease(sb).ToLowerInvariant();
#endif
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

#if !NETCOREAPP
    // .NET Core 2.1 helper which doesn't _actually_ append as lower invariant, and just does it all at the end instead
    private static void AppendAsLowerInvariant(this StringBuilder sb, string? value) => sb.Append(value);
#endif
}
#endif
