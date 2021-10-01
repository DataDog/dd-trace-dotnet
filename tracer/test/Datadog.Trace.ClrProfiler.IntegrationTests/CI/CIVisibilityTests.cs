// <copyright file="CIVisibilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    public class CIVisibilityTests
    {
        [TestCase("https://github.com/DataDog/dd-trace-dotnet.git", "dd-trace-dotnet")]
        [TestCase("git@github.com:DataDog/dd-trace-dotnet.git", "dd-trace-dotnet")]
        [TestCase("ssh://user@host.xz:port/path/to/repo.git", "repo")]
        [TestCase("ssh://user@host.xz/~/path/to/repo.git", "repo")]
        [TestCase("rsync://host.xz/path/to/repo.git", "repo")]
        [TestCase("git://host.xz/~user/path/to/repo.git", "repo")]
        [TestCase("file://~/path/to/repo.git", "repo")]
        [TestCase("/path/to/repo.git/", "repo")]
        [TestCase("user@host.xz:~user/path/to/repo.git/", "repo")]
        [TestCase("ssh://login@server.com:12345/absolute/path/to/repository", "repository")]
        [TestCase("ssh://login@server.com:12345/repository.git", "repository")]
        [TestCase("repo.git", "repo")]
        [TestCase("./repo", "repo")]
        [TestCase(@".\repo", "repo")]
        [TestCase(@"\\wsl$\path\to\repo", "repo")]
        [TestCase(@"C:\path\to\repo", "repo")]
        [TestCase("", "")]
        [TestCase("%^&*", "")]
        public void GetServiceNameFromRepository(string repository, string serviceName)
        {
            Assert.AreEqual(serviceName, Ci.CIVisibility.GetServiceNameFromRepository(repository));
        }
    }
}
