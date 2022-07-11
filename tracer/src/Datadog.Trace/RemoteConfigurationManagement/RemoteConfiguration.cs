// <copyright file="RemoteConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal class RemoteConfiguration : IEquatable<RemoteConfiguration>
{
    public RemoteConfiguration(
        RemoteConfigurationPath path,
        byte[] contents,
        int length,
        Dictionary<string, string> hashes,
        int version)
    {
        Path = path;
        Contents = contents;
        Length = length;
        Hashes = hashes;
        Version = version;
    }

    public RemoteConfigurationPath Path { get; }

    public byte[] Contents { get; }

    public int Length { get; }

    public Dictionary<string, string> Hashes { get; }

    public int Version { get; }

    public override bool Equals(object o)
    {
        if (ReferenceEquals(null, o))
        {
            return false;
        }

        if (ReferenceEquals(this, o))
        {
            return true;
        }

        if (o.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((RemoteConfiguration)o);
    }

    public bool Equals(RemoteConfiguration other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return
            Path == other.Path &&
            Version == other.Version &&
            Length == other.Length &&
            ByteArrayCompare(Contents, other.Contents) &&
            DictionaryContentEquals(Hashes, other.Hashes);
    }

    // because https://stackoverflow.com/a/48599119
    private static bool ByteArrayCompare(byte[] a1, byte[] a2)
    {
        return a1.SequenceEqual(a2);
    }

    public static bool DictionaryContentEquals(Dictionary<string, string> dictionary, Dictionary<string, string> otherDictionary)
    {
        return (otherDictionary ?? new Dictionary<string, string>())
            .SequenceEqual(dictionary ?? new Dictionary<string, string>());
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Path, Contents, Length, Hashes, Version);
    }
}
