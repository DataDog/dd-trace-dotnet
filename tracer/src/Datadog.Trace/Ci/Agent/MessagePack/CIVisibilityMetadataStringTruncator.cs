// <copyright file="CIVisibilityMetadataStringTruncator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Agent.MessagePack;

internal static class CIVisibilityMetadataStringTruncator
{
    public const int MaxMetaStringLength = 5_000;

    public static string Truncate(string value)
        => value.Length <= MaxMetaStringLength ? value : value.Substring(0, MaxMetaStringLength);
}
