// <copyright file="BaseHash.cs" company="Datadog">
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
internal static class BaseHash
{
    /// <summary>
    /// Gets the base64 representation of the hash
    /// </summary>
    public static string B64Value
    {
        get => Volatile.Read(ref field);
        private set => Volatile.Write(ref field, value);
    }

        = Recompute(ProcessTags.SerializedTags, ContainerMetadata.Instance.ContainerTagsHash);

    public static string Recompute(string processTags, string? containerTagsHash)
    {
        var hash = FnvHash64.GenerateHash(processTags, FnvHash64.Version.V1);
        if (containerTagsHash != null)
        {
            hash = FnvHash64.GenerateHash(containerTagsHash, FnvHash64.Version.V1, hash);
        }

        return B64Value = Convert.ToBase64String(BitConverter.GetBytes(hash));
    }
}
