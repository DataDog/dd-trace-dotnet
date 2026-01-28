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
    private static readonly Lazy<IReadOnlyCollection<string>> _tagsList = new(() => GetTagsList());
    private static readonly Lazy<string> _serializedTags = new(() => GetSerializedTagsFromList(_tagsList.Value));

    public static IReadOnlyCollection<string> TagsList => _tagsList.Value;

    public static string SerializedTags => _serializedTags.Value;

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

        // NOTE #1: the most correct way of doing this (that handles most edge cases, etc) is
        // new DirectoryInfo(directoryPath).Name, but it allocates several objects. Instead, we'll try
        // to do this with only a single string allocation for the result.

        // NOTE #2: Since directoryPath always comes from either AppContext.BaseDirectory or Environment.CurrentDirectory,
        // we assume it is always a valid and rooted path to keep things simple.

        if (IsRootPath(directoryPath))
        {
            // root paths like "C:\" on Windows or "/" on other OSes
            return directoryPath;
        }

#if NETCOREAPP3_1_OR_GREATER
        // allocate 1-2 char array on the stack
        ReadOnlySpan<char> separators = FrameworkDescription.Instance.IsWindows() ?
                                            stackalloc[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar } :
                                            stackalloc[] { Path.DirectorySeparatorChar };
#else
        // allocate 1-2 char array on the heap :(
        ReadOnlySpan<char> separators = FrameworkDescription.Instance.IsWindows() ?
                                            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar } :
                                            new[] { Path.DirectorySeparatorChar };
#endif

        // Trim trailing separators (non-allocating)
        var span = directoryPath.AsSpan().TrimEnd(separators);

        // Find the last separator after trimming
        var lastSeparatorIndex = span.LastIndexOfAny(separators);

        // If no separator found, return the entire path
        if (lastSeparatorIndex < 0)
        {
            return span.ToString();
        }

        // Return everything after the last separator
        return span.Slice(lastSeparatorIndex + 1).ToString();
    }

    [TestingAndPrivateOnly]
    internal static bool IsRootPath(string directoryPath)
    {
        if (StringUtil.IsNullOrEmpty(directoryPath))
        {
            return false;
        }

        // On Windows: "\" (most common) or "/"
        // Otherwise: "/" only
        if (directoryPath.Length == 1 &&
            (directoryPath[0] == Path.DirectorySeparatorChar || directoryPath[0] == Path.AltDirectorySeparatorChar))
        {
            return true;
        }

        // On Windows, root drive paths look like "C:\" or "D:\".
        // This code does NOT handle device paths like "\\?\." or "\\.\",
        // or server\share paths like "\\server\share" or "\\?\UNC\".
        if (FrameworkDescription.Instance.IsWindows())
        {
            // the common case (not a root) will fail the first check
            return directoryPath.Length is 3 &&
                   char.IsLetter(directoryPath[0]) &&
                   directoryPath[1] == Path.VolumeSeparatorChar &&
                   directoryPath[2] == Path.DirectorySeparatorChar;
        }

        return false;
    }

    private static string? GetEntryPointName()
    {
        return EntryAssemblyLocator.GetEntryAssembly()?.EntryPoint?.DeclaringType?.FullName;
    }
}
