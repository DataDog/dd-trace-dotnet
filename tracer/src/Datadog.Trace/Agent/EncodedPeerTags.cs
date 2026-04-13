// <copyright file="EncodedPeerTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent;

/// <summary>
/// A class that holds the encoded peer tags for a given <see cref="StatsAggregator"/> bucket.
/// Must always be disposed to ensure pooled arrays are released.
/// </summary>
internal sealed class EncodedPeerTags : IDisposable
{
    public static readonly List<byte[]> EmptyTags = [];
    private List<ArraySegment<byte>>? _utf8PeerTags;

    public ArraySegment<byte> EncodeAndSavePeerTag(string tagKey, string tagValue)
    {
        // Encode key, ':', and value directly into the rented buffer to avoid
        // allocating an intermediate interpolated string.
        var maxBytes = EncodingHelpers.Utf8NoBom.GetMaxByteCount(tagKey.Length + 1 + tagValue.Length);
        var bytes = ArrayPool<byte>.Shared.Rent(maxBytes);

        var byteCount = EncodingHelpers.Utf8NoBom.GetBytes(tagKey, charIndex: 0, charCount: tagKey.Length, bytes, byteIndex: 0);
        bytes[byteCount++] = (byte)':';
        byteCount += EncodingHelpers.Utf8NoBom.GetBytes(tagValue, charIndex: 0, charCount: tagValue.Length, bytes, byteIndex: byteCount);

        _utf8PeerTags ??= new();
        var arraySegment = new ArraySegment<byte>(bytes, offset: 0, count: byteCount);
        _utf8PeerTags.Add(arraySegment);
        return arraySegment;
    }

    public List<byte[]> GetPeerTags()
    {
        if (_utf8PeerTags is null)
        {
            return EmptyTags;
        }

        var list = new List<byte[]>(_utf8PeerTags.Count);
        foreach (var tag in _utf8PeerTags)
        {
            // create an array of the correct size
            var destination = new byte[tag.Count];
            tag.AsSpan().CopyTo(destination);
            list.Add(destination);
        }

        return list;
    }

    public void Dispose()
    {
        if (_utf8PeerTags is not null)
        {
            foreach (var keyValuePair in _utf8PeerTags)
            {
                ArrayPool<byte>.Shared.Return(keyValuePair.Array!);
            }

            _utf8PeerTags = null;
        }
    }
}
