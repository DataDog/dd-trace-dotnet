// <copyright file="RemoteConfigurationContentHandle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.LibDatadog;

namespace Datadog.Trace.RemoteConfigurationManagement;

/// <summary>
/// Owns the contents of a remote configuration, as received by libdatadog: the bytes are read in place,
/// and released once the handle is disposed or finalized.
/// </summary>
internal sealed class RemoteConfigurationContentHandle : SafeBuffer
{
    private readonly IntPtr _owner;
    private readonly long _length;

    public RemoteConfigurationContentHandle(IntPtr data, nuint length, IntPtr owner)
        : base(ownsHandle: true)
    {
        _owner = owner;
        _length = (long)length;
        SetHandle(data);
        Initialize(length);
        GC.AddMemoryPressure(_length);
    }

    protected override bool ReleaseHandle()
    {
        NativeInterop.RemoteConfig.ContentDrop(_owner);
        GC.RemoveMemoryPressure(_length);
        return true;
    }
}
