// <copyright file="ProfileReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using K4os.Compression.LZ4.Streams;
using Perftools.Profiles;
using ZstdSharp;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles
{
    internal static class ProfileReader
    {
        // Same bytes and order as SamplesHelper.GetStream.
        private static readonly byte[] Lz4MagicNumber = BitConverter.GetBytes(0x184D2204);
        private static readonly byte[] ZstdMagicNumber = BitConverter.GetBytes(0xFD2FB528);

        public static ProfileOutput Read(string directory)
        {
            if (directory is null)
            {
                throw new ArgumentNullException(nameof(directory));
            }

            var profiles = new List<PprofProfile>();
            foreach (var file in Directory.EnumerateFiles(directory, "*.pprof", SearchOption.AllDirectories))
            {
                profiles.Add(ReadFile(file));
            }

            return new ProfileOutput(profiles);
        }

        public static PprofProfile ReadFile(string path)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var timestampUtc = File.GetLastWriteTimeUtc(path);
            using (var stream = Open(path))
            {
                var profile = Profile.Parser.ParseFrom(stream);
                return PprofProfile.Create(path, timestampUtc, profile);
            }
        }

        private static Stream Open(string path)
        {
            var stream = File.OpenRead(path);
            var buffer = new byte[4];
            stream.ReadExactly(buffer.AsSpan());
            stream.Position = 0;
            if (StartsWith(buffer, Lz4MagicNumber))
            {
                return LZ4Stream.Decode(stream);
            }
            else if (StartsWith(buffer, ZstdMagicNumber))
            {
                return new DecompressionStream(stream);
            }
            else
            {
                return stream;
            }
        }

        private static bool StartsWith(byte[] buffer, byte[] magic)
        {
            for (var i = 0; i < magic.Length; i++)
            {
                if (buffer[i] != magic[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
