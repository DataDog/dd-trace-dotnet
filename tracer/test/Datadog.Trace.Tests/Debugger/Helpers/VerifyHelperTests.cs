// <copyright file="VerifyHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.Helpers;

public class VerifyHelperTests
{
    [Theory]
    [InlineData(@"_dd.code_origin.frames.0.file: C:\work\dd-trace-dotnet\tracer\test\test-applications\integrations\Samples.AspNetCoreMvc21\Controllers\HomeController.cs,", @"_dd.code_origin.frames.0.file: tracer\test\test-applications\integrations\Samples.AspNetCoreMvc21\Controllers\HomeController.cs,")]
    [InlineData("_dd.code_origin.frames.0.file: /home/runner/work/dd-trace-dotnet/tracer/test/test-applications/integrations/Samples.AspNetCoreMvc21/Controllers/HomeController.cs,", @"_dd.code_origin.frames.0.file: tracer\test\test-applications\integrations\Samples.AspNetCoreMvc21\Controllers\HomeController.cs,")]
    [InlineData("_dd.code_origin.frames.0.file: tracer/test/test-applications/integrations/Samples.AspNetCoreMvc21/Controllers/HomeController.cs,", @"_dd.code_origin.frames.0.file: tracer\test\test-applications\integrations\Samples.AspNetCoreMvc21\Controllers\HomeController.cs,")]
    public void NormalizeCodeOriginFilePaths_UsesRepoRelativeWindowsStylePaths(string input, string expected)
    {
        VerifyHelper.NormalizeCodeOriginFilePaths(input).Should().Be(expected);
    }
}
