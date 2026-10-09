// <copyright file="AotRuntimeSelectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using Datadog.Trace.Tools.Runner.Aot;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class AotRuntimeSelectorTests
{
    [Theory]
    [InlineData("8.0.0", "11.0.0", "8.0.30,8.0.31,11.0.0-rc.1.26425.128", "8.0.31")] // the application's major version
    [InlineData("8.0.0", "8.0.31", "8.0.31,11.0.0-rc.1.26425.128", null)] // already on it
    [InlineData("9.0.0", "8.0.31", "8.0.31,10.0.5,11.0.0-rc.1.26425.128", "10.0.5")] // the closest newer one
    [InlineData("11.0.0", "8.0.31", "8.0.31,11.0.0-rc.1.26425.128", "11.0.0-rc.1.26425.128")] // a preview
    [InlineData("11.0.0", "8.0.31", "11.0.0-rc.1.26425.128,11.0.0", "11.0.0")] // a release before its previews
    [InlineData("10.0.0", "8.0.31", "8.0.31,9.0.20", null)] // none can load the application's assemblies
    [InlineData("9.0.0", "8.0.31", "", null)]
    public void SelectsTheRuntimeOfTheApplication(string target, string current, string installed, string? expected)
        => AotRuntimeSelector.Select(Version.Parse(target), Version.Parse(current), installed.Split(',', StringSplitOptions.RemoveEmptyEntries)).Should().Be(expected);
}
#endif
