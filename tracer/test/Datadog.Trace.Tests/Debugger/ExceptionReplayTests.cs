// <copyright file="ExceptionReplayTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class ExceptionReplayTests
{
    [Fact]
    public void InitializeDisablesExceptionReplayWhenMd5IsUnavailable()
    {
        var settings = new ExceptionReplaySettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance);
        var exceptionReplay = ExceptionReplay.Create(settings);

        exceptionReplay.Initialize(() => throw new InvalidOperationException("MD5 is unavailable"));

        settings.CanBeEnabled.Should().BeFalse();
    }
}
