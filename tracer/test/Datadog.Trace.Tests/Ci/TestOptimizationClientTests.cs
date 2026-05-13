// <copyright file="TestOptimizationClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Util.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class TestOptimizationClientTests
{
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
}
