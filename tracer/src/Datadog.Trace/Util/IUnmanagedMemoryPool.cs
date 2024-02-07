// <copyright file="IUnmanagedMemoryPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Util;

/// <summary>
/// Unmanaged memory pool
/// </summary>
internal interface IUnmanagedMemoryPool : IDisposable
{
    bool IsDisposed { get; }

    IntPtr Rent();

    void Return(IntPtr block);

    void Return(IList<IntPtr> blocks);
}
