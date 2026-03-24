// <copyright file="ServiceRemappingHash.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Util;

namespace Datadog.Trace;

/// <summary>
/// This class is a container for the "base hash", a hash of process tags and container tags.
/// Used by DBM to retrieve all the tag values from the spans, from just a single parameter (this hash),
/// Used by DSM in the pathway to identify different sources that could have been service-remapped
/// </summary>
internal sealed class ServiceRemappingHash
{
    private readonly ProcessTags? _processTags;

    public ServiceRemappingHash(ProcessTags? processTags)
    {
        _processTags = processTags;
        if (processTags != null)
        {
            // containers tags hash is always null at creation, because we discover it later (if any)
            B64Value = Compute(processTags.SerializedTags, containerTagsHash: null);
        }
    }

    /// <summary>
    /// Gets the container tags hash received from the agent, used by DBM/DSM
    /// This is set when we receive a value for it in an http response from the agent
    /// </summary>
    public string? ContainerTagsHash
    {
        get;
        private set;
    }

    /// <summary>
    /// Gets the base64 representation of the hash
    /// </summary>
    public string? B64Value
    {
        get;
        private set;
    }

    public void UpdateContainerTagsHash(string containerTagsHash)
    {
        ContainerTagsHash = containerTagsHash;

        if (_processTags != null)
        {
            B64Value = Compute(_processTags.SerializedTags, containerTagsHash);
        }
    }

    private static string Compute(string processTags, string? containerTagsHash)
    {
        var hash = FnvHash64.GenerateHash(processTags, FnvHash64.Version.V1);
        if (containerTagsHash != null)
        {
            hash = FnvHash64.GenerateHash(containerTagsHash, FnvHash64.Version.V1, hash);
        }

        var b64 = Convert.ToBase64String(BitConverter.GetBytes(hash));
        return b64
            .TrimEnd('=') // remove padding
            // use url-safe characters
            .Replace(oldChar: '+', newChar: '-')
            .Replace(oldChar: '/', newChar: '_');
    }
}
