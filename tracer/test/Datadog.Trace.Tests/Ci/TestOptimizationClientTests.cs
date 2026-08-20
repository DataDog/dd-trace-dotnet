// <copyright file="TestOptimizationClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util.Json;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class TestOptimizationClientTests : SettingsTestsBase
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
    public void CustomTestConfigurationsKeepModuleNamedKeys()
    {
        var customConfigurations = TestOptimizationClient.GetCustomTestsConfigurations(
            new Dictionary<string, string>
            {
                ["test.configuration.test.bundle"] = "Samples.XUnitTests",
                ["test.configuration.test.module"] = "Samples.XUnitTests",
                ["test.configuration.queue"] = "nightly"
            });

        customConfigurations.Should().HaveCount(3);
        customConfigurations.Should().Contain(TestTags.Bundle, "Samples.XUnitTests");
        customConfigurations.Should().Contain(TestTags.Module, "Samples.XUnitTests");
        customConfigurations.Should().Contain("queue", "nightly");
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
            """);

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
    public void ParseSkippableTestsResponseKeepsCoverageSafeWhenLineCoverageMetadataIsOmitted()
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
                    "name": "SimplePassTest"
                  }
                }
              ],
              "meta": {
                "coverage": {
                  "src/Calculator.cs": "wA=="
                }
              }
            }
            """);

        response.Tests.Should().ContainSingle(test => test.Name == "SimplePassTest" && test.MissingLineCodeCoverage == null);
        response.Coverage.IsPresent.Should().BeTrue();
        response.Coverage.IsValid.Should().BeTrue();
        response.IsCoverageBackfillSafe.Should().BeTrue();
    }

    [Fact]
    public void ParseSkippableTestsResponseKeepsCoverageSafeWhenBackendReportsMissingLineCoverage()
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
                    "_missing_line_code_coverage": true
                  }
                }
              ],
              "meta": {
                "coverage": {
                  "src/Calculator.cs": "wA=="
                }
              }
            }
            """);

        response.Tests.Should().ContainSingle(test => test.Name == "SimplePassTest" && test.MissingLineCodeCoverage == true);
        response.Coverage.IsPresent.Should().BeTrue();
        response.Coverage.IsValid.Should().BeTrue();
        response.IsCoverageBackfillSafe.Should().BeTrue();
    }

    [Fact]
    public void ParseSkippableTestsResponseKeepsBackendCustomConfigurations()
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
            """);

        response.Tests.Should().ContainSingle(test => test.Name == "SimplePassTest");
        response.Coverage.IsPresent.Should().BeTrue();
        response.Coverage.IsValid.Should().BeTrue();
        response.IsCoverageBackfillSafe.Should().BeTrue();
    }

    [Fact]
    public void ParseSkippableTestsResponseKeepsCustomBundleWithoutLocalCustomConfigurations()
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
                        "test.bundle": "Samples.XUnitTests"
                      }
                    },
                    "_missing_line_code_coverage": false
                  }
                }
              ],
              "meta": {
                "coverage": {
                  "src/Calculator.cs": "wA=="
                }
              }
            }
            """,
            scope: new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a"));

        response.Tests.Should().ContainSingle(test => test.Name == "SimplePassTest");
        response.IsCoverageBackfillSafe.Should().BeTrue();
    }

    [Fact]
    public void ParseSkippableTestsResponseKeepsMatchingCustomBundle()
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
                        "test.bundle": "Samples.XUnitTests"
                      }
                    },
                    "_missing_line_code_coverage": false
                  }
                }
              ],
              "meta": {
                "coverage": {
                  "src/Calculator.cs": "wA=="
                }
              }
            }
            """,
            scope: new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a"));

        response.Tests.Should().ContainSingle(test => test.Name == "SimplePassTest");
        response.IsCoverageBackfillSafe.Should().BeTrue();
    }

    [Fact]
    public void ParseSkippableTestsResponseKeepsMismatchedCustomBundle()
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
                        "test.bundle": "Other.Tests"
                      }
                    },
                    "_missing_line_code_coverage": false
                  }
                }
              ],
              "meta": {
                "coverage": {
                  "src/Calculator.cs": "wA=="
                }
              }
            }
            """,
            scope: new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a"));

        response.Tests.Should().ContainSingle(test => test.Name == "SimplePassTest");
        response.IsCoverageBackfillSafe.Should().BeTrue();
    }

    [Fact]
    public void ParseSkippableTestsResponseKeepsMatchingCustomModule()
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
                        "test.module": "Samples.XUnitTests"
                      }
                    },
                    "_missing_line_code_coverage": false
                  }
                }
              ],
              "meta": {
                "coverage": {
                  "src/Calculator.cs": "wA=="
                }
              }
            }
            """,
            scope: new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a"));

        response.Tests.Should().ContainSingle(test => test.Name == "SimplePassTest");
        response.IsCoverageBackfillSafe.Should().BeTrue();
    }

    [Fact]
    public void ParseSkippableTestsResponseKeepsMismatchedCustomModule()
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
                        "test.module": "Samples.XUnitTests"
                      }
                    },
                    "_missing_line_code_coverage": false
                  }
                }
              ],
              "meta": {
                "coverage": {
                  "src/Calculator.cs": "wA=="
                }
              }
            }
            """,
            scope: new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a"));

        response.Tests.Should().ContainSingle(test => test.Name == "SimplePassTest");
        response.IsCoverageBackfillSafe.Should().BeTrue();
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
            scope: new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a"));

        response.Tests.Should().BeEmpty();
        response.Coverage.IsPresent.Should().BeTrue();
        response.Coverage.IsValid.Should().BeTrue();
        response.IsCoverageBackfillSafe.Should().BeTrue();
    }

    [Fact]
    public void CustomBundleDoesNotDefineModuleScope()
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

        candidate.TryGetModuleScope(out _).Should().BeFalse();
        candidate.MatchesModuleScope("Samples.XUnitTests").Should().BeTrue();
        candidate.MatchesModuleScope("Other.Tests").Should().BeTrue();
        candidate.MatchesModuleScope(null).Should().BeTrue();
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
    public async Task FileClientCachesSkippableTestsByFullScopeFingerprintAndBypassesUnsafeUnscopedCoverageBackfillCache()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-file-client-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workspacePath);
            var settings = CreateSettings(
                (ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1"),
                (ConfigurationKeys.CIVisibility.CodeCoveragePath, Path.Combine(workspacePath, "coverage")));
            var testOptimization = CreateTestOptimization(settings, workspacePath);
            var sourceClient = new ScopedCountingClient();
            var fileClient = new FileTestOptimizationClient(sourceClient, testOptimization.Object);
            var netFrameworkScope = new SkippableTestsRequestScope("Samples.XUnitTests", "net-framework-scope");
            var netScope = new SkippableTestsRequestScope("Samples.XUnitTests", "net-scope");

            await fileClient.GetSkippableTestsAsync(netFrameworkScope);
            await fileClient.GetSkippableTestsAsync(netFrameworkScope);
            await fileClient.GetSkippableTestsAsync(netScope);
            await fileClient.GetSkippableTestsAsync();
            await fileClient.GetSkippableTestsAsync();

            sourceClient.SkippableRequestScopes.Should().Equal(netFrameworkScope, netScope, default, default);
        }
        finally
        {
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
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

    private static TestOptimizationSettings CreateSettings(params (string Key, string Value)[] values)
        => new(CreateConfigurationSource(values), NullConfigurationTelemetry.Instance);

    private static Mock<ITestOptimization> CreateTestOptimization(TestOptimizationSettings settings, string workspacePath)
    {
        var testOptimization = new Mock<ITestOptimization>();
        testOptimization.Setup(x => x.RunId).Returns("test-run");
        testOptimization.Setup(x => x.Settings).Returns(settings);
        testOptimization.Setup(x => x.CIValues).Returns(new TestCIEnvironmentValues(workspacePath));
        testOptimization.Setup(x => x.Log).Returns(DatadogLogging.GetLoggerFor(typeof(TestOptimizationClientTests)));
        return testOptimization;
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
            return Task.FromResult(
                new TestOptimizationClient.SkippableTestsResponse(
                    correlationId: scope.Fingerprint ?? "default-scope",
                    tests: [],
                    CoverageBackfillData.Missing,
                    isCoverageBackfillSafe: true));
        }

        public Task<long> SendPackFilesAsync(string commitSha, string[]? commitsToInclude, string[]? commitsToExclude)
            => Task.FromResult(0L);

        public Task<long> UploadRepositoryChangesAsync()
            => Task.FromResult(0L);

        public Task<TestOptimizationClient.TestManagementResponse> GetTestManagementTests()
            => Task.FromResult(new TestOptimizationClient.TestManagementResponse());
    }

    private sealed class TestCIEnvironmentValues : CIEnvironmentValues
    {
        public TestCIEnvironmentValues(string workspacePath)
        {
            WorkspacePath = workspacePath;
            Branch = "main";
            Commit = "abcdef123456";
        }

        protected override void Setup(IGitInfo gitInfo)
        {
        }
    }
}
