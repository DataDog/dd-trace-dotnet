// <copyright file="TempPprofDirectory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles.Tests
{
    internal sealed class TempPprofDirectory : IDisposable
    {
        public TempPprofDirectory()
        {
            DirectoryPath = Directory.CreateTempSubdirectory("pprof-read-model-").FullName;
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            Directory.Delete(DirectoryPath, true);
        }
    }
}
