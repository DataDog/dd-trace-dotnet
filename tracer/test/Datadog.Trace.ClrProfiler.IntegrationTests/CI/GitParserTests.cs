// <copyright file="GitParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Ci.CiEnvironment;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    public class GitParserTests
    {
        public static IEnumerable<object[]> GetData()
        {
            var dataFolder = DataHelpers.GetCiDataDirectory();

            // gitdata-01 => Git clone
            yield return
            [
                new TestItem(Path.Combine(dataFolder, "gitdata-01"))
                {
                    AuthorDate = "2021-02-26 18:32:13Z",
                    AuthorEmail = "tony.redondo@datadoghq.com",
                    AuthorName = "Tony Redondo",
                    Branch = "master",
                    Commit = "5b6f3a6dab5972d73a56dff737bd08d995255c08",
                    CommitterDate = "2021-02-26 18:32:13Z",
                    CommitterEmail = "noreply@github.com",
                    CommitterName = "GitHub",
                    Repository = "git@github.com:DataDog/dd-trace-dotnet.git",
                    SourceRoot = Path.Combine(dataFolder, "gitdata-01")
                }
            ];

            // gitdata-02 => Git clone  + git gc (force packs files)
            yield return
            [
                new TestItem(Path.Combine(dataFolder, "gitdata-02"))
                {
                    AuthorDate = "2021-02-26 18:32:13Z",
                    AuthorEmail = "tony.redondo@datadoghq.com",
                    AuthorName = "Tony Redondo",
                    Branch = "master",
                    Commit = "5b6f3a6dab5972d73a56dff737bd08d995255c08",
                    CommitterDate = "2021-02-26 18:32:13Z",
                    CommitterEmail = "noreply@github.com",
                    CommitterName = "GitHub",
                    Repository = "git@github.com:DataDog/dd-trace-dotnet.git",
                    SourceRoot = Path.Combine(dataFolder, "gitdata-02")
                }
            ];

            // gitdata-03 => Git clone + git checkout [sha]
            yield return
            [
                new TestItem(Path.Combine(dataFolder, "gitdata-03"))
                {
                    AuthorDate = "2021-02-26 18:32:13Z",
                    AuthorEmail = "tony.redondo@datadoghq.com",
                    AuthorName = "Tony Redondo",
                    Branch = null,
                    Commit = "5b6f3a6dab5972d73a56dff737bd08d995255c08",
                    CommitterDate = "2021-02-26 18:32:13Z",
                    CommitterEmail = "noreply@github.com",
                    CommitterName = "GitHub",
                    Repository = "git@github.com:DataDog/dd-trace-dotnet.git",
                    SourceRoot = Path.Combine(dataFolder, "gitdata-03")
                }
            ];

            // gitdata-04 => Git clone + git checkout [sha] + git gc (force packs files)
            yield return new object[]
            {
                new TestItem(Path.Combine(dataFolder, "gitdata-04"))
                {
                    AuthorDate = "2021-02-26 18:32:13Z",
                    AuthorEmail = "tony.redondo@datadoghq.com",
                    AuthorName = "Tony Redondo",
                    Branch = null,
                    Commit = "5b6f3a6dab5972d73a56dff737bd08d995255c08",
                    CommitterDate = "2021-02-26 18:32:13Z",
                    CommitterEmail = "noreply@github.com",
                    CommitterName = "GitHub",
                    Repository = "git@github.com:DataDog/dd-trace-dotnet.git",
                    SourceRoot = Path.Combine(dataFolder, "gitdata-04")
                },
            };

            // gitdata-05 => Git clone + git gc + git checkout tag
            yield return
            [
                new TestItem(Path.Combine(dataFolder, "gitdata-05"))
                {
                    AuthorDate = "2021-02-19 12:59:01Z",
                    AuthorEmail = "andrew.lock@datadoghq.com",
                    AuthorName = "Andrew Lock",
                    Branch = null,
                    Commit = "b667f427df9f9b0521b1b25ee0967896aa510012",
                    CommitterDate = "2021-02-19 12:59:01Z",
                    CommitterEmail = "noreply@github.com",
                    CommitterName = "GitHub",
                    Repository = "git@github.com:DataDog/dd-trace-dotnet.git",
                    SourceRoot = Path.Combine(dataFolder, "gitdata-05")
                }
            ];
        }

        [SkippableTheory]
        [MemberData(nameof(GetData))]
        public void ExtractGitDataFromFolder(TestItem testItem)
        {
            Directory.Exists(testItem.GitFolderPath).Should().BeTrue();
            AssertGitInfo(testItem, GitInfo.GetFrom(testItem.GitFolderPath));

            static void AssertGitInfo(TestItem testItem, IGitInfo gitInfo)
            {
                gitInfo.AuthorDate.Should().NotBeNull();
                gitInfo.AuthorDate!.Value.ToString("u").Should().Be(testItem.AuthorDate);
                gitInfo.AuthorEmail.Should().Be(testItem.AuthorEmail);
                gitInfo.AuthorName.Should().Be(testItem.AuthorName);
                gitInfo.Branch.Should().Be(testItem.Branch);
                gitInfo.Commit.Should().Be(testItem.Commit);
                gitInfo.CommitterDate.Should().NotBeNull();
                gitInfo.CommitterDate!.Value.ToString("u").Should().Be(testItem.CommitterDate);
                gitInfo.CommitterEmail.Should().Be(testItem.CommitterEmail);
                gitInfo.CommitterName.Should().Be(testItem.CommitterName);
                gitInfo.Message.Should().NotBeNull();
                gitInfo.Repository.Should().Be(testItem.Repository);
                gitInfo.SourceRoot.Should().Be(testItem.SourceRoot);
            }
        }

        [SkippableFact]
        public void GitCommandGitInfoProviderTest()
        {
            if (GitCommandGitInfoProvider.Instance.TryGetFrom(Environment.CurrentDirectory, out var gitInfo))
            {
                gitInfo.AuthorDate.Should().NotBeNull();
                gitInfo.AuthorEmail.Should().NotBeNull();
                gitInfo.AuthorName.Should().NotBeNull();
                gitInfo.Commit.Should().NotBeNull();
                gitInfo.CommitterDate.Should().NotBeNull();
                gitInfo.CommitterEmail.Should().NotBeNull();
                gitInfo.CommitterName.Should().NotBeNull();
                gitInfo.Message.Should().NotBeNull();
                gitInfo.Repository.Should().NotBeNull();
                gitInfo.SourceRoot.Should().NotBeNull();
            }
            else
            {
                var errors = string.Join(Environment.NewLine, gitInfo?.Errors ?? []);
                throw new Exception($"Error parsing git info from provider: {nameof(GitCommandGitInfoProvider)}{Environment.NewLine}{errors}");
            }
        }

        public class TestItem : IXunitSerializable
        {
            public TestItem()
            {
            }

            internal TestItem(string gitFolderPath)
            {
                GitFolderPath = gitFolderPath;
            }

            internal string GitFolderPath { get; set; }

            internal string AuthorDate { get; set; }

            internal string AuthorEmail { get; set; }

            internal string AuthorName { get; set; }

            internal string Branch { get; set; }

            internal string Commit { get; set; }

            internal string CommitterDate { get; set; }

            internal string CommitterEmail { get; set; }

            internal string CommitterName { get; set; }

            internal string Repository { get; set; }

            internal string SourceRoot { get; set; }

            public void Deserialize(IXunitSerializationInfo info)
            {
                GitFolderPath = info.GetValue<string>(nameof(GitFolderPath));
                AuthorDate = info.GetValue<string>(nameof(AuthorDate));
                AuthorEmail = info.GetValue<string>(nameof(AuthorEmail));
                AuthorName = info.GetValue<string>(nameof(AuthorName));
                Branch = info.GetValue<string>(nameof(Branch));
                Commit = info.GetValue<string>(nameof(Commit));
                CommitterDate = info.GetValue<string>(nameof(CommitterDate));
                CommitterEmail = info.GetValue<string>(nameof(CommitterEmail));
                CommitterName = info.GetValue<string>(nameof(CommitterName));
                Repository = info.GetValue<string>(nameof(Repository));
                SourceRoot = info.GetValue<string>(nameof(SourceRoot));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(GitFolderPath), GitFolderPath);
                info.AddValue(nameof(AuthorDate), AuthorDate);
                info.AddValue(nameof(AuthorEmail), AuthorEmail);
                info.AddValue(nameof(AuthorName), AuthorName);
                info.AddValue(nameof(Branch), Branch);
                info.AddValue(nameof(Commit), Commit);
                info.AddValue(nameof(CommitterDate), CommitterDate);
                info.AddValue(nameof(CommitterEmail), CommitterEmail);
                info.AddValue(nameof(CommitterName), CommitterName);
                info.AddValue(nameof(Repository), Repository);
                info.AddValue(nameof(SourceRoot), SourceRoot);
            }

            public override string ToString() => $"GitFolderPath={GitFolderPath}";
        }
    }
}
