// <copyright file="GitMetadataTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Trace;
using Datadog.Trace.TestHelpers;
using MessagePack;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.DebugInfo
{
    public class GitMetadataTest
    {
        private static readonly Regex GitRepositoryPattern = new("git.repository_url:(?<repository_url>[^,\"]+)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private static readonly Regex GitCommitShaPattern = new("git.commit.sha:(?<commit_sha>[^,\"]+)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private readonly ITestOutputHelper _output;

        public GitMetadataTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckGitMetataFromEnvironmentVariables(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 18", enableTracer: true);
            runner.Environment.CustomEnvironmentVariables["DD_TRACE_GIT_METADATA_ENABLED"] = "1";
            runner.Environment.CustomEnvironmentVariables["DD_GIT_REPOSITORY_URL"] = "https://Myrepository";
            runner.Environment.CustomEnvironmentVariables["DD_GIT_COMMIT_SHA"] = "42";

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            var profilerGitMetadata = new HashSet<(string, string)>();
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                CollectProfilerGitMetadata(ctx.Value.Request, profilerGitMetadata);
            };

            var tracerGitMetadata = new HashSet<(string, string)>();

            agent.TracerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                CollectTracerGitMetadata(ctx.Value.Request, tracerGitMetadata);
            };

            runner.Run(agent);

            Assert.Equal(profilerGitMetadata, tracerGitMetadata);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckGitMetataFromDdTags(string appName, string framework, string appAssembly)
        {
            // In this case, the profiler does not do anything specific.
            // We just get the DD_TAGS and add them to the profile/request
            // But the goal of the test is to ensure that the tracer has extracted them and we have the same.
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 18", enableTracer: true);
            runner.Environment.CustomEnvironmentVariables["DD_TRACE_GIT_METADATA_ENABLED"] = "1";
            runner.Environment.CustomEnvironmentVariables["DD_TAGS"] = "git.repository_url:https://Myrepository,git.commit.sha:42";

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            var profilerGitMetadata = new HashSet<(string, string)>();
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                CollectProfilerGitMetadata(ctx.Value.Request, profilerGitMetadata);
            };

            var tracerGitMetadata = new HashSet<(string, string)>();

            agent.TracerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                CollectTracerGitMetadata(ctx.Value.Request, tracerGitMetadata);
            };

            runner.Run(agent);

            Assert.Equal(profilerGitMetadata, tracerGitMetadata);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckGitMetataFromFromBinary(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 18", enableTracer: true);
            runner.Environment.CustomEnvironmentVariables["DD_TRACE_GIT_METADATA_ENABLED"] = "1";

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            var profilerGitMetadata = new HashSet<(string, string)>();
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                CollectProfilerGitMetadata(ctx.Value.Request, profilerGitMetadata);
            };

            var tracerGitMetadata = new HashSet<(string, string)>();

            agent.TracerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                CollectTracerGitMetadata(ctx.Value.Request, tracerGitMetadata);
            };

            runner.Run(agent);

            Assert.Equal(profilerGitMetadata, tracerGitMetadata);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckGitMetataFromEnvironmentVariablesIfTracerFeatureIsDisabled(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 18", enableTracer: true);
            runner.Environment.CustomEnvironmentVariables["DD_TRACE_GIT_METADATA_ENABLED"] = "0";
            runner.Environment.CustomEnvironmentVariables["DD_GIT_REPOSITORY_URL"] = "https://Myrepository";
            runner.Environment.CustomEnvironmentVariables["DD_GIT_COMMIT_SHA"] = "42";

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            var profilerGitMetadata = new HashSet<(string, string)>();
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                CollectProfilerGitMetadata(ctx.Value.Request, profilerGitMetadata);
            };

            var tracerGitMetadata = new HashSet<(string, string)>();

            agent.TracerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                CollectTracerGitMetadata(ctx.Value.Request, tracerGitMetadata);
            };

            runner.Run(agent);

            Assert.Empty(tracerGitMetadata);
            Assert.Single(profilerGitMetadata);

            var gitMetadata = profilerGitMetadata.First();
            Assert.Equal("https://Myrepository", gitMetadata.Item1);
            Assert.Equal("42", gitMetadata.Item2);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckGitMetataFromDdTagsIfTracerFeatureIsDisabled(string appName, string framework, string appAssembly)
        {
            // In this case, the profiler does not do anything specific.
            // We just get the DD_TAGS and add them to the profile/request
            // But the goal of the test is to ensure that the tracer has extracted them and we have the same.
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 18", enableTracer: true);
            runner.Environment.CustomEnvironmentVariables["DD_TRACE_GIT_METADATA_ENABLED"] = "0";
            runner.Environment.CustomEnvironmentVariables["DD_TAGS"] = "git.repository_url:https://Myrepository,git.commit.sha:42";

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            var profilerGitMetadata = new HashSet<(string, string)>();
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                CollectProfilerGitMetadata(ctx.Value.Request, profilerGitMetadata);
            };

            var tracerGitMetadata = new HashSet<(string, string)>();

            agent.TracerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                CollectTracerGitMetadata(ctx.Value.Request, tracerGitMetadata);
            };

            runner.Run(agent);

            Assert.Empty(tracerGitMetadata);
            Assert.Single(profilerGitMetadata);

            var gitMetadata = profilerGitMetadata.First();
            Assert.Equal("https://Myrepository", gitMetadata.Item1);
            Assert.Equal("42", gitMetadata.Item2);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckGitMetataFromDdTagsIfTracerIsDisabled(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 18", enableTracer: false);
            runner.Environment.CustomEnvironmentVariables["DD_GIT_REPOSITORY_URL"] = "https://Myrepository";
            runner.Environment.CustomEnvironmentVariables["DD_GIT_COMMIT_SHA"] = "42";

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            var profilerGitMetadata = new HashSet<(string, string)>();
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                CollectProfilerGitMetadata(ctx.Value.Request, profilerGitMetadata);
            };

            var tracerGitMetadata = new HashSet<(string, string)>();

            agent.TracerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                CollectTracerGitMetadata(ctx.Value.Request, tracerGitMetadata);
            };

            runner.Run(agent);

            Assert.Empty(tracerGitMetadata);
            Assert.Single(profilerGitMetadata);

            var gitMetadata = profilerGitMetadata.First();
            Assert.Equal("https://Myrepository", gitMetadata.Item1);
            Assert.Equal("42", gitMetadata.Item2);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void EnsureNoGitMetataIfNotPresentInEnvVars(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 18", enableTracer: false);

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            var profilerGitMetadata = new HashSet<(string, string)>();
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                CollectProfilerGitMetadata(ctx.Value.Request, profilerGitMetadata); ;
            };

            runner.Run(agent);

            Assert.Empty(profilerGitMetadata);
        }

        private static void CollectProfilerGitMetadata(HttpListenerRequest request, HashSet<(string RepositoryUrl, string CommitSha)> profilerGitMetadata)
        {
            var gitMetadata = ExtractProfilerGitMetadata(request);
            if (string.IsNullOrWhiteSpace(gitMetadata.Repository) && string.IsNullOrWhiteSpace(gitMetadata.CommitSha))
            {
                return;
            }

            profilerGitMetadata.Add(gitMetadata);
        }

        private static void CollectTracerGitMetadata(HttpListenerRequest request, HashSet<(string RepositoryUrl, string CommitSha)> profilerGitMetadata)
        {
            var gitMetadata = ExtractTracerGitMetadata(request);
            profilerGitMetadata.UnionWith(gitMetadata);
        }

        private static (string Repository, string CommitSha) ExtractProfilerGitMetadata(HttpListenerRequest request)
        {
            string text;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                text = reader.ReadToEnd();
            }

            var matchRepo = GitRepositoryPattern.Match(text);
            var matchCommitSha = GitCommitShaPattern.Match(text);
            return (matchRepo.Groups["repository_url"].Value, matchCommitSha.Groups["commit_sha"].Value);
        }

        private static HashSet<(string Repository, string CommitSha)> ExtractTracerGitMetadata(HttpListenerRequest request)
        {
            var traces = MessagePackSerializer.Deserialize<List<List<MockSpan>>>(request.InputStream);

            var gitMetadata = new HashSet<(string, string)>();
            foreach (var trace in traces)
            {
                foreach (var span in trace)
                {
                    var gitRepositoryUrl = string.Empty;
                    span.Tags?.TryGetValue(Tags.GitRepositoryUrl, out gitRepositoryUrl);
                    var gitCommitSha = string.Empty;
                    span.Tags?.TryGetValue(Tags.GitCommitSha, out gitCommitSha);

                    if (string.IsNullOrWhiteSpace(gitRepositoryUrl) && string.IsNullOrWhiteSpace(gitCommitSha))
                    {
                        continue;
                    }

                    gitMetadata.Add((gitRepositoryUrl, gitCommitSha));
                }
            }

            return gitMetadata;
        }
    }
}
