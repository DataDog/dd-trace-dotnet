// <copyright file="ProfilerLogFile.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;

namespace Datadog.Profiler.IntegrationTests.Helpers.Logs
{
    internal sealed class ProfilerLogFile
    {
        private ProfilerLogFile(string path, IReadOnlyList<string> lines)
        {
            Path = path;
            FileName = System.IO.Path.GetFileName(path);
            Lines = lines;
        }

        public string Path { get; }

        public string FileName { get; }

        public IReadOnlyList<string> Lines { get; }

        public static ProfilerLogFile Read(string path)
        {
            // Smoke tests read xunit.txt while the named-pipe mock agent can still append to it.
            // File.ReadAllLines uses FileShare.Read, which throws on Windows if that append overlaps the read.
            var lines = new List<string>();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            return new ProfilerLogFile(path, lines);
        }
    }
}
