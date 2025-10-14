// <copyright file="ProcessTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Processors;

namespace Datadog.Trace;

internal static class ProcessTags
{
    public const string EntrypointName = "entrypoint.name";
    public const string EntrypointBasedir = "entrypoint.basedir";
    public const string EntrypointWorkdir = "entrypoint.workdir";

    private static Lazy<string> _lazySerializedTags = new(GetSerializedTags);

    public static string SerializedTags
    {
        get => _lazySerializedTags.Value;
    }

    private static Dictionary<string, string> LoadBaseTags()
    {
        var tags = new Dictionary<string, string>();

        if (!Tracer.Instance.Settings.PropagateProcessTags)
        {
            // do not collect anything when disabled
            return tags;
        }

        var entrypointFullName = Assembly.GetEntryAssembly()?.EntryPoint?.DeclaringType?.FullName;
        if (!string.IsNullOrEmpty(entrypointFullName))
        {
            tags.Add(EntrypointName, entrypointFullName!);
        }

        tags.Add(EntrypointBasedir, GetLastPathSegment(AppContext.BaseDirectory));
        // workdir can be changed by the code, but we consider that capturing the value when this is called is good enough
        tags.Add(EntrypointWorkdir, GetLastPathSegment(Environment.CurrentDirectory));

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

    private static string GetSerializedTags()
    {
        return string.Join(",", LoadBaseTags().OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{NormalizeTagValue(kv.Value)}"));
    }

    private static string NormalizeTagValue(string tagValue)
    {
        // TraceUtil.NormalizeTag does almost exactly what we want, except it allows ':',
        // which we don't want because we use it as a key/value separator.
        return TraceUtil.NormalizeTag(tagValue).Replace(oldChar: ':', newChar: '_');
    }

    internal static void ResetForTests()
    {
        _lazySerializedTags = new Lazy<string>(GetSerializedTags);
    }
}
