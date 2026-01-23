// <copyright file="ProcessTagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

public class ProcessTagsTests
{
    [Fact]
    public void TagsPresentWhenEnabled()
    {
        var tags = ProcessTags.SerializedTags;

        tags.Should().ContainAll(ProcessTags.EntrypointBasedir, ProcessTags.EntrypointWorkdir);
        // EntrypointName may not be present, especially when ran in the CI

        tags.Split(',').Should().BeInAscendingOrder();

        // assert on the format, which is key:values in a string, comma separated.
        tags.Split(',')
            .Should()
            .AllSatisfy(s =>
            {
                s.Count(c => c == ':').Should().Be(1);
            });
        // cannot really assert on content because it depends on how the tests are run.
    }

    [Theory]
    [InlineData(@"C:\Users\MyUser\Documents", "Documents")]
    [InlineData(@"C:\Users\MyUser\Documents\", "Documents")]
    [InlineData(@"/home/user/projects", "projects")]
    [InlineData(@"/home/user/projects/", "projects")]
    [InlineData(@"C:\Program Files\", "Program Files")]
    [InlineData(@"/var/log/", "log")]
    [InlineData(@"C:\", @"C:\")]
    [InlineData(@"/", @"/")]
    [InlineData(@"simple", "simple")]
    [InlineData(@"simple/", "simple")]
    [InlineData(@"", "")]
    [InlineData(null, "")]
    public void GetLastPathSegment_ReturnsLastDirectory(string path, string expected)
    {
        ProcessTags.GetLastPathSegment(path).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"C:\Users\MyUser\Documents", @"C:\Users\MyUser\Documents")]
    [InlineData(@"C:\Users\MyUser\Documents\", @"C:\Users\MyUser\Documents")]
    [InlineData(@"/home/user/projects", @"/home/user/projects")]
    [InlineData(@"/home/user/projects/", @"/home/user/projects")]
    [InlineData(@"C:\", @"C:")]
    [InlineData(@"/", @"")]
    [InlineData(@"simple", @"simple")]
    [InlineData(@"simple\", @"simple")]
    [InlineData(@"simple/", @"simple")]
    [InlineData(@"", "")]
    [InlineData(null, "")]
    public void TrimEndingDirectorySeparator_RemovesTrailingSeparator(string path, string expected)
    {
        ProcessTags.TrimEndingDirectorySeparator(path).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"/", true)]
    [InlineData(@"C:\", true)]
    [InlineData(@"c:\", true)]
    [InlineData(@"D:\", true)]
    [InlineData(@"C:/", true)]
    [InlineData(@"C:\Users", false)]
    [InlineData(@"/home", false)]
    [InlineData(@"C:\Program Files", false)]
    [InlineData(@"/var/log", false)]
    [InlineData(@"simple", false)]
    [InlineData(@"", false)]
    [InlineData(null, false)]
    [InlineData(@"C:", false)]
    [InlineData(@"C", false)]
    [InlineData(@"1:\", false)]
    [InlineData(@"CC:\", false)]
    public void IsRootPath_DetectsRootPaths(string path, bool expected)
    {
        ProcessTags.IsRootPath(path).Should().Be(expected);
    }
}
