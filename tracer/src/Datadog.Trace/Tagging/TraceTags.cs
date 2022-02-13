// <copyright file="TraceTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Tagging.PropagatedTags;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tagging
{
    internal class TraceTags
    {
        private readonly List<KeyValuePair<string, string>> _tags;

        public TraceTags(List<KeyValuePair<string, string>> tags)
        {
            _tags = tags;
        }

        public static TraceTags Parse(string concatenatedTags)
        {
            var tags = concatenatedTags.Split(DatadogTagsHeader.TagPairSeparator);
            var tagList = new List<KeyValuePair<string, string>>(tags.Length);

            foreach (var tag in tags)
            {
                var separatorIndex = tag.IndexOf(DatadogTagsHeader.KeyValueSeparator);

                if (separatorIndex > 0)
                {
                    var key = tag.Substring(0, separatorIndex);
                    var value = tag.Substring(separatorIndex);
                    tagList.Add(new(key, value));
                }
            }

            return new TraceTags(tagList);
        }

        public void SetTag(string key, string value)
        {
            foreach (var tag in _tags)
            {

            }
        }

        public override string ToString()
        {
            if (_tags == null || _tags.Count == 0)
            {
                return string.Empty;
            }

            // one '=' for each tag (_tags.Count) and one ',' between each tag (_tags.Count - 1)
            int length = (_tags.Count * 2) - 1;
            var cachedStringBuilder = StringBuilderCache.Acquire(length);

            foreach (var tag in _tags)
            {
                if (cachedStringBuilder.Length > 0)
                {
                    cachedStringBuilder.Append(DatadogTagsHeader.TagPairSeparator);
                }

                cachedStringBuilder.Append(tag.Key)
                                   .Append(DatadogTagsHeader.KeyValueSeparator)
                                   .Append(tag.Value);
            }

            return StringBuilderCache.GetStringAndRelease(cachedStringBuilder);
        }
    }
}
