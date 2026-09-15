// <copyright file="NullOtelThreadContextPublisher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.OtelThreadContext;

/// <summary>
/// Used when thread context publication is disabled, unsupported on the current platform, or has failed.
/// </summary>
internal sealed class NullOtelThreadContextPublisher : IOtelThreadContextPublisher
{
    public static readonly NullOtelThreadContextPublisher Instance = new();

    private NullOtelThreadContextPublisher()
    {
    }

    public bool IsEnabled => false;

    public void Set(Span span)
    {
    }

    public void Reset()
    {
    }
}
