// <copyright file="CaptureLimitInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.Expressions;

internal class CaptureLimitInfo(
    int? maxReferenceDepth,
    int? maxCollectionSize,
    int? maxLength,
    int? maxFieldCount,
    int? timeoutInMilliSeconds)
{
    internal static CaptureLimitInfo Default { get; } = new(
        DebuggerSettings.DefaultMaxDepthToSerialize,
        DebuggerSettings.DefaultMaxNumberOfItemsInCollectionToCopy,
        DebuggerSettings.DefaultMaxStringLength,
        DebuggerSettings.DefaultMaxNumberOfFieldsToCopy,
        DebuggerSettings.DefaultMaxSerializationTimeInMilliseconds);

    internal int MaxReferenceDepth { get; } = maxReferenceDepth ?? DebuggerSettings.DefaultMaxDepthToSerialize;

    internal int MaxCollectionSize { get; } = maxCollectionSize ?? DebuggerSettings.DefaultMaxNumberOfItemsInCollectionToCopy;

    internal int MaxLength { get; } = maxLength ?? DebuggerSettings.DefaultMaxStringLength;

    internal int MaxFieldCount { get; } = maxFieldCount ?? DebuggerSettings.DefaultMaxNumberOfFieldsToCopy;

    internal int TimeoutInMs { get; } = timeoutInMilliSeconds ?? DebuggerSettings.DefaultMaxSerializationTimeInMilliseconds;
}
