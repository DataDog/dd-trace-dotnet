// <copyright file="FeatureFlagsDiagnosticsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FeatureFlagsDiagnosticsTests
{
    [Theory]
    [InlineData(0.84551240822557006)]
    [InlineData(1.0000000000000002)]
    [InlineData(double.Epsilon)]
    [InlineData(double.MaxValue)]
    public void FormatValuePreservesDoublePrecision(double value)
    {
        double.Parse(FeatureFlagsDiagnostics.FormatValue(value), CultureInfo.InvariantCulture).Should().Be(value);
    }

    [Theory]
    [InlineData(1.0000001f)]
    [InlineData(float.Epsilon)]
    [InlineData(float.MaxValue)]
    public void FormatValuePreservesSinglePrecision(float value)
    {
        float.Parse(FeatureFlagsDiagnostics.FormatValue(value), CultureInfo.InvariantCulture).Should().Be(value);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("\"quoted\"")]
    [InlineData("back\\slash")]
    [InlineData("line\nbreak\r\n\ttab")]
    [InlineData("")]
    public void FormatMapEscapesKeys(string key)
    {
        var map = new Dictionary<string, string> { [key] = "value" };

        JsonConvert.DeserializeObject<Dictionary<string, string>>(FeatureFlagsDiagnostics.FormatMap(map))
                   .Should().BeEquivalentTo(map);
    }
}
