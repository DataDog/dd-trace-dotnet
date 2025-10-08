// <copyright file="ProcessTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Datadog.Trace.Processors;

namespace Datadog.Trace;

internal static class ProcessTags
{
    public const string EntrypointName = "entrypoint.name";
    public const string EntrypointBasedir = "entrypoint.basedir";
    public const string EntrypointWorkdir = "entrypoint.workdir";

    private static Lazy<Dictionary<string, string>> _tags = new(LoadBaseTags);

    private static Dictionary<string, string> LoadBaseTags()
    {
        var tags = new Dictionary<string, string>();

        var entrypointFullName = Assembly.GetEntryAssembly()?.EntryPoint?.DeclaringType?.FullName;
        if (!string.IsNullOrEmpty(entrypointFullName))
        {
            tags.Add(EntrypointName, entrypointFullName);
        }

        tags.Add(EntrypointBasedir, GetLastPathSegment(AppContext.BaseDirectory));
        tags.Add(EntrypointWorkdir, GetLastPathSegment(Environment.CurrentDirectory)); // ⚠️ can be changed by the code, not constant

        return tags;
    }

    /// <summary>
    /// From the full path of a directory, get the name of the leaf directory.
    /// </summary>
    private static string GetLastPathSegment(string directoryPath)
    {
        // Path.GetFileName returns an empty string if the path ends with a '/'.
        // We could use Path.TrimEndingDirectorySeparator instead of the trim here, but it's not available on .netframework
        return Path.GetFileName(directoryPath.TrimEnd('\\', '/'));
    }

    public static string GetSerializedTags()
    {
        return string.Join(separator: ',', _tags.Value.Select(kv => $"{kv.Key}:{NormalizeTagValue(kv.Value)}"));
    }

    private static string NormalizeTagValue(string tagValue)
    {
        // TraceUtil.NormalizeTag does almost exactly what we want, except it allows ':',
        // which we don't want because we use it as a key/value separator.
        return TraceUtil.NormalizeTag(tagValue).Replace(oldChar: ':', newChar: '_');
    }
}
