// <copyright file="GcpInfoTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Serverless;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Serverless;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentRestorer(
    "FUNCTION_NAME",
    "GCP_PROJECT",
    "FUNCTION_TARGET",
    "K_SERVICE")]
public class GcpInfoTests
{
    [Theory]
    [PairwiseData]
    public void IsCloudFunctions(bool value, bool gen1)
    {
        if (value)
        {
            if (gen1)
            {
                Environment.SetEnvironmentVariable("FUNCTION_NAME", "test");
                Environment.SetEnvironmentVariable("GCP_PROJECT", "test");
            }
            else
            {
                // gen2
                Environment.SetEnvironmentVariable("K_SERVICE", "test");
                Environment.SetEnvironmentVariable("FUNCTION_TARGET", "test");
            }
        }

        new GcpInfo().IsCloudFunction.Should().Be(value);
    }
}
