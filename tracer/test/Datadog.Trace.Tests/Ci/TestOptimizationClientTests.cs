// <copyright file="TestOptimizationClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Util.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class TestOptimizationClientTests
{
    [Fact]
    public void TestsConfigurationsSerializesTopLevelBundle()
    {
        var configurations = new TestsConfigurations(
            "linux",
            "1",
            "x64",
            runtimeName: "dotnet",
            runtimeVersion: "9.0.0",
            runtimeArchitecture: "x64",
            custom: null,
            testBundle: "Samples.XUnitTests");

        var serializedConfigurations = JsonHelper.SerializeObject(configurations);

        serializedConfigurations.Should().Contain("\"test.bundle\":\"Samples.XUnitTests\"");
        serializedConfigurations.Should().Contain("\"runtime.name\":\"dotnet\"");
    }

    [Fact]
    public void ParseSkippableTestsResponseKeepsBackendCoverage()
    {
        var response = TestOptimizationClient.ParseSkippableTestsResponse(
            """
            {
              "data": [
                {
                  "id": "Samples.XUnitTests.TestSuite.SimplePassTest",
                  "type": "test_params",
                  "attributes": {
                    "suite": "Samples.XUnitTests.TestSuite",
                    "name": "SimplePassTest",
                    "_missing_line_code_coverage": false
                  }
                }
              ],
              "meta": {
                "correlation_id": "2e8a36bda770b683345957cc6c15baf9",
                "coverage": {
                  "src/Calculator.cs": "wA=="
                }
              }
            }
            """,
            customConfigurations: null);

        response.CorrelationId.Should().Be("2e8a36bda770b683345957cc6c15baf9");
        response.Tests.Should().ContainSingle(test => test.Name == "SimplePassTest" && test.MissingLineCodeCoverage == false);
        response.Coverage.IsPresent.Should().BeTrue();
        response.Coverage.IsValid.Should().BeTrue();
        response.Coverage.ExecutedLinesByRelativePath.Should().ContainKey("src/Calculator.cs");
        response.Coverage.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal((byte)0xC0);
        response.Coverage.TotalBitmapBytes.Should().Be(1);
        response.IsCoverageBackfillSafe.Should().BeTrue();

        var serializedResponse = JsonHelper.SerializeObject(response);
        serializedResponse.Should().Contain("\"coverage\"");
        serializedResponse.Should().Contain("\"coverage_backfill_safe\":true");
    }

    [Fact]
    public void ParseSkippableTestsResponseMarksCoverageUnsafeWhenConfigurationsFilterTests()
    {
        var response = TestOptimizationClient.ParseSkippableTestsResponse(
            """
            {
              "data": [
                {
                  "id": "Samples.XUnitTests.TestSuite.SimplePassTest",
                  "type": "test_params",
                  "attributes": {
                    "suite": "Samples.XUnitTests.TestSuite",
                    "name": "SimplePassTest",
                    "configurations": {
                      "custom": {
                        "queue": "nightly"
                      }
                    },
                    "_missing_line_code_coverage": false
                  }
                }
              ],
              "meta": {
                "correlation_id": "2e8a36bda770b683345957cc6c15baf9",
                "coverage": {
                  "src/Calculator.cs": "wA=="
                }
              }
            }
            """,
            new Dictionary<string, string> { { "queue", "default" } });

        response.Tests.Should().BeEmpty();
        response.Coverage.IsPresent.Should().BeTrue();
        response.Coverage.IsValid.Should().BeTrue();
        response.IsCoverageBackfillSafe.Should().BeFalse();
    }

    [Fact]
    public void ParseSkippableTestsResponseFiltersMismatchedTopLevelBundle()
    {
        var response = TestOptimizationClient.ParseSkippableTestsResponse(
            """
            {
              "data": [
                {
                  "id": "Samples.XUnitTests.TestSuite.SimplePassTest",
                  "type": "test_params",
                  "attributes": {
                    "suite": "Samples.XUnitTests.TestSuite",
                    "name": "SimplePassTest",
                    "configurations": {
                      "test.bundle": "Other.Tests"
                    },
                    "_missing_line_code_coverage": false
                  }
                }
              ],
              "meta": {
                "correlation_id": "2e8a36bda770b683345957cc6c15baf9",
                "coverage": {
                  "src/Calculator.cs": "wA=="
                }
              }
            }
            """,
            customConfigurations: null,
            scope: new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a"));

        response.Tests.Should().BeEmpty();
        response.Coverage.IsPresent.Should().BeTrue();
        response.Coverage.IsValid.Should().BeTrue();
        response.IsCoverageBackfillSafe.Should().BeFalse();
    }

    [Fact]
    public void ModuleScopedSkippableCandidateMatchesLocalBundle()
    {
        var candidate = new SkippableTest(
            "SimplePassTest",
            "Samples.XUnitTests.TestSuite",
            parameters: null,
            new TestsConfigurations(
                "linux",
                "1",
                "x64",
                runtimeName: null,
                runtimeVersion: null,
                runtimeArchitecture: null,
                new Dictionary<string, string> { [TestTags.Bundle] = "Samples.XUnitTests" }));

        candidate.MatchesModuleScope("Samples.XUnitTests").Should().BeTrue();
        candidate.MatchesModuleScope("Other.Tests").Should().BeFalse();
        candidate.MatchesModuleScope(null).Should().BeFalse();
    }

    [Fact]
    public void ModuleScopedSkippableCandidateMatchesTopLevelBundle()
    {
        var candidate = new SkippableTest(
            "SimplePassTest",
            "Samples.XUnitTests.TestSuite",
            parameters: null,
            new TestsConfigurations(
                "linux",
                "1",
                "x64",
                runtimeName: null,
                runtimeVersion: null,
                runtimeArchitecture: null,
                custom: null,
                testBundle: "Samples.XUnitTests"));

        candidate.MatchesModuleScope("Samples.XUnitTests").Should().BeTrue();
        candidate.MatchesModuleScope("Other.Tests").Should().BeFalse();
        candidate.MatchesModuleScope(null).Should().BeFalse();
    }

    [Fact]
    public async Task CachedClientCachesSkippableTestsByFullScopeFingerprint()
    {
        var sourceClient = new ScopedCountingClient();
        var cachedClient = new CachedTestOptimizationClient(sourceClient);
        var netFrameworkScope = new SkippableTestsRequestScope("Samples.XUnitTests", "net-framework-scope");
        var netScope = new SkippableTestsRequestScope("Samples.XUnitTests", "net-scope");
        var nunitScope = new SkippableTestsRequestScope("Samples.NUnitTests", "nunit-scope");

        await cachedClient.GetSkippableTestsAsync(netFrameworkScope);
        await cachedClient.GetSkippableTestsAsync(netFrameworkScope);
        await cachedClient.GetSkippableTestsAsync(netScope);
        await cachedClient.GetSkippableTestsAsync(nunitScope);

        sourceClient.SkippableRequestScopes.Should().Equal(netFrameworkScope, netScope, nunitScope);
    }

    [Fact]
    public void UnscopedSkippableCandidateKeepsLegacyMatching()
    {
        var candidate = new SkippableTest(
            "SimplePassTest",
            "Samples.XUnitTests.TestSuite",
            parameters: null,
            configurations: null);

        candidate.MatchesModuleScope(null).Should().BeTrue();
        candidate.MatchesModuleScope("Any.Module").Should().BeTrue();
    }

    [Fact]
    public void ModuleScopedDuplicateCandidatesAreNotAmbiguous()
    {
        var candidates = new[]
        {
            CreateModuleScopedCandidate("Samples.XUnitTests"),
            CreateModuleScopedCandidate("Samples.NUnitTests")
        };

        TestOptimizationSkippableFeature.HasAmbiguousCoverageScope(candidates).Should().BeFalse();
    }

    [Fact]
    public void UnscopedDuplicateCandidatesRemainAmbiguous()
    {
        var candidates = new[]
        {
            CreateModuleScopedCandidate("Samples.XUnitTests"),
            new SkippableTest(
                "SimplePassTest",
                "Samples.Tests.TestSuite",
                parameters: null,
                configurations: null)
        };

        TestOptimizationSkippableFeature.HasAmbiguousCoverageScope(candidates).Should().BeTrue();
    }

    private static SkippableTest CreateModuleScopedCandidate(string moduleName)
    {
        return new SkippableTest(
            "SimplePassTest",
            "Samples.Tests.TestSuite",
            parameters: null,
            new TestsConfigurations(
                "linux",
                "1",
                "x64",
                runtimeName: null,
                runtimeVersion: null,
                runtimeArchitecture: null,
                new Dictionary<string, string> { [TestTags.Bundle] = moduleName }));
    }

    private sealed class ScopedCountingClient : ITestOptimizationClient
    {
        public List<SkippableTestsRequestScope> SkippableRequestScopes { get; } = [];

        public Task<TestOptimizationClient.SettingsResponse> GetSettingsAsync(bool skipFrameworkInfo = false)
            => Task.FromResult(default(TestOptimizationClient.SettingsResponse));

        public Task<TestOptimizationClient.KnownTestsResponse> GetKnownTestsAsync()
            => Task.FromResult(default(TestOptimizationClient.KnownTestsResponse));

        public Task<TestOptimizationClient.SearchCommitResponse> GetCommitsAsync()
            => Task.FromResult(default(TestOptimizationClient.SearchCommitResponse));

        public Task<TestOptimizationClient.SkippableTestsResponse> GetSkippableTestsAsync(SkippableTestsRequestScope scope = default)
        {
            SkippableRequestScopes.Add(scope);
            return Task.FromResult(default(TestOptimizationClient.SkippableTestsResponse));
        }

        public Task<long> SendPackFilesAsync(string commitSha, string[]? commitsToInclude, string[]? commitsToExclude)
            => Task.FromResult(0L);

        public Task<long> UploadRepositoryChangesAsync()
            => Task.FromResult(0L);

        public Task<TestOptimizationClient.TestManagementResponse> GetTestManagementTests()
            => Task.FromResult(new TestOptimizationClient.TestManagementResponse());
    }
}
