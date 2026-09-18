// <copyright file="IOtelThreadContextRecordProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.OtelThreadContext;

/// <summary>
/// Provides the calling OS thread's native-owned OTEP 4947 Thread-Local Context Record.
/// </summary>
internal interface IOtelThreadContextRecordProvider
{
    /// <summary>
    /// Gets the calling thread's record.
    /// Returns <see cref="IntPtr.Zero"/> when no record can be provided.
    /// </summary>
    IntPtr GetRecord();
}
