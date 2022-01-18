// <copyright file="DatadogTagsHeader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tagging.PropagatedTags;

internal static class DatadogTagsHeader
{
    /*
        tagset = tag, { ",", tag };
        tag = ( identifier - space ), "=", identifier;
        identifier = allowed characters, { allowed characters };
        allowed characters = ( ? ASCII characters 32-126 ? - equal or comma );
        equal or comma = "=" | ",";
        space = " ";
     */

    public const char TagPairSeparator = ',';
    public const char KeyValueSeparator = '=';

    public static string? GetTagValue(string? headers, string key)
    {
        if (headers is null or { Length: 0 })
        {
            return null;
        }

        if (FindTag(headers, key, out var valueIndex, out var valueLength))
        {
            return headers.Substring(valueIndex, valueLength);
        }

        return null;
    }

    private static bool FindTag(string headers, string key, out int valueIndex, out int valueLength)
    {
        valueIndex = -1;
        valueLength = -1;

        if (string.IsNullOrWhiteSpace(headers))
        {
            return false;
        }

        int searchStartIndex = 0;

        while (searchStartIndex < headers!.Length)
        {
            var keyIndex = headers.IndexOf(key, searchStartIndex, StringComparison.Ordinal);

            if (keyIndex < 0)
            {
                // key not found
                return false;
            }

            int separatorIndex = keyIndex + key.Length;

            if (separatorIndex < headers.Length - 1 && headers[separatorIndex] != KeyValueSeparator)
            {
                // found "key", but not "key=",
                // this looks like a different key that starts the same ("keyfoo="),
                // or we found the key inside a value ("foo=key"),
                // skip ahead and keep looking
                searchStartIndex = separatorIndex;
                continue;
            }

            if (keyIndex > 0 && headers[keyIndex - 1] != TagPairSeparator)
            {
                // found "key", but not at the beginning of string and not preceded by tag separator ("...,key="),
                // this looks like a different key that ends the same ("fookey="),
                // skip ahead and keep looking
                searchStartIndex = separatorIndex;
                continue;
            }

            valueIndex = keyIndex + key.Length + 1;

            // find the end of the tag's value
            var valueEndIndex = headers.IndexOf(TagPairSeparator, valueIndex);

            if (valueEndIndex < 0)
            {
                // tag separator not found, so the tag's value reaches the end of string
                valueEndIndex = headers.Length;
            }

            valueLength = valueEndIndex - valueIndex;
            return true;
        }

        // we should never reach this line
        return false;
    }

    /// <summary>
    /// Adds or replaces an <see cref="UpstreamService"/> tag on the specified list of headers.
    /// If the tag does not already exists, it is appended at the end as a new tag. If the tag already exists,
    /// the new value is appended to the existing tag using <see cref="UpstreamService.GroupSeparator"/>.
    /// </summary>
    /// <param name="headers">The list of tags, if any, to add a new tag to.</param>
    /// <param name="tag">The tag to add.</param>
    /// <returns>A new list of tags.</returns>
    public static string SetTagValue(string headers, UpstreamService tag)
    {
        return SetTagValue(
            headers,
            UpstreamService.GroupSeparator,
            key: Tags.Propagated.UpstreamServices,
            value: tag.ToString());
    }

