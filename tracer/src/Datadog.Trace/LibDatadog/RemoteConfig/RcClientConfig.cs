// <copyright file="RcClientConfig.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.InteropServices;

// Interop mapping of libdatadog-dotnet's remote config FFI (datadog-dotnet-ffi/src/remote_config.rs).
// Do not change the layout unless the native side changes.
namespace Datadog.Trace.LibDatadog.RemoteConfig;

[StructLayout(LayoutKind.Sequential)]
internal struct RcClientConfig
{
    public CharSlice Language;
    public CharSlice TracerVersion;
    public CharSlice RuntimeId;
    public CharSlice ClientId;
    public RcTarget Target;
}
