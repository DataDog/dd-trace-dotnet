// <copyright file="AwsPlatformDetectionTests.cs" company="Datadog">
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
[EnvironmentRestorer("AWS_LAMBDA_FUNCTION_NAME")]
public class AwsPlatformDetectionTests
{
    [Theory]
    [PairwiseData]
    public void IsAwsLambda(bool value)
    {
        if (value)
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "test");
        }

        AwsPlatformDetection.IsAwsLambda.Should().Be(value);
    }
}