    /// <summary>
    /// Adds or replaces an arbitrary tag (key/value pair) on the specified list of headers.
    /// If the tag does not already exists, it is appended at the end as a new tag. If the tag already exists,
    /// the new value is appended to the existing tag using <paramref name="tagValueSeparator"/>.
    /// </summary>
    /// <param name="headers">The list of tags, if any, to add a new tag to.</param>
    /// <param name="tagValueSeparator">Separator used between multiple values of the same tag.</param>
    /// <param name="key">The name of the tag to add or append to.</param>
    /// <param name="value">The value of the tag to add or append.</param>
    /// <returns>A new list of tags.</returns>
    public static string SetTagValue(string headers, char tagValueSeparator, string key, string value)
    {
        StringBuilder sb;

        if (FindTag(headers, key, out var valueIndex, out var valueLength))
        {
            // tag already exists
            sb = StringBuilderCache.Acquire(headers.Length - valueLength + value.Length);

            sb.Append(headers, startIndex: 0, count: valueLength)
              .Append(value);

            var valueEndIndex = valueIndex + valueLength;

            if (valueEndIndex < headers.Length - 1)
            {
                sb.Append(headers, startIndex: valueEndIndex, count: headers.Length - valueEndIndex);
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        // key not found, append as new key/value pair
        sb = StringBuilderCache.Acquire(headers.Length + key.Length + value.Length + 2);
        sb.Append(headers);
        AppendKeyValuePair(sb, key, value);
        return StringBuilderCache.GetStringAndRelease(sb);
    }

    /*
    /// <summary>
    /// Adds or appends an <see cref="UpstreamService"/> tag to the specified list of tags.
    /// </summary>
    /// <param name="headers">The list of tags, if any, to add a new tag to.</param>
    /// <param name="tag">The tag to add.</param>
    /// <returns>A new list of tags.</returns>
    public static string AppendTagValue(string? headers, UpstreamService tag)
    {
        return AppendTagValue(
            headers,
            UpstreamService.GroupSeparator,
            key: Tags.Propagated.UpstreamServices,
            value: tag.ToString());
    }

    /// <summary>
    /// Adds an arbitrary tag (key/value pair) to <paramref name="headers"/>. If the tag does not already exists,
    /// it is appended at the end of <paramref name="headers"/> as a new tag. If the tag already exists,
    /// the new value is appended to the existing tag using <paramref name="tagValueSeparator"/>.
    /// </summary>
    /// <param name="headers">The list of tags, if any, to add a new tag to.</param>
    /// <param name="tagValueSeparator">Separator used between multiple values of the same tag.</param>
    /// <param name="key">The name of the tag to add or append to.</param>
    /// <param name="value">The value of the tag to add or append.</param>
    /// <returns>A new list of tags.</returns>
    public static string AppendTagValue(string? headers, char tagValueSeparator, string key, string value)
    {
        if (string.IsNullOrEmpty(headers))
        {
            var sb = StringBuilderCache.Acquire(key.Length + value.Length + 1);
            sb.Append(headers);
            AppendKeyValuePair(sb, key, value);
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        return AppendTagValueWithSearch(headers ?? string.Empty, tagValueSeparator, key, value);
    }

    private static string AppendTagValueWithSearch(string headers, char tagValueSeparator, string key, string value)
    {
        if (FindTag(headers, key, out var valueIndex, out var valueLength))
        {
            // tag already exists, find the end of its current value
            var sb = StringBuilderCache.Acquire(headers.Length + 1 + value.Length);
            var valueEndIndex = valueIndex + valueLength;

            sb.Append(headers, startIndex: 0, count: valueEndIndex)
              .Append(tagValueSeparator)
              .Append(value);

            if (valueEndIndex < headers.Length - 1)
            {
                sb.Append(headers, startIndex: valueEndIndex, count: headers.Length - valueEndIndex);
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }
        else
        {
            // key not found, append as new key/value pair
            var sb = StringBuilderCache.Acquire(headers.Length + key.Length + value.Length + 2);
            sb.Append(headers);
            AppendKeyValuePair(sb, key, value);
            return StringBuilderCache.GetStringAndRelease(sb);
        }
    }
    */

    private static void AppendKeyValuePair(StringBuilder sb, string key, string value)
    {
        if (sb.Length > 0)
        {
            sb.Append(TagPairSeparator);
        }

        sb.Append(key)
          .Append(KeyValueSeparator)
          .Append(value);
    }

    public static string Serialize(KeyValuePair<string, string?>[]? tags)
    {
        if (tags == null || tags.Length == 0)
        {
            return string.Empty;
        }

        int totalLength = 0;

        foreach (var tag in tags)
        {
            if (!string.IsNullOrEmpty(tag.Value))
            {
                // ",{key}={value}", we'll go over by one comma but that's fine
                totalLength += tag.Key.Length + tag.Value!.Length + 2;
            }
        }

        var sb = StringBuilderCache.Acquire(totalLength);

        foreach (var tag in tags)
        {
            if (!string.IsNullOrEmpty(tag.Value))
            {
                AppendKeyValuePair(sb, tag.Key, tag.Value!);
            }
        }

        return StringBuilderCache.GetStringAndRelease(sb);
    }

    public static List<KeyValuePair<string, string>> Parse(string datadogTags)
    {
        var pairs = datadogTags.Split(TagPairSeparator);
        var list = new List<KeyValuePair<string, string>>(capacity: pairs.Length);

        foreach (var pair in pairs)
        {
            var keyValueSeparatorIndex = pair.IndexOf(KeyValueSeparator);

            if (keyValueSeparatorIndex > 0)
            {
                var key = pair.Substring(0, keyValueSeparatorIndex);
                var value = pair.Substring(keyValueSeparatorIndex);
                list.Add(new KeyValuePair<string, string>(key, value));
            }
        }

        return list;
    }
}
