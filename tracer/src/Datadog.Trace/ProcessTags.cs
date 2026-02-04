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
    public static IReadOnlyCollection<string> TagsList => field ??= GetTagsList();

    public static string SerializedTags => field ??= string.Join(",", TagsList);

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

    /// <summary>
    /// From the full path of a directory, get the name of the leaf directory.
    /// </summary>
    [TestingAndPrivateOnly]
    internal static string GetLastPathSegment(string directoryPath)
    {
        // See https://learn.microsoft.com/en-us/dotnet/standard/io/file-path-formats
        // Use DirectoryInfo.Name because it correctly handles
        // - "/" and "\" separators
        // - paths with or without trailing separators
        // - root paths like "/" or "C:\"
        // - other edge cases on Windows like device paths ("\\?\.\" etc) or "\\server\share" UNC paths
        return StringUtil.IsNullOrEmpty(directoryPath) ?
                   string.Empty :
                   new DirectoryInfo(directoryPath).Name;
    }

    private static string? GetEntryPointName()
    {
        return EntryAssemblyLocator.GetEntryAssembly()?.EntryPoint?.DeclaringType?.FullName;
    }
}
