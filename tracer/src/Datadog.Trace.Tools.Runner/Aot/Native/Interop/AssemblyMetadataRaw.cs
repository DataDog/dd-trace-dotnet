// <copyright file="AssemblyMetadataRaw.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

/// <summary>
/// Mutable mirror of ASSEMBLYMETADATA (cor.h) used to fill the caller's structure.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AssemblyMetadataRaw
{
    public ushort MajorVersion;
    public ushort MinorVersion;
    public ushort BuildNumber;
    public ushort RevisionNumber;
    public char* Locale;
    public uint LocaleLength;
    public int* Processor;
    public uint ProcessorCount;
    public void* OperatingSystems;
    public uint OperatingSystemCount;
}
#endif
