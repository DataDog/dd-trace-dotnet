// <copyright file="CIVisibilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    public class CIVisibilityTests
    {
        private static readonly ITestOptimizationTracerManagement TracerManagement = new TestOptimizationTracerManagement(TestOptimizationSettings.FromDefaultSources());

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
            Assert.Equal(serviceName, TracerManagement.GetServiceNameFromRepository(repository));
        }

        [SkippableTheory]
        [MemberData(nameof(GetParserData))]
        public void CustomTestConfigurationParser(SerializableDictionary tags, SerializableDictionary expected)
        {
            Assert.Equal(expected, TestOptimizationClient.GetCustomTestsConfigurations(tags?.ToDictionary()));
        }

        [SkippableFact]
        public void GitFetchCommitInfo()
        {
            var workingDirectory = Environment.CurrentDirectory;
            var localCommits = GitCommandHelper.GetLocalCommits(workingDirectory);
            if (localCommits.Length == 0)
            {
                Skip.If(true, "No local commits found. Skipping test.");
            }
            else
            {
                localCommits.Should().NotBeNullOrEmpty();
                localCommits.Should().NotContainNulls();
                foreach (var localCommit in localCommits)
                {
                    if (GitCommandHelper.FetchCommitData(workingDirectory, localCommit) is { } commitData)
                    {
                        commitData.CommitSha.Should().NotBeNullOrWhiteSpace();
                        commitData.AuthorName.Should().NotBeNullOrWhiteSpace();
                        commitData.AuthorEmail.Should().NotBeNullOrWhiteSpace();
                        commitData.AuthorDate.Should().BeAfter(DateTimeOffset.MinValue).And.BeBefore(DateTimeOffset.Now);
                        commitData.CommitterName.Should().NotBeNullOrWhiteSpace();
                        commitData.CommitterEmail.Should().NotBeNullOrWhiteSpace();
                        commitData.CommitterDate.Should().BeAfter(DateTimeOffset.MinValue).And.BeBefore(DateTimeOffset.Now);
                        commitData.CommitMessage.Should().NotBeNullOrWhiteSpace();
                    }
                    else
                    {
                        throw new InvalidOperationException($"Failed to fetch commit data for {localCommit} in {workingDirectory}");
                    }
                }
            }
        }

        [SkippableFact]
        public void SetStringOrArray()
        {
            /*
             * SetStringOrArray is used to set a string or an array of strings in the suite tags.
             * This is useful when we have tests from multiple files (partial classes) and we want to
             * report all the source files in the suite tags.
             */

            var testTags = new  TestSpanTags();
            var suiteTags = new TestSuiteSpanTags(new TestModuleSpanTags(new TestSessionSpanTags()), "SuiteName");

            testTags.SourceFile = "TestFile.cs";

            Test.SetStringOrArray(
                testTags,
                suiteTags,
                tTags => tTags.SourceFile,
                sTags => sTags.SourceFile,
                (sTags, value) => sTags.SourceFile = value);

            // Verify that the suite tags are updated with the test tags value
            suiteTags.SourceFile.Should().Be(testTags.SourceFile);

            Test.SetStringOrArray(
                testTags,
                suiteTags,
                tTags => tTags.SourceFile,
                sTags => sTags.SourceFile,
                (sTags, value) => sTags.SourceFile = value);

            // Verify that there's no change in the suite tags (not duplicate assignment)
            suiteTags.SourceFile.Should().Be(testTags.SourceFile);

            testTags.SourceFile = "TestFile2.cs";
            Test.SetStringOrArray(
                testTags,
                suiteTags,
                tTags => tTags.SourceFile,
                sTags => sTags.SourceFile,
                (sTags, value) => sTags.SourceFile = value);

            // Verify that the suite tags are updated containing both values
            suiteTags.SourceFile.Should().Be("""["TestFile.cs","TestFile2.cs"]""");
        }

        [SkippableFact]
        public void SetCodeOwnersOnSuiteTags()
        {
            var testTags = new  TestSpanTags();
            var suiteTags = new TestSuiteSpanTags(new TestModuleSpanTags(new TestSessionSpanTags()), "SuiteName");

            Test.SetCodeOwnersOnTags(testTags, suiteTags, ["owner1", "owner2"]);

            testTags.CodeOwners.Should().Be("""["owner1","owner2"]""");
            // Verify that the suite tags are updated with the test tags value
            suiteTags.CodeOwners.Should().Be(testTags.CodeOwners);

            Test.SetCodeOwnersOnTags(testTags, suiteTags, ["owner1", "owner2"]);

            testTags.CodeOwners.Should().Be("""["owner1","owner2"]""");
            // Verify that the suite tags are not changed (not duplicate assignment)
            suiteTags.CodeOwners.Should().Be(testTags.CodeOwners);

            Test.SetCodeOwnersOnTags(testTags, suiteTags, ["owner3", "owner4"]);
            testTags.CodeOwners.Should().Be("""["owner3","owner4"]""");

            // Verify that the new test tags are appended to the suite tags
            suiteTags.CodeOwners.Should().Be("""["owner1","owner2","owner3","owner4"]""");
        }
    }
}
