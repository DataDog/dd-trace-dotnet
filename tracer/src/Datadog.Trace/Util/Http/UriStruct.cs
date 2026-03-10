// <copyright file="UriStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Util.Http;

/// <summary>
/// A duck type for <see cref="System.Uri"/> to check the DisablePathAndQueryCanonicalizationFlag
/// See https://github.com/dotnet/runtime/blob/v10.0.3/src/libraries/System.Private.Uri/src/System/Uri.cs#L125
/// </summary>
[DuckCopy]
internal struct UriStruct
{
    // The flag changed in .NET 10
    private static readonly ulong DisablePathAndQueryCanonicalizationFlag
        = FrameworkDescription.Instance.RuntimeVersion.Major >= 10
              ? 1UL << 55
              : 0x200000000000;

    [DuckField(Name = "_flags")]
    public ulong Flags;

    public readonly bool IsDangerousDisablePathAndQueryCanonicalization() => (Flags & DisablePathAndQueryCanonicalizationFlag) != 0;
}
#endif
