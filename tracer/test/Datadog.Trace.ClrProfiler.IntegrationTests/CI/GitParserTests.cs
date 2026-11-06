// <copyright file="GitParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    [Trait("Area", "CIVisibility")]
    [Collection(nameof(CIEnvironmentVariableTests))]
    public class GitParserTests
    {
        public static IEnumerable<object[]> GetTagCheckoutCases()
        {
            yield return [false, false];
            yield return [true, false];
            yield return [true, true];
        }

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

        [SkippableFact]
        public void GitInfoCurrentReadsCurrentCheckoutMetadata()
        {
            var gitInfo = GitInfo.GetCurrent();

            gitInfo.Commit.Should().NotBeNullOrEmpty();
            gitInfo.Repository.Should().NotBeNullOrEmpty();
            gitInfo.SourceRoot.Should().NotBeNullOrEmpty();
        }

        [SkippableTheory]
        [MemberData(nameof(GetTagCheckoutCases))]
        public void OnlyReportsAnUnambiguousDetachedTag(bool detached, bool multiple)
        {
            using var checkout = new GitCheckout();
            var config = Path.Combine(checkout.Root, ".git", "config");
            File.AppendAllText(config, "\n[column]\nui = always\ntag = always\n");
            if (detached)
            {
                File.WriteAllText(Path.Combine(checkout.Root, ".git", "HEAD"), "5b6f3a6dab5972d73a56dff737bd08d995255c08\n");
            }

            var tags = Path.Combine(checkout.Root, ".git", "refs", "tags");
            Directory.CreateDirectory(tags);
            File.WriteAllText(Path.Combine(tags, "v1"), "5b6f3a6dab5972d73a56dff737bd08d995255c08\n");
            if (multiple)
            {
                File.WriteAllText(Path.Combine(tags, "stable"), "5b6f3a6dab5972d73a56dff737bd08d995255c08\n");
            }

            if (!GitCommandGitInfoProvider.Instance.TryGetFrom(checkout.Root, out var info))
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, info?.Errors ?? []));
            }

            info.Tag.Should().Be(detached && !multiple ? "v1" : null);
        }

        [SkippableTheory]
        [CombinatorialData]
        public void DetectsImpactedFilesInDetachedCheckout(bool hasBaseCommit, bool differentWorkspace)
        {
            using var checkout = new GitCheckout();
            const string commit = "5b6f3a6dab5972d73a56dff737bd08d995255c08";
            const string baseCommit = "98cd7c8e9cf71e02dc28bd9b13928bee0f85b74c";
            File.WriteAllText(Path.Combine(checkout.Root, ".git", "HEAD"), commit + "\n");
            Directory.CreateDirectory(Path.Combine(checkout.Root, ".git", "refs", "heads"));
            File.WriteAllText(Path.Combine(checkout.Root, ".git", "refs", "heads", "main"), baseCommit + "\n");
            var reset = GitCommandHelper.RunGitCommand(checkout.Root, "reset --hard HEAD", MetricTags.CIVisibilityCommands.GetHead)
                     ?? throw new InvalidOperationException("Git checkout reset did not start.");
            reset.ExitCode.Should().Be(0, reset.Error);
            File.AppendAllText(Path.Combine(checkout.Root, "README.md"), "\nChanged in the checkout\n");
            GitCommandGitInfoProvider.Instance.TryGetFrom(checkout.Root, out var gitInfo).Should().BeTrue();
            var workspace = differentWorkspace ? checkout.Root + "-provider" : checkout.Root;
            var values = new ImpactedEnvironmentValues(workspace, hasBaseCommit ? baseCommit : null, gitInfo);
            values.Branch.Should().BeNull();

            var impacted = ImpactedTestsModule.CreateInstance(values, "main");

            impacted.BaseCommitSha.Should().Be(baseCommit);
            impacted.CurrentCommitSha.Should().Be(commit);
            impacted.ModifiedFiles.Should().NotBeEmpty();
            values.Branch.Should().BeNull();
            values.WorkspacePath.Should().Be(workspace);
        }

        [SkippableFact]
        public void PreservesAuthorSeparatorsAndTrailingMessageQuotes()
        {
            using var checkout = new GitCheckout();
            var treeOutput = GitCommandHelper.RunGitCommand(checkout.Root, "rev-parse HEAD^{tree}", MetricTags.CIVisibilityCommands.GetHead)
                          ?? throw new InvalidOperationException("Git tree lookup did not start.");
            treeOutput.ExitCode.Should().Be(0, treeOutput.Error);
            var tree = treeOutput.Output.Trim();
            var message = "Update [\"a\",\"b\"]'";
            var content = $"tree {tree}\nauthor Author|,|Name <author@example.com> 1 +0000\ncommitter Committer <committer@example.com> 2 +0000\n\n{message}\n";
            var objectOutput = GitCommandHelper.RunGitCommand(checkout.Root, "hash-object -t commit -w --stdin", MetricTags.CIVisibilityCommands.GetHead, input: content, useCache: false)
                            ?? throw new InvalidOperationException("Git object creation did not start.");
            objectOutput.ExitCode.Should().Be(0, objectOutput.Error);
            var sha = objectOutput.Output.Trim();
            File.WriteAllText(Path.Combine(checkout.Root, ".git", "HEAD"), sha + "\n");

            if (!GitCommandGitInfoProvider.Instance.TryGetFrom(checkout.Root, out var info))
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, info?.Errors ?? []));
            }

            info.AuthorName.Should().Be("Author|,|Name");
            info.Message.Should().Be(message);
            var fetched = GitCommandHelper.FetchCommitData(checkout.Root, sha)
                       ?? throw new InvalidOperationException("Git commit details were unavailable.");
            fetched.CommitSha.Should().Be(sha);
            fetched.AuthorName.Should().Be("Author|,|Name");
            fetched.CommitMessage.Should().Be(message);
        }

        [SkippableFact]
        public void UsesTheCurrentCheckoutRootAndSelectedCommitHistory()
        {
            using var checkout = new GitCheckout();
            var originalDirectory = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = checkout.Root;
                GitInfo.GetCurrent().SourceRoot.Should().Be(Environment.CurrentDirectory);
            }
            finally
            {
                Environment.CurrentDirectory = originalDirectory;
            }

            var selected = "98cd7c8e9cf71e02dc28bd9b13928bee0f85b74c";
            var commits = GitCommandHelper.GetLocalCommits(checkout.Root, selected);
            commits.Should().StartWith(selected).And.NotContain("5b6f3a6dab5972d73a56dff737bd08d995255c08");
            GitCommandHelper.GetLocalCommits(checkout.Root, new string('f', 40)).Should().BeEmpty();
            GitCommandHelper.GetLocalCommits(checkout.Root, "--all").Should().BeEmpty();
        }

        [SkippableTheory]
        [CombinatorialData]
        public async Task RecoversTheSelectedCommitBeforeUploadingAShallowCheckout(bool remoteAvailable, bool unrelated)
        {
            using var remote = new GitCheckout();
            string Run(string arguments, string input = null)
            {
                var output = GitCommandHelper.RunGitCommand(remote.Root, arguments, MetricTags.CIVisibilityCommands.GetHead, input, useCache: false)
                          ?? throw new InvalidOperationException("Git fixture command did not start.");
                output.ExitCode.Should().Be(0, output.Error);
                return output.Output.Trim();
            }

            var tree = Run("hash-object -t tree -w --stdin", string.Empty);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var selected = Run("hash-object -t commit -w --stdin", $"tree {tree}\nauthor Test <test@example.com> {timestamp} +0000\ncommitter Test <test@example.com> {timestamp} +0000\n\nselected\n");
            var root = unrelated ? Run("hash-object -t commit -w --stdin", $"tree {tree}\nauthor Test <test@example.com> {timestamp} +0000\ncommitter Test <test@example.com> {timestamp} +0000\n\nother root\n") : selected;
            var middle = Run("hash-object -t commit -w --stdin", $"tree {tree}\nparent {root}\nauthor Test <test@example.com> {timestamp} +0000\ncommitter Test <test@example.com> {timestamp} +0000\n\nmiddle\n");
            var head = Run("hash-object -t commit -w --stdin", $"tree {tree}\nparent {middle}\nauthor Test <test@example.com> {timestamp} +0000\ncommitter Test <test@example.com> {timestamp} +0000\n\nhead\n");
            Run("update-ref refs/heads/juan-fernandez/shallow-tip " + head);
            var checkout = Path.Combine(remote.Root, "shallow");
            Run($"clone --depth=2 --no-local --branch juan-fernandez/shallow-tip \"{remote.Root}\" \"{checkout}\"");
            if (!remoteAvailable)
            {
                Run($"-C \"{checkout}\" remote set-url origin \"{Path.Combine(remote.Root, "unavailable")}\"");
            }

            GitCommandHelper.GetLocalCommits(checkout, selected).Should().BeEmpty();
            var values = CIEnvironmentValues.Create(new Dictionary<string, string> { [ConfigurationKeys.CIVisibility.GitCommitSha] = selected });
            values.GitSearchFolder = checkout;
            var response = new Mock<IApiResponse>();
            response.Setup(x => x.StatusCode).Returns(200);
            response.Setup(x => x.GetStreamAsync()).ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("{\"data\":[]}")));
            var packResponse = new Mock<IApiResponse>();
            packResponse.Setup(x => x.StatusCode).Returns(204);
            packResponse.Setup(x => x.GetStreamAsync()).ReturnsAsync(() => new MemoryStream());
            var request = new Mock<IApiRequest>();
            var searches = new List<string>();
            var uploads = new List<MultipartFormItem[]>();
            request.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<string>()))
                   .Callback<ArraySegment<byte>, string>((body, _) => searches.Add(Encoding.UTF8.GetString(body.Array, body.Offset, body.Count)))
                   .ReturnsAsync(response.Object);
            request.Setup(x => x.PostAsync(It.IsAny<MultipartFormItem[]>(), It.IsAny<MultipartCompression>()))
                   .Callback<MultipartFormItem[], MultipartCompression>((items, _) => uploads.Add(items))
                   .ReturnsAsync(packResponse.Object);
            var factory = new Mock<IApiRequestFactory>();
            factory.Setup(x => x.GetEndpoint(It.IsAny<string>())).Returns<string>(path => new Uri("http://localhost/" + path));
            factory.Setup(x => x.Create(It.IsAny<Uri>())).Returns(request.Object);
            var management = new Mock<ITestOptimizationTracerManagement>();
            management.Setup(x => x.EventPlatformProxySupport).Returns(EventPlatformProxySupport.V4);
            management.Setup(x => x.GetRequestFactory(It.IsAny<TracerSettings>(), It.IsAny<TimeSpan>())).Returns(factory.Object);
            var optimization = new Mock<ITestOptimization>();
            optimization.Setup(x => x.CIValues).Returns(values);
            optimization.Setup(x => x.Settings).Returns(new TestOptimizationSettings(new DictionaryConfigurationSource(new Dictionary<string, string>()), NullConfigurationTelemetry.Instance));
            optimization.Setup(x => x.TracerManagement).Returns(management.Object);

            var bytes = await TestOptimizationClient.Create(checkout, optimization.Object).UploadRepositoryChangesAsync();

            if (remoteAvailable)
            {
                bytes.Should().BeGreaterThan(0);
                searches.Should().ContainSingle().Which.Should().Contain(selected).And.NotContain(head);
                var parts = uploads.Should().ContainSingle().Which;
                var pushedSha = parts.Should().ContainSingle(part => part.Name == "pushedSha").Which.ContentInBytes
                             ?? throw new InvalidOperationException("The selected SHA was not uploaded.");
                Encoding.UTF8.GetString(pushedSha.Array, pushedSha.Offset, pushedSha.Count).Should().Contain(selected).And.NotContain(head);
                var pack = parts.Should().ContainSingle(part => part.Name == "packfile").Which.ContentInBytes
                        ?? throw new InvalidOperationException("The Git pack was not uploaded.");
                var packFile = Path.Combine(remote.Root, "uploaded.pack");
                File.WriteAllBytes(packFile, pack.ToArray());
                Run($"index-pack \"{packFile}\"");
                var objects = Run($"verify-pack -v \"{Path.ChangeExtension(packFile, "idx")}\"");
                objects.Should().Contain(selected + " commit ").And.NotContain(head + " commit ");
            }
            else
            {
                bytes.Should().Be(0);
                searches.Should().BeEmpty();
                uploads.Should().BeEmpty();
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

        private sealed class ImpactedEnvironmentValues : CIEnvironmentValues<DictionaryValuesProvider>
        {
            private readonly string _root;
            private readonly string _baseCommit;

            public ImpactedEnvironmentValues(string root, string baseCommit, IGitInfo gitInfo)
                : base(new DictionaryValuesProvider(new Dictionary<string, string>()))
            {
                _root = root;
                _baseCommit = baseCommit;
                Setup(gitInfo);
            }

            protected override void OnInitialize(IGitInfo gitInfo)
            {
                Branch = "provider-branch";
                WorkspacePath = _root;
                PrBaseBranch = "main";
                PrBaseCommit = _baseCommit;
            }
        }

        private sealed class GitCheckout : IDisposable
        {
            public GitCheckout()
            {
                Root = Path.Combine(Path.GetTempPath(), "dd-git-precedence-" + Path.GetRandomFileName());
                var destination = Path.Combine(Root, ".git");
                var fixture = Path.Combine(DataHelpers.GetCiDataDirectory(), "gitdata-02");
                Directory.CreateDirectory(destination);
                foreach (var folder in Directory.EnumerateDirectories(fixture, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(Path.Combine(destination, folder.Substring(fixture.Length + 1)));
                }

                foreach (var file in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
                {
                    File.Copy(file, Path.Combine(destination, file.Substring(fixture.Length + 1)));
                }
            }

            public string Root { get; }

            public void Dispose()
            {
                foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
