// <copyright file="FiltersHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.Coverage.Collector;

/// <summary>
/// Coverage filters helper
/// </summary>
internal static class FiltersHelper
{
    public static bool FilteredByAttribute(string attributeFullName, IReadOnlyList<string> filters)
    {
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
