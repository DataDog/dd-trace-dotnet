// <copyright file="OtelThreadContextRecordProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.OtelThreadContext;

/// <summary>
/// Get access to the calling OS thread's native-owned OTEP 4947 Thread-Local Context Record.
/// See docs/OTelContextPropagation.md.
/// </summary>
internal sealed class OtelThreadContextRecordProvider : IOtelThreadContextRecordProvider
{
    public static readonly OtelThreadContextRecordProvider Instance = new();

    private OtelThreadContextRecordProvider()
    {
    }

    public IntPtr GetRecord() => NativeMethods.GetOrCreateOtelThreadContextRecord();
}
