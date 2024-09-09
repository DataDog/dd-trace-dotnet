// <copyright file="StackReporterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Datadog.Trace.AppSec.Rasp;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.RASP;

public class StackReporterTests
{
    [Fact]
    public void GivenMaxDepthMoreThanTotalFrames_WhenGetStackIsCalled_ThenReturnsAllFrames()
    {
        int maxStackTraceDepth = 10;
        var mockFrames = CreateStackForTests(2);
        var result = StackReporter.GetStack(maxStackTraceDepth, 100, "test", mockFrames);

        Assert.NotNull(result);
        Assert.True(result.ContainsKey("frames"));

        var frames = result["frames"] as List<Dictionary<string, object>>;
        Assert.NotNull(frames);
        Assert.Equal(mockFrames.Length, frames.Count);
        Assert.Equal("file1.cs", frames[0]["file"]);
        Assert.Equal("file2.cs", frames[1]["file"]);
    }

    [Fact]
    public void GivenMultipleFrames_WhenMaxDepthIsSet_ThenReturnsTop75AndBottom25Percent()
    {
        int maxStackTraceDepth = 2;
        var mockFrames = CreateStackForTests(4);
        var result = StackReporter.GetStack(maxStackTraceDepth, 75, "test", mockFrames);
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("frames"));

        var frames = result["frames"] as List<Dictionary<string, object>>;
        Assert.NotNull(frames);
        Assert.Equal(maxStackTraceDepth, frames.Count);
        Assert.Equal("file1.cs", frames[0]["file"]);
        Assert.Equal("file4.cs", frames[1]["file"]);
    }

    [Fact]
    public void GivenMultipleFrames_WhenMaxDepthIsSetTo4_ThenReturnsTop75AndBottom25Percent()
    {
        int maxStackTraceDepth = 4;
        var mockFrames = CreateStackForTests(40);
        var result = StackReporter.GetStack(maxStackTraceDepth, 75, "test", mockFrames);
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("frames"));

        var frames = result["frames"] as List<Dictionary<string, object>>;
        Assert.NotNull(frames);
        Assert.Equal(maxStackTraceDepth, frames.Count);
        Assert.Equal("file1.cs", frames[0]["file"]);
        Assert.Equal("file2.cs", frames[1]["file"]);
        Assert.Equal("file3.cs", frames[2]["file"]);
        Assert.Equal("file40.cs", frames[3]["file"]);
    }

    [Fact]
    public void GivenMultipleFrames_WhenMaxDepthIsSetTo0_ThenAllFramesAreReturned()
    {
        var mockFrames = CreateStackForTests(40);

        var result = StackReporter.GetStack(0, 0, "test", mockFrames);
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("frames"));

        (result["frames"] as List<Dictionary<string, object>>).Count.Should().Be(40);
    }

    [Fact]
    public void GivenNoStackFrames_WhenGetStackIsCalled_ThenReturnsNull()
    {
        StackFrame[] mockFrames = [];
        var result = StackReporter.GetStack(5, 100, "test", mockFrames);
        Assert.Null(result);
    }

    private StackFrame[] CreateStackForTests(int numberOfElements)
    {
        StackFrame[] mockFrames = new StackFrame[numberOfElements];
        for (int i = 1; i <= numberOfElements; i++)
        {
            mockFrames[i - 1] = StackFrameCreate($"file{i}.cs", i, 1);
        }

        return mockFrames;
    }

    private StackFrame StackFrameCreate(string file, int line, int column)
    {
        var frame = new StackFrameForTest(file, line, column);
        return frame;
    }

    public class StackFrameForTest : StackFrame
    {
        public StackFrameForTest(string fileName, int lineNumber, int columnNumber)
            : base(fileName, lineNumber, columnNumber)
        {
        }

        public override MethodBase GetMethod()
        {
            return typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string) });
        }
    }
}
