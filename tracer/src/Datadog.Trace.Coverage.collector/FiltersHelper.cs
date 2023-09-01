// <copyright file="FiltersHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Datadog.Trace.Coverage.Collector;

/// <summary>
/// Coverage filters helper
/// </summary>
internal static class FiltersHelper
{
    private static readonly ConcurrentDictionary<IReadOnlyList<string>, IReadOnlyList<Regex>> AttributesRegexes = new();
    // https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.filesystemglobbing.matcher?view=dotnet-plat-ext-6.0
    private static readonly ConcurrentDictionary<IReadOnlyList<string>, Tuple<Matcher, IReadOnlyList<Regex>>> Matchers = new();

    public static bool FilteredByAttribute(string attributeFullName, IReadOnlyList<string> filters)
    {
        if (filters.Count == 0)
        {
            return false;
        }

        var regexes = AttributesRegexes.GetOrAdd(
            filters,
            list =>
            {
                var lstRegex = new List<Regex>(list.Count);
                foreach (var item in list)
                {
                    lstRegex.Add(new Regex(item, RegexOptions.Compiled));
                }

                return lstRegex;
            });

        foreach (var regex in regexes)
        {
            if (regex.IsMatch(attributeFullName))
            {
                return true;
            }
        }

        return false;
    }

    public static bool FilteredBySourceFile(string sourcePath, IReadOnlyList<string> filters)
    {
        if (filters.Count == 0 || string.IsNullOrWhiteSpace(sourcePath))
        {
            return false;
        }

        try
        {
            var value = Matchers.GetOrAdd(
                filters,
                list =>
                {
                    var instance = new Matcher();
                    var lstRegex = new List<Regex>();
                    foreach (var filter in list)
                    {
                        if (filter is null)
                        {
                            continue;
                        }

                        if (!filter.Contains("**"))
                        {
                            try
                            {
                                lstRegex.Add(new Regex(filter, RegexOptions.Compiled));
                            }
                            catch
                            {
                                // .
                            }
                        }

                        instance.AddInclude(Path.IsPathRooted(filter) ? filter.Substring(Path.GetPathRoot(filter).Length) : filter);
                    }

                    return Tuple.Create(instance, (IReadOnlyList<Regex>)lstRegex);
                });

            var matcher = value.Item1;
            // https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.filesystemglobbing.matcher?view=dotnet-plat-ext-6.0
            var globbingResult = matcher.Match(Path.IsPathRooted(sourcePath) ? sourcePath.Substring(Path.GetPathRoot(sourcePath).Length) : sourcePath).HasMatches;
            if (globbingResult)
            {
                return true;
            }

            foreach (var regex in value.Item2)
            {
                if (regex.IsMatch(sourcePath))
                {
                    return true;
                }
            }
        }
        catch
        {
            // We don't have any logger here to report the problem, we just skip the check.
        }

        return false;
    }

    public static bool FilteredByAssemblyAndType(string module, string? type, IReadOnlyList<string> filters)
    {
        if (filters.Count == 0)
        {
            return false;
        }

        module = Path.GetFileNameWithoutExtension(module);
        if (module == null)
        {
            return false;
        }

        foreach (var filter in filters)
        {
            try
            {
                if (!IsValidFilterExpression(filter))
                {
                    // The filter is invalid so we skip it
                    continue;
                }

                var indexOfModuleTypeSeparator = filter.IndexOf(']');
                var typePattern = filter.Substring(indexOfModuleTypeSeparator + 1);
                var modulePattern = filter.Substring(1, indexOfModuleTypeSeparator - 1);
                var moduleRegex = new Regex(WildcardToRegex(modulePattern));

                if (typePattern == "*")
                {
                    if (moduleRegex.IsMatch(module))
                    {
                        return true;
                    }
                }
                else if (type is not null)
                {
                    var typeRegex = new Regex(WildcardToRegex(typePattern));
                    if (moduleRegex.IsMatch(module) &&
                        typeRegex.IsMatch(type))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // We don't have any logger here to report the problem, we just skip the problematic filter.
            }
        }

        return false;

        static string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern).
                               Replace("\\*", ".*").
                               Replace("\\?", "?") + "$";
        }
    }

    public static bool IsValidFilterExpression(string? filter)
    {
        if (filter == null)
        {
            return false;
        }

        if (!filter.StartsWith("["))
        {
            return false;
        }

        if (!filter.Contains("]"))
        {
            return false;
        }

        if (filter.Count(f => f == '[') > 1)
        {
            return false;
        }

        if (filter.Count(f => f == ']') > 1)
        {
            return false;
        }

        if (filter.IndexOf(']') < filter.IndexOf('['))
        {
            return false;
        }

        if (filter.IndexOf(']') - filter.IndexOf('[') == 1)
        {
            return false;
        }

        if (filter.EndsWith("]"))
        {
            return false;
        }

        if (new Regex(@"[^\w*]").IsMatch(filter.Replace(".", string.Empty).Replace("?", string.Empty).Replace("[", string.Empty).Replace("]", string.Empty)))
        {
            return false;
        }

        return true;
    }
}
