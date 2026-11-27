// <copyright file="NativeReactorBinding.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Datadog.Trace.RemoteConfigurationManagement.Shared;

internal sealed unsafe class NativeReactorBinding : SafeHandleZeroOrMinusOneIsInvalid, IReactorBinding
{
    private readonly nint _library;
    private readonly delegate* unmanaged[Cdecl]<nint, void> _drop;
    private readonly delegate* unmanaged[Cdecl]<nint, int> _error;
    private readonly delegate* unmanaged[Cdecl]<nint, int, byte*> _read;
    private readonly delegate* unmanaged[Cdecl]<nint, int, byte*> _write;

    internal NativeReactorBinding(string path)
        : base(ownsHandle: true)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("The native reactor library path must be absolute", nameof(path));
        }

        _library = NativeLibrary.Load(path);
        try
        {
            var version = (delegate* unmanaged[Cdecl]<int>)Load("rc_version");
            if (version() != 1)
            {
                throw new InvalidOperationException("Expected native ABI v1");
            }

            _drop = (delegate* unmanaged[Cdecl]<nint, void>)Load("rc_reactor_drop");
            _error = (delegate* unmanaged[Cdecl]<nint, int>)Load("rc_error");
            _read = (delegate* unmanaged[Cdecl]<nint, int, byte*>)Load("rc_buffer_bytes");
            _write = (delegate* unmanaged[Cdecl]<nint, int, byte*>)Load("rc_input_bytes");
            var create = (delegate* unmanaged[Cdecl]<nint>)Load("rc_reactor_new");
            var reactor = create();
            if (reactor == 0)
            {
                throw new InvalidOperationException("Could not create native reactor");
            }

            SetHandle(reactor);
        }
        catch
        {
            NativeLibrary.Free(_library);
            throw;
        }
    }

    public int GetError() => _error(GetContext());

    public Func<int, int> BindUnary(string export)
    {
        var function = (delegate* unmanaged[Cdecl]<nint, int, int>)Load(export);
        return argument => function(GetContext(), argument);
    }

    public Func<int, int, int> BindBinary(string export)
    {
        var function = (delegate* unmanaged[Cdecl]<nint, int, int, int>)Load(export);
        return (first, second) => function(GetContext(), first, second);
    }

    public Func<int, int, int, int> BindTernary(string export)
    {
        var function = (delegate* unmanaged[Cdecl]<nint, int, int, int, int>)Load(export);
        return (first, second, third) => function(GetContext(), first, second, third);
    }

    public Func<int, int, int, int, int> BindQuaternary(string export)
    {
        var function = (delegate* unmanaged[Cdecl]<nint, int, int, int, int, int>)Load(export);
        return (first, second, third, fourth) => function(GetContext(), first, second, third, fourth);
    }

    public ReadOnlySpan<byte> BorrowRead(int buffer, int length) => new(RequirePointer(_read(GetContext(), buffer)), length);

    public Span<byte> BorrowWrite(int buffer, int length) => new(RequirePointer(_write(GetContext(), buffer)), length);

    protected override bool ReleaseHandle()
    {
        _drop(handle);
        NativeLibrary.Free(_library);
        return true;
    }

    private static byte* RequirePointer(byte* pointer) => pointer != null ? pointer : throw new InvalidOperationException("Invalid native reactor buffer");

    private nint Load(string export) => NativeLibrary.GetExport(_library, "ddog_" + export);

    private nint GetContext()
    {
        if (IsClosed)
        {
            throw new ObjectDisposedException(nameof(NativeReactorBinding));
        }

        return handle;
    }
}
