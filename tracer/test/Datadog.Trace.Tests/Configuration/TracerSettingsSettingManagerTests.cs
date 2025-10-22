// <copyright file="TracerSettingsSettingManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

public class TracerSettingsSettingManagerTests
{
    [Fact]
    public void UpdatingTracerSettingsDoesNotReplaceSettingsManager()
    {
        var tracerSettings = TracerSettings.Create([]);
        tracerSettings.MutableSettings.Should().BeSameAs(tracerSettings.InitialMutableSettings);

        var originalManager = tracerSettings.Manager;
        var newSettings = tracerSettings with { MutableSettings = MutableSettings.CreateForTesting(tracerSettings, []) };

        newSettings.MutableSettings.Should().NotBeSameAs(newSettings.InitialMutableSettings);
        newSettings.Manager.Should().BeSameAs(originalManager);
    }
}
