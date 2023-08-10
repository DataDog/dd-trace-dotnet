// <copyright file="FiltersHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Datadog.Trace.Coverage.Collector;

/// <summary>
/// Coverage filters helper
/// </summary>
internal static class FiltersHelper
{
    private static readonly ConcurrentDictionary<IReadOnlyList<string>, IReadOnlyList<Regex>> AttributesRegexes = new();

    public static bool FilteredByAttribute(string attributeFullName, IReadOnlyList<string> filters)
    {
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
        return false;
    }

    public static bool FilteredByAssemblyAndType(string assemblyOrType, IReadOnlyList<string> filters)
    {
        return false;
    }
}
