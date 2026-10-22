// <copyright file="RemoteConfigurationContent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;

namespace Datadog.Trace.RemoteConfigurationManagement;

/// <summary>
/// The (UTF-8 JSON) contents of a remote configuration. Received contents live in native memory
/// and are read in place, without copying them to the managed heap.
/// </summary>
internal sealed class RemoteConfigurationContent
{
    private readonly byte[]? _bytes;
    private readonly RemoteConfigurationContentHandle? _handle;

    public RemoteConfigurationContent(byte[] bytes)
    {
        _bytes = bytes;
    }

    public RemoteConfigurationContent(RemoteConfigurationContentHandle handle)
    {
        _handle = handle;
    }

    public long Length => _handle is null ? _bytes!.Length : (long)_handle.ByteLength;

    public static implicit operator RemoteConfigurationContent(byte[] bytes) => new(bytes);

    public Stream OpenStream()
        => _handle is null
               ? new MemoryStream(_bytes!, writable: false)
               // this constructor keeps the handle alive (and referenced) while reading
               : new UnmanagedMemoryStream(_handle, 0, (long)_handle.ByteLength);
}
