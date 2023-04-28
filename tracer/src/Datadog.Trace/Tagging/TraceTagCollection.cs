// <copyright file="TraceTagCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tagging
{
    internal class TraceTagCollection
    {
        private List<KeyValuePair<string, string>>? _tags;
        private string? _cachedPropagationHeader;
        private string? _samplingMechanismValue;

        public TraceTagCollection()
        {
        }

        public TraceTagCollection(List<KeyValuePair<string, string>>? tags, string? cachedPropagationHeader)
        {
            if (tags?.Count > 0)
            {
                lock (tags)
                {
                    KeyValuePair<string, string>? samplingMechanismPair = null;
                    foreach (var item in tags)
                    {
                        if (item.Key == Trace.Tags.Propagated.DecisionMaker)
                        {
                            samplingMechanismPair = item;
                            break;
                        }
                    }

                    if (samplingMechanismPair != null)
                    {
                        tags.Remove(samplingMechanismPair.Value);
                        _samplingMechanismValue = samplingMechanismPair.Value.Value;
                    }
                }
            }

            _tags = tags;
            _cachedPropagationHeader = cachedPropagationHeader;
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="TraceTagCollection"/>.
        /// </summary>
        public int Count => (_tags?.Count ?? 0) + (_samplingMechanismValue != null ? 1 : 0);

        /// <summary>
        /// Adds a new tag to the collection.
        /// If the tag already exists, is not modified.
        /// </summary>
        /// <param name="name">The name of the tag.</param>
        /// <param name="value">The value of the tag.</param>
        /// <returns><see langword="true"/> if the tag is added to the collection, <see langword="false"/> otherwise.</returns>
        public bool TryAddTag(string name, string value)
        {
            if (value == null!)
            {
                // if tag exists we won't change it, and if it doesn't exist we won't add it,
                // so nothing to do here
                return false;
            }

            return SetTag(name, value, replaceIfExists: false);
        }

        /// <summary>
        /// Adds a new tag to the collection if it doesn't already exists,
        /// or updates the tag with a new value if it already exists.
        /// If the tag value is <see langword="null"/>, the tag is not added to the collection,
        /// and its previous value is removed if found.
        /// </summary>
        /// <param name="name">The name of the tag.</param>
        /// <param name="value">The value of the tag.</param>
        /// <returns>
        /// <see langword="true"/> if the collection is modified by adding, updating, or removing a tag,
        /// <see langword="false"/> otherwise.
        /// </returns>
        public bool SetTag(string name, string? value)
        {
            return SetTag(name, value, replaceIfExists: true);
        }

        private bool SetTag(string name, string? value, bool replaceIfExists)
        {
            if (name == null!)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(name));
            }

            if (value == null)
            {
                return RemoveTag(name);
            }

            if (name == Trace.Tags.Propagated.DecisionMaker)
            {
                if (_samplingMechanismValue != null && !replaceIfExists)
                {
                    return false;
                }

                _samplingMechanismValue = value;
                return true;
            }

            var tags = _tags;
            if (tags == null)
            {
                var newTags = new List<KeyValuePair<string, string>>(2);
                tags = Interlocked.CompareExchange(ref _tags, newTags, null) ?? newTags;
            }

            lock (tags)
            {
                // we have some tags already, try to find this one
                for (var i = 0; i < tags.Count; i++)
                {
                    if (string.Equals(tags[i].Key, name, StringComparison.OrdinalIgnoreCase))
                    {
                        // found the tag
                        if (replaceIfExists)
                        {
                            if (!string.Equals(tags[i].Value, value, StringComparison.Ordinal))
                            {
                                // tag already exists with different value, replace it
                                tags[i] = new(name, value);

                                // clear the cached header
                                if (name.StartsWith(TagPropagation.PropagatedTagPrefix, StringComparison.OrdinalIgnoreCase))
                                {
                                    _cachedPropagationHeader = null;
                                }

                                return true;
                            }
                        }

                        // tag exists but replaceIfExists is false, don't modify anything
                        return false;
                    }
                }

                // tag not found

                // add new tag
                tags.Add(new(name, value));

                // clear the cached header if we added a propagated tag
                if (name.StartsWith(TagPropagation.PropagatedTagPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    _cachedPropagationHeader = null;
                }

                return true;
            }
        }

        public bool RemoveTag(string name)
        {
            if (name == null!)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(name));
            }

            if (name == Trace.Tags.Propagated.DecisionMaker)
            {
                _samplingMechanismValue = null;
                return true;
            }

            var tags = _tags;
            if (tags == null || tags.Count == 0)
            {
                // tag not found
                return false;
            }

            lock (tags)
            {
                for (var i = 0; i < tags.Count; i++)
                {
                    if (string.Equals(tags[i].Key, name, StringComparison.OrdinalIgnoreCase))
                    {
                        tags.RemoveAt(i);

                        // clear the cached header
                        if (name.StartsWith(TagPropagation.PropagatedTagPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            _cachedPropagationHeader = null;
                        }

                        return true;
                    }
                }
            }

            // tag not found
            return false;
        }

        public string? GetTag(string name)
        {
            if (name == null!)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(name));
            }

            if (name == Trace.Tags.Propagated.DecisionMaker)
            {
                return _samplingMechanismValue;
            }

            var tags = _tags;
            if (tags == null || tags.Count == 0)
            {
                return null;
            }

            lock (tags)
            {
                for (var i = 0; i < tags.Count; i++)
                {
                    if (string.Equals(tags[i].Key, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return tags[i].Value;
                    }
                }
            }

            return null;
        }

        public void SetTags(TraceTagCollection? tags)
        {
            if (tags?.Count > 0)
            {
                var traceTagsSetter = new TraceTagSetter(this);
                tags.Enumerate(ref traceTagsSetter);
            }
        }

        public void FixTraceIdTag(TraceId traceId)
        {
            var tagValue = GetTag(Trace.Tags.Propagated.TraceIdUpper);

            if (traceId.Upper > 0)
            {
                // add missing "_dd.p.tid" tag with the upper 64 bits of the trace id,
                // or replace existing tag if it has the wrong value
                // (parse the hex string and compare ulongs to avoid allocating another string)
                if (tagValue == null || !HexString.TryParseUInt64(tagValue, out var currentValue) || currentValue != traceId.Upper)
                {
                    SetTag(Trace.Tags.Propagated.TraceIdUpper, HexString.ToHexString(traceId.Upper));
                }
            }
            else if (traceId.Upper == 0 && tagValue != null)
            {
                // remove tag "_dd.p.tid" if trace id is only 64 bits
                RemoveTag(Trace.Tags.Propagated.TraceIdUpper);
            }
        }

        /// <summary>
        /// Constructs a string that can be used for horizontal propagation using the "x-datadog-tags" header
        /// in a "key1=value1,key2=value2" format. This header should only include tags with the "_dd.p.*" prefix.
        /// The returned string is cached and reused if no relevant tags are changed between calls.
        /// </summary>
        /// <returns>A string that can be used for horizontal propagation using the "x-datadog-tags" header.</returns>
        public string ToPropagationHeader(int? maximumHeaderLength)
        {
            return _cachedPropagationHeader ??= TagPropagation.ToHeader(this, maximumHeaderLength ?? TagPropagation.OutgoingTagPropagationHeaderMaxLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enumerate<TTagEnumerator>(ref TTagEnumerator tagEnumerator)
            where TTagEnumerator : struct, ITagEnumerator
        {
            if (_samplingMechanismValue != null)
            {
                tagEnumerator.Next(new KeyValuePair<string, string>(Trace.Tags.Propagated.DecisionMaker, _samplingMechanismValue));
            }

            var tags = _tags;
            if (tags is null || tags.Count == 0)
            {
                return;
            }

            lock (tags)
            {
                for (var i = 0; i < tags.Count; i++)
                {
                    tagEnumerator.Next(tags[i]);
                }
            }
        }

#pragma warning disable SA1201
        public interface ITagEnumerator
#pragma warning restore SA1201
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void Next(KeyValuePair<string, string> item);
        }

        internal readonly struct TraceTagSetter : ITagEnumerator
        {
            private readonly TraceTagCollection _traceTagCollection;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal TraceTagSetter(TraceTagCollection traceTagCollection)
            {
                _traceTagCollection = traceTagCollection;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Next(KeyValuePair<string, string> tag)
            {
                _traceTagCollection.SetTag(tag.Key, tag.Value);
            }
        }
    }
}
