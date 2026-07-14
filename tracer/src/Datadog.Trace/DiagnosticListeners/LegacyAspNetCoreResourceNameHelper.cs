// <copyright file="LegacyAspNetCoreResourceNameHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.DiagnosticListeners;

/// <summary>
/// Duck-typed equivalent of <c>AspNetCoreResourceNameHelper</c>'s route template simplification, for use
/// by <see cref="LegacyAspNetCoreDiagnosticObserver"/>, which can't reference the ASP.NET Core routing
/// types directly and so walks the duck-typed proxies defined there instead of the real types.
/// </summary>
internal static class LegacyAspNetCoreResourceNameHelper
{
    internal static string SimplifyRouteTemplate(
        LegacyAspNetCoreDiagnosticObserver.RouteTemplateStruct routeTemplate,
        IDictionary<string, object>? routeValueDictionary,
        string? areaName,
        string? controllerName,
        string? actionName,
        bool expandRouteParameters)
    {
        // note that this is not accurate if expandRouteParameters=true, but we don't have a good fallback for that
        var maxSize = (routeTemplate.TemplateText?.Length ?? 0)
                    + (string.IsNullOrEmpty(areaName) ? 0 : Math.Max(areaName!.Length - 4, 0)) // "area".Length
                    + (string.IsNullOrEmpty(controllerName) ? 0 : Math.Max(controllerName!.Length - 10, 0)) // "controller".Length
                    + (string.IsNullOrEmpty(actionName) ? 0 : Math.Max(actionName!.Length - 6, 0)) // "action".Length
                    + 1; // '/' prefix

        var sb = StringBuilderCache.Acquire(maxSize);

        if (routeTemplate.Segments is { } segments)
        {
            foreach (var segmentObj in segments)
            {
                var pathSegment = segmentObj.DuckCast<LegacyAspNetCoreDiagnosticObserver.TemplateSegmentStruct>();
                if (pathSegment.Parts is not { } parts)
                {
                    continue;
                }

                var addedPart = false;
                foreach (var partObj in parts)
                {
                    var part = partObj.DuckCast<LegacyAspNetCoreDiagnosticObserver.TemplatePartStruct>();
                    if (!part.IsParameter)
                    {
                        if (!addedPart)
                        {
                            sb.Append('/');
                            addedPart = true;
                        }

                        AppendLowerInvariant(sb, part.Text);
                    }
                    else
                    {
                        var parameterName = part.Name ?? string.Empty;

                        var mustExpand = false;
                        object? paramValue;
                        if (parameterName.Equals("area", StringComparison.OrdinalIgnoreCase))
                        {
                            mustExpand = true;
                            paramValue = areaName;
                        }
                        else if (parameterName.Equals("controller", StringComparison.OrdinalIgnoreCase))
                        {
                            mustExpand = true;
                            paramValue = controllerName;
                        }
                        else if (parameterName.Equals("action", StringComparison.OrdinalIgnoreCase))
                        {
                            mustExpand = true;
                            paramValue = actionName;
                        }
                        else if (routeValueDictionary is null || !routeValueDictionary.TryGetValue(parameterName, out paramValue))
                        {
                            paramValue = null;
                        }

                        var haveParameter = paramValue is not null;

                        if (part.IsOptional && !haveParameter)
                        {
                            continue;
                        }

                        if (!addedPart)
                        {
                            sb.Append('/');
                            addedPart = true;
                        }

                        // Is this parameter an identifier segment?
                        var valueAsString = paramValue as string;
                        if (haveParameter
                         && (mustExpand || (expandRouteParameters && !IsIdentifierSegment(paramValue, out valueAsString))))
                        {
                            // write the expanded parameter value
                            AppendLowerInvariant(sb, valueAsString);
                        }
                        else
                        {
                            // write the route template value
                            sb.Append('{');

                            if (part.IsCatchAll)
                            {
                                sb.Append('*');
                            }

                            AppendLowerInvariant(sb, parameterName);
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

        // We never added anything, or we just added the first `/`, no need for explicit ToString()
        if (sb.Length <= 1)
        {
            StringBuilderCache.Release(sb);
            return "/";
        }

        return StringBuilderCache.GetStringAndRelease(sb);

        static void AppendLowerInvariant(StringBuilder sb, string? value)
        {
            if (StringUtil.IsNullOrEmpty(value))
            {
                return;
            }

            if (value.Length >= 32)
            {
                // iterating over every character causes too much slow down
                // for large parameters, so just suck up the extra allocation instead
                sb.Append(value.ToLowerInvariant());
                return;
            }

            sb.EnsureCapacity(sb.Length + value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                sb.Append(char.ToLowerInvariant(value[i]));
            }
        }
    }

    [TestingAndPrivateOnly]
    internal static bool IsIdentifierSegment(object? value, out string? valueAsString)
    {
        // Avoid allocating a string for cases we know are going to be flagged as identifiers and therefore
        // If we have "whole number float/double/decimal", then we technically don't need to call ToString()
        // but if they're not whole numbers, we need to do the allocation, due to differences in culture
        // Rather than increase complexity here, we just ignore double/float/decimal
        if (value is short or ushort or int or uint or long or ulong or Guid)
        {
            valueAsString = null;
            return true;
        }

        // This may allocate for e.g. enums and other types etc, but we were going to have to do that anyway at some point
        // NOTE: that this is doing a culture _sensitive_ serialization. Depending on the culture,
        // this could lead to differences in behaviour as to whether a segment is considered an identifier.
        // For example, 1.23 serialized using en-us, is _not_ counted as an identifier, but in fr-FR it _would_ be
        // counted as an identifier.
        valueAsString = value as string ?? value?.ToString();
        if (valueAsString is null)
        {
            return false;
        }

        return UriHelpers.IsIdentifierSegment(valueAsString, 0, valueAsString.Length);
    }
}

#endif
