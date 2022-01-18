// <copyright file="DoubleExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ExtensionMethods;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ExtensionMethods;

public class DoubleExtensionsTests
{
    [Theory]
    [InlineData(0, 4, 0)]
    [InlineData(1, 4, 1)]
    [InlineData(0.98761, 1, 1.0)]
    [InlineData(0.98761, 2, 0.99)]
    [InlineData(0.98761, 3, 0.988)]
    [InlineData(0.98761, 4, 0.9877)]
    [InlineData(0.98769, 4, 0.9877)]
    public void RoundUp(double? initialValue, int digits, double? expected)
    {
        initialValue.RoundUp(digits).Should().Be(expected);
    }
}
