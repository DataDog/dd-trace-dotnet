// <copyright file="CancellationTokenHandle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// A libdatadog cancellation token, to abort a blocking call from another thread: pass the token to
/// the call, and cancel a <see cref="Clone"/> of it, as a token must not be used by two threads at once.
/// </summary>
internal sealed class CancellationTokenHandle : SafeHandle
{
    private CancellationTokenHandle(IntPtr token)
        : base(IntPtr.Zero, ownsHandle: true)
    {
        // The functions take a pointer to the native handle struct, which holds the token pointer
        var handleStruct = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(handleStruct, token);
        SetHandle(handleStruct);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public static CancellationTokenHandle Create() => new(NativeInterop.Common.CancellationTokenNew());

    /// <summary>
    /// Returns a token which is cancelled together with this one, with an independent lifetime.
    /// </summary>
    public CancellationTokenHandle Clone() => new(NativeInterop.Common.CancellationTokenClone(this));

    public void Cancel() => NativeInterop.Common.CancellationTokenCancel(this);

    protected override bool ReleaseHandle()
    {
        NativeInterop.Common.CancellationTokenDrop(handle);
        Marshal.FreeHGlobal(handle);
        return true;
    }
}
