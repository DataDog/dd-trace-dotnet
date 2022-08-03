// <copyright file="TagPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tagging;

internal static class TagPropagation
{
    /// <summary>
    /// Default value for the maximum length of an outgoing propagation header ("x-datadog-tags").
    /// This value is used when injecting headers and can be overriden with DD_TRACE_X_DATADOG_TAGS_MAX_LENGTH.
    /// </summary>
    public const int OutgoingPropagationHeaderMaxLength = 512;

    /// <summary>
    /// The maximum length of an incoming propagation header ("x-datadog-tags").
    /// This value is used when extracting headers and cannot be overriden via configuration.
    /// </summary>
    public const int IncomingPropagationHeaderMaxLength = 512;

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
    internal const int MinimumPropagationHeaderLength = PropagatedTagPrefixLength + 3;

    private static readonly char[] TagPairSeparators = { TagPairSeparator };

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TagPropagation));

    /// <summary>
    /// Parses the "x-datadog-tags" header value in "key1=value1,key2=value2" format.
    /// Propagated tags require the an "_dd.p.*" prefix, so any other tags are ignored.
    /// </summary>
    /// <param name="propagationHeader">The header value to parse.</param>
    /// <returns>
    /// A <see cref="TraceTagCollection"/> containing the valid tags parsed from the specified header value, if any.
    /// </returns>
    public static TraceTagCollection ParseHeader(string? propagationHeader)
    {
        if (string.IsNullOrEmpty(propagationHeader))
        {
            return new TraceTagCollection();
        }

        List<KeyValuePair<string, string>> traceTags;

        if (propagationHeader!.Length > IncomingPropagationHeaderMaxLength)
        {
            Log.Debug<int, int>("Incoming tag propagation header is too long. Length: {0}, Maximum: {1}.", propagationHeader.Length, IncomingPropagationHeaderMaxLength);

            traceTags = new List<KeyValuePair<string, string>>(1) { new(Tags.TagPropagationError, PropagationErrorTagValues.ExtractMaxSize) };
            return new TraceTagCollection(traceTags);
        }

        var headerTags = propagationHeader.Split(TagPairSeparators, StringSplitOptions.RemoveEmptyEntries);
        traceTags = new List<KeyValuePair<string, string>>(headerTags.Length);
        string? cachedHeader = propagationHeader;
        bool addedErrorTag = false;

        foreach (var headerTag in headerTags)
        {
            // the shortest tag has the "_dd.p." prefix, a 1-character key, and 1-character value (e.g. "_dd.p.a=b")
            if (headerTag.Length >= MinimumPropagationHeaderLength &&
                headerTag.StartsWith(PropagatedTagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // NOTE: the first equals sign is the separator between key/value, but the tag value can contain
                // additional equals signs, so make sure we only split on the _first_ one.
                var separatorIndex = headerTag.IndexOf(KeyValueSeparator);

                // "_dd.p.a=b"
                //         ⬆   separator must be at index 7 or higher and before the end of string
                //  012345678
                if (separatorIndex > PropagatedTagPrefixLength &&
                    separatorIndex < headerTag.Length - 1)
                {
                    // TODO: implement something like StringSegment to avoid allocating new (sub)strings?
                    var key = headerTag.Substring(0, separatorIndex);

                    if (key.Equals("_dd.p.upstream_services", StringComparison.OrdinalIgnoreCase))
                    {
                        // special case: ignore deprecated tag, but don't add the "decoding error" tag.
                        // we can't reuse the same header string if we skip any key/value pair.
                        cachedHeader = null;
                        continue;
                    }

                    var value = headerTag.Substring(separatorIndex + 1);

                    if (IsValid(key, value))
                    {
                        traceTags.Add(new(key, value));
                        continue;
                    }
                }
            }

            if (!addedErrorTag)
            {
                // add "_dd.propagation_error:decoding_error" tag if any of the tags it not valid,
                // but only add the error tag once
                addedErrorTag = true;
                traceTags.Add(new(Tags.TagPropagationError, PropagationErrorTagValues.DecodingError));
            }

            // we can't reuse the same header string if we skip any key/value pair
            cachedHeader = null;
        }

        return traceTags.Count > 0 ? new TraceTagCollection(traceTags, cachedHeader) : new TraceTagCollection();
    }

    /// <summary>
    /// Constructs a string that can be used for horizontal propagation using the "x-datadog-tags" header
    /// in a "key1=value1,key2=value2" format. This header should only include tags with the "_dd.p.*" prefix.
    /// The returned string is cached and reused if no relevant tags are changed between calls.
    /// </summary>
    /// <returns>A string that can be used for horizontal propagation using the "x-datadog-tags" header.</returns>
    public static string ToHeader(TraceTagCollection tagsCollection, int maxOutgoingHeaderLength)
    {
        if (maxOutgoingHeaderLength == 0)
        {
            // propagation is disabled,
            // set tag "_dd.propagation_error:disabled"...
            tagsCollection.SetTag(Tags.TagPropagationError, PropagationErrorTagValues.PropagationDisabled);

            // ... and don't set the header
            return string.Empty;
        }

        var tagsArray = tagsCollection.ToArray();
        var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

        foreach (var tag in tagsArray)
        {
            if (!string.IsNullOrEmpty(tag.Key) &&
                !string.IsNullOrEmpty(tag.Value) &&
                tag.Key.StartsWith(PropagatedTagPrefix, StringComparison.Ordinal))
            {
                if (!IsValid(tag.Key, tag.Value))
                {
                    Log.Debug("Propagated tag is not valid. Key: \"{key}\", Value: \"{value}\"", tag.Key, tag.Value);

                    // if tag contains invalid chars,
                    // set tag "_dd.propagation_error:encoding_error"...
                    tagsCollection.SetTag(Tags.TagPropagationError, PropagationErrorTagValues.EncodingError);

                    // ... and don't set the header
                    StringBuilderCache.Release(sb);
                    return string.Empty;
                }

                if (sb.Length > 0)
                {
                    sb.Append(TagPairSeparator);
                }

                sb.Append(tag.Key)
                  .Append(KeyValueSeparator)
                  .Append(tag.Value);
            }

            if (sb.Length > maxOutgoingHeaderLength)
            {
                Log.Debug<int, int>("Outgoing tag propagation header is too long. Length: {0}, Maximum: {1}.", sb.Length, maxOutgoingHeaderLength);

                // if combined tags get too long for propagation headers,
                // set tag "_dd.propagation_error:inject_max_size"...
                tagsCollection.SetTag(Tags.TagPropagationError, PropagationErrorTagValues.InjectMaxSize);

                // ... and don't set the header
                StringBuilderCache.Release(sb);
                return string.Empty;
            }
        }

        return StringBuilderCache.GetStringAndRelease(sb);
    }

    internal static bool IsValid(string key, string value)
    {
        // keys don't allow comma, space, or equals
        foreach (char c in key)
        {
            if (c is < (char)32 or > (char)126 or ',' or ' ' or '=')
            {
                return false;
            }
        }

        // values don't allow comma
        foreach (char c in value)
        {
            if (c is < (char)32 or > (char)126 or ',')
            {
                return false;
            }
        }

        return true;
    }
}
