// <copyright file="TestApplicationDescriptor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    /// <summary>
    /// Folder and assembly names for one <see cref="TestApplication"/>.
    /// </summary>
    public sealed class TestApplicationDescriptor
    {
        public TestApplicationDescriptor(TestApplication application, string folderName, string assemblyName)
        {
            Application = application;
            FolderName = folderName;
            AssemblyName = assemblyName;
        }

        public TestApplication Application { get; }

        public string FolderName { get; }

        public string AssemblyName { get; }
    }
}
