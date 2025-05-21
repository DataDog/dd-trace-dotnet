// <copyright file="TracerMemfdHandle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog.ServiceDiscovery;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1401
#pragma warning disable SA1600
[StructLayout(LayoutKind.Sequential)]
// ReSharper disable once ClassNeverInstantiated.Global
public struct TracerMemfdHandle
{
    public int Fd;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore SA1401
}
#pragma warning restore SA1600
