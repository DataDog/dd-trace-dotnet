// <copyright file="CIVisibilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    public class CIVisibilityTests
    {
        [Theory]
        [InlineData("https://github.com/DataDog/dd-trace-dotnet.git", "dd-trace-dotnet")]
        [InlineData("git@github.com:DataDog/dd-trace-dotnet.git", "dd-trace-dotnet")]
        [InlineData("ssh://user@host.xz:port/path/to/repo.git", "repo")]
        [InlineData("ssh://user@host.xz/~/path/to/repo.git", "repo")]
        [InlineData("rsync://host.xz/path/to/repo.git", "repo")]
        [InlineData("git://host.xz/~user/path/to/repo.git", "repo")]
        [InlineData("file://~/path/to/repo.git", "repo")]
        [InlineData("/path/to/repo.git/", "repo")]
        [InlineData("user@host.xz:~user/path/to/repo.git/", "repo")]
        [InlineData("ssh://login@server.com:12345/absolute/path/to/repository", "repository")]
        [InlineData("ssh://login@server.com:12345/repository.git", "repository")]
        [InlineData("repo.git", "repo")]
        [InlineData("./repo", "repo")]
        [InlineData(@".\repo", "repo")]
        [InlineData(@"\\wsl$\path\to\repo", "repo")]
        [InlineData(@"C:\path\to\repo", "repo")]
        [InlineData("", "")]
        [InlineData("%^&*", "")]
        public void GetServiceNameFromRepository(string repository, string serviceName)
        {
            Assert.Equal(serviceName, Ci.CIVisibility.GetServiceNameFromRepository(repository));
        }
    }
}
