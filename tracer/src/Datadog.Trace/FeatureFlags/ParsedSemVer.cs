// <copyright file="ParsedSemVer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.FeatureFlags;

/// <summary>
/// The language-neutral representation of the Rust/Eppo SemVer subset used by FFE.
/// </summary>
internal readonly struct ParsedSemVer : IEquatable<ParsedSemVer>
{
    public ParsedSemVer(ulong major, ulong minor, ulong patch, string prerelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
    }

    public ulong Major { get; }

    public ulong Minor { get; }

    public ulong Patch { get; }

    public string Prerelease { get; }

    public bool Equals(ParsedSemVer other)
        => Major == other.Major && Minor == other.Minor && Patch == other.Patch && Prerelease == other.Prerelease;

    public override bool Equals(object? obj) => obj is ParsedSemVer other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, Prerelease);
}
