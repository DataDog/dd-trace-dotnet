// <copyright file="ProfilerLogMatch.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace Datadog.Profiler.IntegrationTests.Helpers.Logs
{
    internal sealed class ProfilerLogMatch
    {
        public ProfilerLogMatch(string path, int lineNumber, string line)
        {
            Path = path;
            LineNumber = lineNumber;
            Line = line;
        }

        public string Path { get; }

        public int LineNumber { get; }

        public string Line { get; }
    }
}
