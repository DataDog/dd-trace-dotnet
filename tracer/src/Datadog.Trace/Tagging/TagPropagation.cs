// <copyright file="TagPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tagging;

internal static class TagPropagation
{
    // tags with this prefix are propagated horizontally
    // (i.e. from upstream services and to downstream services)
    // using the "x-datadog-tags" header
    public const string PropagatedTagPrefix = "_dd.p.";

    // "x-datadog-tags" header format is "key1=value1,key2=value2"
    private const char TagPairSeparator = ',';
    private const char KeyValueSeparator = '=';

    private const int PropagatedTagPrefixLength = 6; // "_dd.p.".Length

    // the smallest possible header length, 1-char key and 1-char value:
    // "_dd.p.a=b" = "_dd.p.".Length + "a=b".Length
    public const int MinimumPropagationHeaderLength = PropagatedTagPrefixLength + 3;

    private static readonly char[] TagPairSeparators = { TagPairSeparator };

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TagPropagation));

    /// <summary>
    /// Parses the "x-datadog-tags" header value in "key1=value1,key2=value2" format.
    /// Propagated tags require the an "_dd.p.*" prefix, so any other tags are ignored.
    /// </summary>
    /// <param name="propagationHeader">The header value to parse.</param>
    /// <param name="maxHeaderLength">The maximum length allowed for incoming propagation header.</param>
    /// <param name="tagCollection">When this methods returns, contains the tag collection parsed from the header.</param>
    /// <returns>
    /// A list of valid tags parsed from the specified header value,
    /// or null if <paramref name="propagationHeader"/> is <c>null</c> or empty.
    /// </returns>
    public static bool TryParseHeader(
        string? propagationHeader,
        int maxHeaderLength,
        [NotNullWhen(returnValue: true)] out TraceTagCollection? tagCollection)
    {
        if (string.IsNullOrEmpty(propagationHeader))
        {
            tagCollection = null;
            return false;
        }

        List<KeyValuePair<string, string>> traceTags;

        if (propagationHeader!.Length > maxHeaderLength)
        {
            Log.Debug<int, int>("Incoming tag propagation header too long. Length {0}, maximum {1}.", propagationHeader.Length, maxHeaderLength);

            traceTags = new List<KeyValuePair<string, string>>(1)
                        {
                            new(Tags.TagPropagation.Error, PropagationErrorTagValues.ExtractMaxSize)
                        };

            tagCollection = new TraceTagCollection(traceTags);
            return false;
        }

        var headerTags = propagationHeader.Split(TagPairSeparators, StringSplitOptions.RemoveEmptyEntries);
        traceTags = new List<KeyValuePair<string, string>>(headerTags.Length);

        foreach (var headerTag in headerTags)
        {
            // the shortest tag has the "_dd.p." prefix, a 1-character key, and 1-character value (e.g. "_dd.p.a=b")
            if (headerTag.Length >= MinimumPropagationHeaderLength &&
                headerTag.StartsWith(PropagatedTagPrefix, StringComparison.Ordinal))
            {
                // NOTE: the first equals sign is the separator between key/value, but the tag value can contain
                // additional equals signs, so make sure we only split on the _first_ one. For example,
                // the "_dd.p.upstream_services" tag will have base64-encoded strings which use '=' for padding.
                var separatorIndex = headerTag.IndexOf(KeyValueSeparator);

                // "_dd.p.a=b"
                //         ⬆   separator must be at index 7 or higher and before the end of string
                //  012345678
                if (separatorIndex > PropagatedTagPrefixLength &&
                    separatorIndex < headerTag.Length - 1)
                {
                    // TODO: implement something like StringSegment to avoid allocating new (sub)strings?
                    var name = headerTag.Substring(0, separatorIndex);
                    var value = headerTag.Substring(separatorIndex + 1);

                    traceTags.Add(new(name, value));
                }
            }
        }

        tagCollection = new TraceTagCollection(traceTags, maxHeaderLength);
        return true;
    }

    /// <summary>
    /// Constructs a string that can be used for horizontal propagation using the "x-datadog-tags" header
    /// in a "key1=value1,key2=value2" format. This header should only include tags with the "_dd.p.*" prefix.
    /// The returned string is cached and reused if no relevant tags are changed between calls.
    /// </summary>
    /// <returns>A string that can be used for horizontal propagation using the "x-datadog-tags" header.</returns>
    public static string ToHeader(TraceTagCollection tags, int maxHeaderLength)
    {
        if (tags.Count == 0)
        {
            return string.Empty;
        }

        // validate all tags first
        foreach (var tag in tags)
        {
            // TODO
        }

        var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

        foreach (var tag in tags)
        {
            if (!string.IsNullOrEmpty(tag.Key) &&
                !string.IsNullOrEmpty(tag.Value) &&
                tag.Key.StartsWith(PropagatedTagPrefix, StringComparison.Ordinal))
            {
                if (sb.Length > 0)
                {
                    sb.Append(TagPairSeparator);
                }

                sb.Append(tag.Key)
                  .Append(KeyValueSeparator)
                  .Append(tag.Value);
            }

            if (sb.Length > maxHeaderLength)
            {
                // if combined tags get too long for propagation headers,
                // set tag "_dd.propagation_error:inject_max_size"...
                tags.SetTag(Tags.TagPropagation.Error, PropagationErrorTagValues.InjectMaxSize);

                // ... and don't set the header
                return string.Empty;
            }
        }

        return StringBuilderCache.GetStringAndRelease(sb);
    }
}
