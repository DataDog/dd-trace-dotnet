// <copyright file="ExceptionTrackManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class ExceptionTrackManagerTests
    {
        [Fact]
        public void IsSupportedExceptionType_RejectsStackOverflowException()
        {
            var settings = new ExceptionReplaySettings(
                new NameValueConfigurationSource(new NameValueCollection()),
                NullConfigurationTelemetry.Instance);

            using var manager = ExceptionTrackManager.Create(settings);

            manager.IsSupportedExceptionType(typeof(StackOverflowException)).Should().BeFalse();
            manager.IsSupportedExceptionType(typeof(InvalidOperationException)).Should().BeTrue();
        }
    }
}
