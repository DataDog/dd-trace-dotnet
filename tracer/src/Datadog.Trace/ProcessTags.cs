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
using Datadog.Trace.Util;

namespace Datadog.Trace;

internal static class ProcessTags
{
    public const string EntrypointName = "entrypoint.name";
    public const string EntrypointBasedir = "entrypoint.basedir";
    public const string EntrypointWorkdir = "entrypoint.workdir";

    private static readonly Lazy<string> LazySerializedTags = new(GetSerializedTags);

    public static string SerializedTags
    {
        get => LazySerializedTags.Value;
    }

    /// <summary>
    /// From the full path of a directory, get the name of the leaf directory.
    /// </summary>
    private static string GetLastPathSegment(string directoryPath)
    {
        // Path.GetFileName returns an empty string if the path ends with a '/'.
        // We could use Path.TrimEndingDirectorySeparator instead of the trim here, but it's not available on .NET Framework
        return Path.GetFileName(directoryPath.TrimEnd('\\').TrimEnd('/'));
    }

    private static string GetSerializedTags()
    {
        // ⚠️ make sure entries are added in alphabetical order of keys
        List<KeyValuePair<string, string?>> tags =
        [
            new(EntrypointBasedir, GetLastPathSegment(AppContext.BaseDirectory)),
            new(EntrypointName, EntryAssemblyLocator.GetEntryAssembly()?.EntryPoint?.DeclaringType?.FullName),
            // workdir can be changed by the code, but we consider that capturing the value when this is called is good enough
            new(EntrypointWorkdir, GetLastPathSegment(Environment.CurrentDirectory))
        ];

        // then normalize values and put all tags in a string
        var serializedTags = StringBuilderCache.Acquire();
        foreach (var kvp in tags)
        {
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                serializedTags.Append($"{kvp.Key}:{NormalizeTagValue(kvp.Value!)},");
            }
        }

        serializedTags.Remove(serializedTags.Length - 1, length: 1); // remove last comma
        return StringBuilderCache.GetStringAndRelease(serializedTags);
    }

    private static string NormalizeTagValue(string tagValue)
    {
        // TraceUtil.NormalizeTag does almost exactly what we want, except it allows ':',
        // which we don't want because we use it as a key/value separator.
        return TraceUtil.NormalizeTag(tagValue).Replace(oldChar: ':', newChar: '_');
    }
}
