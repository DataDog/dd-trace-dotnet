// <copyright file="AspNetResourceNameHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    internal static class AspNetResourceNameHelper
    {
        private enum ReplaceType
        {
            ValueOnly = 0,
            ValueAndBraces = 1,
            ValueBracesAndLeadingSlash = 2,
        }

        public static string CalculateResourceName(
            string httpMethod,
            string routeTemplate,
            IDictionary<string, object> routeValues,
            IDictionary<string, object>? defaults,
            out string? areaName,
            out string? controllerName,
            out string? actionName,
            bool expandRouteTemplates) =>
            CalculateResourceName(httpMethod, routeTemplate, routeValues, defaults, out areaName, out controllerName, out actionName, addSlashPrefix: routeTemplate[0] != '/', expandRouteTemplates);

        public static string CalculateResourceName(
            string httpMethod,
            string routeTemplate,
            IDictionary<string, object> routeValues,
            IDictionary<string, object>? defaults,
            out string? areaName,
            out string? controllerName,
            out string? actionName,
            bool addSlashPrefix,
            bool expandRouteTemplates)
        {
            // We could calculate the actual maximum size required by looping through all the
            // route values provided, comparing the parameter sizes to the parameter values
            // (only action/controller/area unless expandRouteParameters = true,
            // but doesn't seem worth it
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

            sb.Append(httpMethod)
              .Append(' ');

            if (addSlashPrefix)
            {
                sb.Append('/');
            }

            sb.Append(routeTemplate.ToLowerInvariant());

            areaName = null;
            controllerName = null;
            actionName = null;

            foreach (var kvp in routeValues)
            {
                if (string.Equals(kvp.Key, "action", StringComparison.OrdinalIgnoreCase) && kvp.Value is string action)
                {
                    actionName = action.ToLowerInvariant();
                    sb.Replace("{action}", actionName);
                }
                else if (string.Equals(kvp.Key, "controller", StringComparison.OrdinalIgnoreCase) && kvp.Value is string controller)
                {
                    controllerName = controller.ToLowerInvariant();
                    sb.Replace("{controller}", controllerName);
                }
                else if (string.Equals(kvp.Key, "area", StringComparison.OrdinalIgnoreCase) && kvp.Value is string area)
                {
                    areaName = area.ToLowerInvariant();
                    sb.Replace("{area}", areaName);
                }
                else if (expandRouteTemplates)
                {
                    var valueAsString = kvp.Value as string ?? kvp.Value?.ToString() ?? string.Empty;
                    if (UriHelpers.IsIdentifierSegment(valueAsString, 0, valueAsString.Length))
                    {
                        // We're replacing the key with itself, so that we strip out all the additional parameters etc
                        // We should probably be doing that for the non-expanded approach too, but we historically haven't
                        ReplaceValue(sb, kvp.Key, kvp.Key, ReplaceType.ValueOnly);
                    }
                    else
                    {
                        ReplaceValue(sb, kvp.Key, valueAsString, ReplaceType.ValueAndBraces);
                    }
                }
            }

            // Remove unused parameters from conventional route templates
            if (defaults is not null)
            {
                foreach (var kvp in defaults)
                {
                    if (routeValues.ContainsKey(kvp.Key))
                    {
                        continue;
                    }

                    ReplaceValue(sb, kvp.Key, null, replaceType: ReplaceType.ValueBracesAndLeadingSlash);
                }
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        private static void ReplaceValue(StringBuilder sb, string key, string? value, ReplaceType replaceType)
        {
            var startIndex = IndexOf(sb, key, out var length, removeLeadingSlash: replaceType == ReplaceType.ValueBracesAndLeadingSlash);

            if (startIndex != -1)
            {
                if (replaceType == ReplaceType.ValueOnly)
                {
                    sb.Remove(startIndex + 1, length - 2);
                    sb.Insert(startIndex + 1, value);
                }
                else
                {
                    sb.Remove(startIndex, length);
                    sb.Insert(startIndex, value?.ToLowerInvariant() ?? string.Empty);
                }
            }

            static int IndexOf(StringBuilder sb, string key, out int length, bool removeLeadingSlash)
            {
                var sbLength = sb.Length;
                var keyLength = key.Length;
                // max start index to replace is {key} or /{key}
                var maxStartIndex = sbLength - keyLength - (removeLeadingSlash ? 3 : 2);
                int endIndex;
                int matchIndex;

                for (var i = 0; i <= maxStartIndex; i++)
                {
                    matchIndex = i;
                    if (removeLeadingSlash)
                    {
                        if (sb[i] != '/')
                        {
                            continue;
                        }

                        matchIndex++;
                    }

                    if (sb[matchIndex] != '{')
                    {
                        continue;
                    }

                    matchIndex++;

                    if (sb[matchIndex] == '*')
                    {
                        // catch all constraint
                        matchIndex++;
                    }

                    if (!IsMatch(sb, matchIndex, key))
                    {
                        continue;
                    }

                    // we know we have `{key` or `{*key`
                    // they _may_ have additional route constraints/defaults etc
                    // so assume they're well formed, and find the final `}`
                    endIndex = matchIndex + keyLength;

                    // first check if we're _not_ in the constraint part
                    if (endIndex < sbLength
                        && sb[endIndex] is not '?' and not ':' and not '=' and not '}')
                    {
                        // we haven't finished the parameter name
                        // e.g `{keyo` or `{*key1`
                        continue;
                    }

                    while (endIndex < sbLength)
                    {
                        if (sb[endIndex] == '}')
                        {
                            // we're done, success
                            length = endIndex - i + 1;
                            return i;
                        }

                        // constraints/optional/default values
                        endIndex++;
                    }

                    // if we fall out of here, there was no closing }
                    // which is an invalid route template, but be safe
                }

                // we didn't find a match
                length = 0;
                return -1;

                static bool IsMatch(StringBuilder sb, int startIndex, string key)
                {
                    var keyLength = key.Length;
                    var keyIndex = 0;
                    while (keyIndex < keyLength)
                    {
                        // this is a case-sensitive comparison, which is probably ok?
                        if (sb[startIndex + keyIndex] != key[keyIndex])
                        {
                            // no match
                            return false;
                        }

                        keyIndex++;
                    }

                    return true;
                }
            }
        }
    }
}
#endif
