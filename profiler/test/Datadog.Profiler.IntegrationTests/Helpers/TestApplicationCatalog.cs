// <copyright file="TestApplicationCatalog.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal static class TestApplicationCatalog
    {
        private static readonly Dictionary<TestApplication, TestApplicationDescriptor> ByApplication = new()
        {
            [TestApplication.Computer01] = Describe(TestApplication.Computer01, "Samples.Computer01", "Samples.Computer01"),
            [TestApplication.BuggyBits] = Describe(TestApplication.BuggyBits, "Samples.BuggyBits", "Samples.BuggyBits"),
            [TestApplication.ExceptionGenerator] = Describe(TestApplication.ExceptionGenerator, "Samples.ExceptionGenerator", "Samples.ExceptionGenerator"),
            [TestApplication.ParallelCountSites] = Describe(TestApplication.ParallelCountSites, "Samples.ParallelCountSites", "Samples.ParallelCountSites"),
            [TestApplication.WebsiteAspNetCore01] = Describe(TestApplication.WebsiteAspNetCore01, "Samples.Website-AspNetCore01", "Samples.Website-AspNetCore01"),
            [TestApplication.WaitHandles] = Describe(TestApplication.WaitHandles, "Samples.WaitHandles", "Samples.WaitHandles"),
            [TestApplication.HttpRequest] = Describe(TestApplication.HttpRequest, "Samples.HttpRequest", "Samples.HttpRequest"),
            [TestApplication.FileAccess] = Describe(TestApplication.FileAccess, "Samples.FileAccess", "Samples.FileAccess"),
            [TestApplication.WebsiteAspNet] = Describe(TestApplication.WebsiteAspNet, "Website-AspNet", "Datadog.Demos.Website-AspNet"),
        };

        private static readonly Dictionary<string, TestApplicationDescriptor> ByFolder = CreateFolderIndex();

        public static IReadOnlyCollection<TestApplicationDescriptor> All => ByApplication.Values;

        public static bool TryGet(TestApplication application, out TestApplicationDescriptor descriptor)
        {
            return ByApplication.TryGetValue(application, out descriptor);
        }

        public static TestApplicationDescriptor Get(TestApplication application)
        {
            if (!TryGet(application, out var descriptor))
            {
                throw new KeyNotFoundException($"Unknown test application '{application}'.");
            }

            return descriptor;
        }

        public static bool TryGetByFolder(string folderName, out TestApplicationDescriptor descriptor)
        {
            if (folderName == null)
            {
                descriptor = null;
                return false;
            }

            return ByFolder.TryGetValue(folderName, out descriptor);
        }

        private static Dictionary<string, TestApplicationDescriptor> CreateFolderIndex()
        {
            var index = new Dictionary<string, TestApplicationDescriptor>();
            foreach (var descriptor in ByApplication.Values)
            {
                index.Add(descriptor.FolderName, descriptor);
            }

            return index;
        }

        private static TestApplicationDescriptor Describe(TestApplication application, string folderName, string assemblyName)
        {
            return new TestApplicationDescriptor(application, folderName, assemblyName);
        }
    }
}
