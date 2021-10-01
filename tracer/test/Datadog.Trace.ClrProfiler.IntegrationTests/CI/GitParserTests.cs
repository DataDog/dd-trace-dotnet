// <copyright file="GitParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Ci;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    public class GitParserTests
    {
        public static IEnumerable<object[]> GetData()
        {
            string dataFolder = DataHelpers.GetCiDataDirectory();

            // gitdata-01 => Git clone
            yield return new object[]
            {
                new TestItem(Path.Combine(dataFolder, "gitdata-01"))
                {
                    AuthorDate = "2021-02-26 18:32:13Z",
                    AuthorEmail = "tony.redondo@datadoghq.com",
                    AuthorName = "Tony Redondo",
                    Branch = "refs/heads/master",
                    Commit = "5b6f3a6dab5972d73a56dff737bd08d995255c08",
                    CommitterDate = "2021-02-26 18:32:13Z",
                    CommitterEmail = "noreply@github.com",
                    CommitterName = "GitHub",
                    Repository = "git@github.com:DataDog/dd-trace-dotnet.git",
                    SourceRoot = dataFolder
                },
            };

            // gitdata-02 => Git clone  + git gc (force packs files)
            yield return new object[]
            {
                new TestItem(Path.Combine(dataFolder, "gitdata-02"))
                {
                    AuthorDate = "2021-02-26 18:32:13Z",
                    AuthorEmail = "tony.redondo@datadoghq.com",
                    AuthorName = "Tony Redondo",
                    Branch = "refs/heads/master",
                    Commit = "5b6f3a6dab5972d73a56dff737bd08d995255c08",
                    CommitterDate = "2021-02-26 18:32:13Z",
                    CommitterEmail = "noreply@github.com",
                    CommitterName = "GitHub",
                    Repository = "git@github.com:DataDog/dd-trace-dotnet.git",
                    SourceRoot = dataFolder
                },
            };

            // gitdata-03 => Git clone + git checkout [sha]
            yield return new object[]
            {
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
                    SourceRoot = dataFolder
                },
            };

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
                    SourceRoot = dataFolder
                },
            };

            // gitdata-05 => Git clone + git gc + git checkout tag
            yield return new object[]
            {
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
                    SourceRoot = dataFolder
                },
            };
        }

        [TestCaseSource(nameof(GetData))]
        public void ExtractGitDataFromFolder(TestItem testItem)
        {
            Assert.True(Directory.Exists(testItem.GitFolderPath));

            var gitInfo = GitInfo.GetFrom(testItem.GitFolderPath);

            Assert.AreEqual(testItem.AuthorDate, gitInfo.AuthorDate.Value.ToString("u"));
            Assert.AreEqual(testItem.AuthorEmail, gitInfo.AuthorEmail);
            Assert.AreEqual(testItem.AuthorName, gitInfo.AuthorName);
            Assert.AreEqual(testItem.Branch, gitInfo.Branch);
            Assert.AreEqual(testItem.Commit, gitInfo.Commit);
            Assert.AreEqual(testItem.CommitterDate, gitInfo.CommitterDate.Value.ToString("u"));
            Assert.AreEqual(testItem.CommitterEmail, gitInfo.CommitterEmail);
            Assert.AreEqual(testItem.CommitterName, gitInfo.CommitterName);
            Assert.NotNull(gitInfo.Message);
            Assert.NotNull(gitInfo.PgpSignature);
            Assert.AreEqual(testItem.Repository, gitInfo.Repository);
            Assert.AreEqual(testItem.SourceRoot, gitInfo.SourceRoot);
        }

        public class TestItem
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

            public override string ToString() => $"GitFolderPath={GitFolderPath}";
        }
    }
}
