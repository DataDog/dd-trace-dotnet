// <copyright file="PortableExecutable.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Datadog.Trace.Tools.Runner.Checks.Windows;

// https://docs.microsoft.com/en-us/windows/win32/debug/pe-format
// https://github.com/dotnet/runtime/tree/main/src/libraries/System.Reflection.Metadata/src/System/Reflection/PortableExecutable
// https://github.com/jbevain/cecil/blob/master/Mono.Cecil.PE/ImageReader.cs

internal static class PortableExecutable
{
    internal const ushort DosSignature = 0x5A4D;  // "MZ"
    internal const int PESignatureOffsetLocation = 0x3C; // 60
    internal const uint PESignature = 0x00004550; // "PE\0\0"

    public static bool TryGetPEHeaders(string path, out PEHeaders? peHeaders)
    {
        try
        {
            if (File.Exists(path))
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

                if (Validate(stream))
                {
                    stream.Position = 0;
                    using var reader = new PEReader(stream);
                    peHeaders = reader.PEHeaders;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Utils.WriteWarning($"Error getting native library architecture: {ex.Message}");
        }

        peHeaders = null;
        return false;
    }

    public static bool Validate(Stream stream)
    {
        // Do some pre-validation not done by PEReader because PEReader supports COFF-only files.
        // Without this, it will try to parse our .so file on Linux and return garbage.
        // https://github.com/dotnet/runtime/blob/eb2fea80c6a4ff8117930b4003391d737f05c686/src/libraries/System.Reflection.Metadata/src/System/Reflection/PortableExecutable/PEHeaders.cs#L251-L252

        if (stream.Length < 384)
        {
            // File is too small, don't bother.
            // Value from https://github.com/katahiromz/smalldll
            return false;
        }

        using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: true);

        if (reader.ReadUInt16() != DosSignature)
        {
            return false;
        }

        stream.Position = PESignatureOffsetLocation;
        stream.Position = reader.ReadUInt32();

        if (reader.ReadUInt32() != PESignature)
        {
            return false;
        }

        return true;
    }
}
