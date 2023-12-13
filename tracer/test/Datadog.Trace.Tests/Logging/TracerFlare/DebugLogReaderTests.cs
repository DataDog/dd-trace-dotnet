// <copyright file="DebugLogReaderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Logging.TracerFlare;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging.TracerFlare;

public class DebugLogReaderTests
{
    [Fact]
    public void TryToCreateSentinelFile_CreateSentinelInExistingDirectory_Succeeds()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);

        var id = Guid.NewGuid().ToString();
        var result = DebugLogReader.TryToCreateSentinelFile(directory, id);
        result.Should().BeTrue();
    }

    [Fact]
    public void TryToCreateSentinelFile_CreateSecondSentinelInExistingDirectory_Succeeds()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);

        var id1 = Guid.NewGuid().ToString();
        var id2 = Guid.NewGuid().ToString();

        var result1 = DebugLogReader.TryToCreateSentinelFile(directory, id1);
        var result2 = DebugLogReader.TryToCreateSentinelFile(directory, id2);

        result1.Should().BeTrue();
        result2.Should().BeTrue();
    }

    [Fact]
    public void TryToCreateSentinelFile_CreateSameSentinel_Fails()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);

        var id = Guid.NewGuid().ToString();

        var result1 = DebugLogReader.TryToCreateSentinelFile(directory, id);
        var result2 = DebugLogReader.TryToCreateSentinelFile(directory, id);

        result1.Should().BeTrue();
        result2.Should().BeFalse();
    }

    [Fact]
    public void TryToCreateSentinelFile_CreateSentinelInNonExistingDirectory_Fails()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var id = Guid.NewGuid().ToString();

        var result = DebugLogReader.TryToCreateSentinelFile(directory, id);

        result.Should().BeFalse();
    }
}
