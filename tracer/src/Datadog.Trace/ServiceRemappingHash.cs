// <copyright file="ServiceRemappingHash.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using System.Threading;
using Datadog.Trace.Util;

namespace Datadog.Trace;

/// <summary>
/// This class is a container for the "base hash", a hash of process tags and container tags.
/// Used by DBM to retrieve all the tag values from the spans, from just a single parameter (this hash),
/// Used by DSM in the pathway to identify different sources that could have been service-remapped
/// </summary>
internal sealed class ServiceRemappingHash
{
    private readonly string? _serializedProcessTags;

    public ServiceRemappingHash(string? serializedProcessTags)
    {
        _serializedProcessTags = serializedProcessTags;
        if (serializedProcessTags != null)
        {
            // containers tags hash is always null at creation, because we discover it later (if any)
            Base64Value = Compute(serializedProcessTags, containerTagsHash: null);
        }
    }

    /// <summary>
    /// Gets the container tags hash received from the agent, used by DBM/DSM
    /// This is set when we receive a value for it in an http response from the agent
    /// </summary>
    public string? ContainerTagsHash
    {
        get => Volatile.Read(ref field);
        private set => Volatile.Write(ref field, value);
    }

    /// <summary>
    /// Gets the base64 representation of the hash
    /// </summary>
    public string? Base64Value
    {
        get => Volatile.Read(ref field);
        private set => Volatile.Write(ref field, value);
    }

    public void UpdateContainerTagsHash(string containerTagsHash)
    {
        ContainerTagsHash = containerTagsHash;

        if (_serializedProcessTags != null)
        {
            Base64Value = Compute(_serializedProcessTags, containerTagsHash);
        }
    }

    private static string Compute(string processTags, string? containerTagsHash)
    {
        var hash = FnvHash64.GenerateHash(processTags, FnvHash64.Version.V1);
        if (containerTagsHash != null)
        {
            hash = FnvHash64.GenerateHash(containerTagsHash, FnvHash64.Version.V1, hash);
        }

#if NETCOREAPP3_1_OR_GREATER
        Span<byte> buf = stackalloc byte[12];
#else
        // can't stackalloc into the vendored Span<T>
        var buf = new byte[12];
#endif

        BinaryPrimitives.WriteUInt64LittleEndian(buf, hash); // write 8 bytes into a 12-byte buffer
        Base64.EncodeToUtf8InPlace(buf, dataLength: 8, out var bytesWritten);

        // no padding
        while (bytesWritten > 0 && buf[bytesWritten - 1] == (byte)'=')
        {
            bytesWritten--;
        }

        // use url-safe characters (for the SQL comment)
        for (var i = 0; i < bytesWritten; i++)
        {
            if (buf[i] == (byte)'+')
            {
                buf[i] = (byte)'-';
            }
            else if (buf[i] == (byte)'/')
            {
                buf[i] = (byte)'_';
            }
        }

#if NETCOREAPP3_1_OR_GREATER
        return Encoding.ASCII.GetString(buf[..bytesWritten]);
#else
        // can't use Range
        return Encoding.ASCII.GetString(buf, index: 0, bytesWritten);
#endif
    }
}
