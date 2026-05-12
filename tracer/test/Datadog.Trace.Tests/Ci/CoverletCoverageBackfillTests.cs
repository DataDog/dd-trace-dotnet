// <copyright file="CoverletCoverageBackfillTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class CoverletCoverageBackfillTests
{
    [Fact]
    public void MutatesExistingCoverletLineHitsOnly()
    {
        var lineHits = new Dictionary<int, int>
        {
            [1] = 1,
            [2] = 0,
            [3] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["src/Calculator.cs"] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = lineHits
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_0110_0000])
            });

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeTrue();

        updatedLines.Should().Be(2);
        lineHits[1].Should().Be(1);
        lineHits[2].Should().Be(1);
        lineHits[3].Should().Be(1);
        lineHits.Should().NotContainKey(4);
    }
}
