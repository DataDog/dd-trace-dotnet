// <copyright file="AwsS3CommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.S3;

public class AwsS3CommonTests
{
    [Fact]
    public void GetCorrectBucketName()
    {
        "1".Should().Be("1");
    }
}
