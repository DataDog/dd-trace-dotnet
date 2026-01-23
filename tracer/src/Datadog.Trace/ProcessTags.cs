// <copyright file="ProcessTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Processors;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace;

internal static class ProcessTags
{
    public const string EntrypointName = "entrypoint.name";
    public const string EntrypointBasedir = "entrypoint.basedir";
    public const string EntrypointWorkdir = "entrypoint.workdir";

    // two views on the same data
    public static readonly IReadOnlyCollection<string> TagsList = GetTagsList();
    public static readonly string SerializedTags = GetSerializedTagsFromList(TagsList);

    private static List<string> GetTagsList()
    {
        // ⚠️ make sure entries are added in alphabetical order of keys
        var tags = new List<string>(3); // Update if you add more entries below
        tags.AddNormalizedTag(EntrypointBasedir, GetLastPathSegment(AppContext.BaseDirectory));
        tags.AddNormalizedTag(EntrypointName, GetEntryPointName());
        // workdir can be changed by the code, but we consider that capturing the value when this is called is good enough
        tags.AddNormalizedTag(EntrypointWorkdir, GetLastPathSegment(Environment.CurrentDirectory));

        return tags;
    }

    /// <summary>
    /// normalizes the tag value (keys are hardcoded so they don't need that)
    /// and adds it to the list iff not null or empty
    /// </summary>
    private static void AddNormalizedTag(this List<string> tags, string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        // TraceUtil.NormalizeTag does almost exactly what we want, except it allows ':',
        // which we don't want because we use it as a key/value separator.
        var normalizedValue = TraceUtil.NormalizeTag(value).Replace(oldChar: ':', newChar: '_');
        tags.Add($"{key}:{normalizedValue}");
    }

    private static string GetSerializedTagsFromList(IEnumerable<string> tags)
    {
        return string.Join(",", tags);
    }

    /// <summary>
    /// From the full path of a directory, get the name of the leaf directory.
    /// </summary>
    [TestingAndPrivateOnly]
    internal static string GetLastPathSegment(string directoryPath)
    {
        if (StringUtil.IsNullOrEmpty(directoryPath))
        {
            return string.Empty;
        }

        if (IsRootPath(directoryPath))
        {
            // return root paths like "/" or "C:\" as-is
            return directoryPath;
        }

        // Path.GetFileName() returns an empty string if the path ends in a directory separator,
        // so trim those first
        var trimmedPath = TrimEndingDirectorySeparator(directoryPath.AsSpan());

#if NETCOREAPP3_1_OR_GREATER
        // avoid the intermediate string allocation for trimmedPath
        return Path.GetFileName(trimmedPath).ToString();
#else
        return Path.GetFileName(trimmedPath.ToString());
#endif
    }

    [TestingOnly]
    internal static string TrimEndingDirectorySeparator(string path)
    {
        return TrimEndingDirectorySeparator(path.AsSpan()).ToString();
    }

    private static ReadOnlySpan<char> TrimEndingDirectorySeparator(ReadOnlySpan<char> path)
    {
        if (path.IsEmpty)
        {
            return ReadOnlySpan<char>.Empty;
        }

        // Path.TrimEndingDirectorySeparator() is not available in older .NET versions.
        // Not using "#if NETCOREAPP3_1_OR_GREATER" here to keep consistent behavior across TFMs.
        var last = path[path.Length - 1];

        return last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar ?
                   path.Slice(0, path.Length - 1) :
                   path;
    }

    [TestingAndPrivateOnly]
    internal static bool IsRootPath(string path)
    {
        if (StringUtil.IsNullOrEmpty(path))
        {
            return false;
        }

        if (path == "/")
        {
            return true;
        }

        // path could be drive root like C:\
        if (path.Length == 3 &&
            char.IsLetter(path[0]) &&
            path[1] == ':' &&
            (path[2] == Path.DirectorySeparatorChar || path[2] == Path.AltDirectorySeparatorChar))
        {
            return true;
        }

        return false;
    }

    private static string? GetEntryPointName()
    {
        return EntryAssemblyLocator.GetEntryAssembly()?.EntryPoint?.DeclaringType?.FullName;
    }
}
