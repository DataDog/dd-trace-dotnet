// <copyright file="CIVisibilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Ci;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    public class CIVisibilityTests
    {
        public static IEnumerable<object[]> GetParserData()
        {
            yield return
            [
                new SerializableDictionary
                {
                    { "config1", "value1" },
                    { "config2", "value2" },
                    { "config3", "value3" },
                    { "test.configuration.key01", "value1" },
                    { "test.configuration.key02", "value2" },
                    { "test.configuration.key03", "value3" },
                    { "test.configuration.", "value3" },
                },
                new SerializableDictionary
                {
                    { "key01", "value1" },
                    { "key02", "value2" },
                    { "key03", "value3" },
                }
            ];

            yield return
            [
                new SerializableDictionary
                {
                    { "config1", "value1" },
                    { "test.configuration.key01", "value1" },
                    { "test.configurations.key01", "value1" },
                },
                new SerializableDictionary
                {
                    { "key01", "value1" },
                }
            ];

            yield return
            [
                new SerializableDictionary
                {
                    { "test.configuration.", "invalid test configuration" },
                },
                null
            ];

            yield return
            [
                new SerializableDictionary
                {
                    { "config1", "value1" },
                    { "config2", "value2" },
                    { "config3", "value3" },
                },
                null
            ];

            yield return
            [
                null,
                null
            ];
        }

        [SkippableTheory]
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

        [SkippableTheory]
        [MemberData(nameof(GetParserData))]
        public void CustomTestConfigurationParser(SerializableDictionary tags, SerializableDictionary expected)
        {
            Assert.Equal(expected, IntelligentTestRunnerClient.GetCustomTestsConfigurations(tags?.ToDictionary()));
        }
    }
}
