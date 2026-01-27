// <copyright file="ProcessTagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.TestHelpers;
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

    [SkippableTheory]
    [InlineData(@"C:\Users\MyUser\Documents", "Documents")]  // no trailing separator
    [InlineData(@"C:\Users\MyUser\Documents\", "Documents")] // with trailing separator
    [InlineData(@"C:\Program Files\", "Program Files")]      // with space
    [InlineData(@"simple", "simple")]                        // not rooted, no trailing separator
    [InlineData(@"simple/", "simple")]                       // not rooted, with trailing separator
    [InlineData(@"C:\", @"C:\")]                             // root
    [InlineData(@"", "")]                                    // empty
    [InlineData(null, "")]                                   // null
    public void GetLastPathSegment_ReturnsLastDirectory_Windows(string path, string expected)
    {
        // run these on Windows only
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

        ProcessTags.GetLastPathSegment(path).Should().Be(expected);
    }

    [SkippableTheory]
    [InlineData(@"/home/user/projects", "projects")]  // no trailing separator
    [InlineData(@"/home/user/projects/", "projects")] // with trailing separator
    [InlineData(@"simple", "simple")]                 // not rooted, no trailing separator
    [InlineData(@"simple/", "simple")]                // not rooted, with trailing separator
    [InlineData(@"/", @"/")]                          // root
    [InlineData(@"", "")]                             // empty
    [InlineData(null, "")]                            // null
    public void GetLastPathSegment_ReturnsLastDirectory_NonWindows(string path, string expected)
    {
        // do not run these on Windows
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        ProcessTags.GetLastPathSegment(path).Should().Be(expected);
    }

    [SkippableTheory]
    [InlineData(@"C:\", true)]               // root, common case
    [InlineData(@"c:\", true)]               // root, lower-case
    [InlineData(@"D:\", true)]               // root, different drive
    [InlineData(@"C:\Users", false)]         // not root, single path segment
    [InlineData(@"C:\Users\foo", false)]     // not root, multiple path segments
    [InlineData(@"C:\Program Files", false)] // not root, with space
    [InlineData(@"C:", false)]               // not root
    [InlineData(@"simple", false)]           // not rooted
    [InlineData(@"C", false)]                // not rooted (single character, looks like a drive letter)
    [InlineData(@"1:\", false)]              // invalid drive
    [InlineData(@"CC:\", false)]             // invalid drive
    [InlineData(@"", false)]                 // empty
    [InlineData(null, false)]                // null
    public void IsRootPath_DetectsRootPaths_Windows(string path, bool expected)
    {
        // run these on Windows only
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

        ProcessTags.IsRootPath(path).Should().Be(expected);
    }

    [SkippableTheory]
    [InlineData(@"/", true)]         // root
    [InlineData(@"/home", false)]    // not root, single path segment
    [InlineData(@"/var/log", false)] // not root, multiple path segments
    [InlineData(@"simple", false)]   // not rooted
    [InlineData(@"", false)]         // empty
    [InlineData(null, false)]        // null
    public void IsRootPath_DetectsRootPaths_NonWindows(string path, bool expected)
    {
        // do not run these on Windows
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        ProcessTags.IsRootPath(path).Should().Be(expected);
    }
}
