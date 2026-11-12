// <copyright file="ModuleCoverageDataTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.InteropServices;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Metadata;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class ModuleCoverageDataTests
{
    [Theory]
    [CombinatorialData]
    public void CaptureOnlyReportsExecutableLines(bool callCounts)
    {
        // Lines 1 and 3 are executable; line 2 is not. A probe that kept a stale pointer to a reused buffer can still
        // write the counter of line 2.
        const int lines = 3;
        var coverageMode = callCounts ? 1 : 0;
        var bytesPerLine = callCounts ? sizeof(int) : sizeof(byte);
        var metadata = new TestModuleCoverageMetadata(lines, coverageMode, [new FileCoverageMetadata("/src/capture.cs", 0, lines, [0b1010_0000])]);
        using var value = new ModuleValue(metadata, typeof(ModuleCoverageDataTests).Module, lines * bytesPerLine, ModuleValue.BufferKind.GlobalFallback);
        for (var line = 0; line < lines; line++)
        {
            if (callCounts)
            {
                Marshal.WriteInt32(value.FilesLines, line * sizeof(int), 1);
            }
            else
            {
                Marshal.WriteByte(value.FilesLines, line, 1);
            }
        }

        var data = ModuleCoverageData.Capture(value);

        data.ExecutedBitmaps.Should().ContainSingle().Which.Should().Equal(0b1010_0000);
    }
}
