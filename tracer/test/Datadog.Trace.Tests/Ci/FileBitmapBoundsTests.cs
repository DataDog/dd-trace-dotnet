// <copyright file="FileBitmapBoundsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Coverage.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class FileBitmapBoundsTests
{
    // Line 0 is a valid visible sequence point line, and `git diff -U0` reports `+0,0` when lines are removed at the
    // top of a file; neither has a bit in the one-based bitmap.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(17)]
    [InlineData(int.MaxValue)]
    public unsafe void LinesOutsideTheBitmapAreNotWrittenOrRead(int line)
    {
        // The bitmap covers the first two bytes; the rest of the buffer detects writes past its end.
        var buffer = new byte[64];
        fixed (byte* pointer = buffer)
        {
            var bitmap = new FileBitmap(pointer, 2);
            bitmap.Set(line);

            bitmap.Get(line).Should().BeFalse();
            bitmap.CountActiveBits().Should().Be(0);
        }

        buffer.Should().OnlyContain(value => value == 0);
    }
}
