// <copyright file="ContainerMetadataTryGetInodeIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.VersionConflict;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.VersionConflict;

public class ContainerMetadataTryGetInodeIntegrationTests
{
    [Theory]
    [CombinatorialData]
    public void RestoresProcessTracing(bool wasSuppressed, bool returnValue, bool throws)
    {
        var wasTracing = ProcessHelpers.ShouldTraceProcessStart();
        try
        {
            ProcessHelpers.ForceDoNotTrace(wasSuppressed);
            string? path = "/proc/self/ns/cgroup";
            long inode = 0;

            var state = ContainerMetadataTryGetInodeIntegration.OnMethodBegin<object>(ref path, ref inode);
            ProcessHelpers.ShouldTraceProcessStart().Should().BeFalse();

            var exception = throws ? new InvalidOperationException() : null;
            ContainerMetadataTryGetInodeIntegration.OnMethodEnd<object>(returnValue, exception, in state).GetReturnValue().Should().Be(returnValue);
            ProcessHelpers.ShouldTraceProcessStart().Should().Be(!wasSuppressed);
        }
        finally
        {
            ProcessHelpers.ForceDoNotTrace(!wasTracing);
        }
    }
}
