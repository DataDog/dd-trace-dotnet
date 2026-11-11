// <copyright file="ProfilerLogSet.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;

namespace Datadog.Profiler.IntegrationTests.Helpers.Logs
{
    internal sealed class ProfilerLogSet
    {
        public const string NativeProfilerPattern = "DD-DotNet-Profiler-Native*.log";

        // Smoke tests treat these two patterns as the managed log groups.
        public const string ManagedProfilerPattern = "DD-DotNet-Profiler-Managed*.*";

        public const string ManagedLoaderPattern = "DD-DotNet-Common-ManagedLoader*.*";

        public const string XUnitFileName = "xunit.txt";

        private readonly IReadOnlyList<ProfilerLogFile> _files;

        private ProfilerLogSet(string directory, IReadOnlyList<ProfilerLogFile> files)
        {
            Directory = directory;
            _files = files;
        }

        public string Directory { get; }

        public ProfilerLogGroup NativeProfiler => Matching(NativeProfilerPattern);

        public ProfilerLogGroup ManagedLoader => Matching(ManagedLoaderPattern);

        public ProfilerLogGroup XUnit => Matching(XUnitFileName);

        public static ProfilerLogSet Read(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("A log directory is required.", nameof(directory));
            }

            var files = new List<ProfilerLogFile>();
            if (System.IO.Directory.Exists(directory))
            {
                foreach (var path in System.IO.Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    files.Add(ProfilerLogFile.Read(path));
                }
            }

            return new ProfilerLogSet(directory, files);
        }

        public ProfilerLogGroup Matching(string fileNameOrGlob)
        {
            return ProfilerLogGroup.FromFiles(Directory, fileNameOrGlob, _files);
        }
    }
}
