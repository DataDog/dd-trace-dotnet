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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TagsPresentWhenServiceNameUserDefined(bool isServiceNameUserDefined)
    {
        var processTags = new ProcessTags(serviceNameUserDefined: isServiceNameUserDefined, autoServiceName: "auto-service");
        var tags = processTags.SerializedTags;

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

        if (isServiceNameUserDefined)
        {
            tags.Should().Contain("svc.user:true");
            tags.Should().NotContain("svc.auto");
        }
        else
        {
            tags.Should().NotContain("svc.user");
            tags.Should().Contain("svc.auto:auto-service");
        }

        // cannot really assert the rest of the content because it depends on how the tests are run.
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

    [Theory]
    [InlineData("#test_starting_hash", "test_starting_hash")]
    [InlineData("TestCAPSandSuch", "testcapsandsuch")]
    [InlineData("Test Conversion Of Weird !@#$%^&**() Characters", "test_conversion_of_weird_characters")]
    [InlineData("$#weird_starting", "weird_starting")]
    [InlineData("disallowed:c0l0ns", "disallowed_c0l0ns")]
    [InlineData("1love", "love")]
    [InlineData("123456", "")]
    [InlineData("7.0", "")] // this is not ideal
    [InlineData("ünicöde", "ünicöde")]
    [InlineData("ünicöde:metäl", "ünicöde_metäl")]
    [InlineData("Data🐨dog🐶 繋がっ⛰てて", "data_dog_繋がっ_てて")]
    [InlineData(" spaces   ", "spaces")]
    [InlineData(" #hashtag!@#spaces #__<>#  ", "hashtag_spaces")]
    [InlineData(":testing", "testing")]
    [InlineData("_foo", "foo")]
    [InlineData(":::test", "test")]
    [InlineData("te::st", "te_st")]
    [InlineData("te:_st", "te_st")]
    [InlineData("te:🐨st", "te_st")]
    [InlineData("contiguous_____underscores", "contiguous_underscores")]
    [InlineData("foo_", "foo")]
    [InlineData("", "")]
    [InlineData(" ", "")]
    [InlineData("ok", "ok")]
    [InlineData("AlsO:ök", "also_ök")]
    [InlineData(":still_ok", "still_ok")]
    [InlineData("___trim", "trim")]
    [InlineData("fun:ky__tag/1", "fun_ky_tag/1")]
    [InlineData("fun:ky@tag/2", "fun_ky_tag/2")]
    [InlineData("fun:ky@@@tag/3", "fun_ky_tag/3")]
    [InlineData("tag:1/2.3", "tag_1/2.3")]
    [InlineData("---fun:k####y_ta@#g/1_@@#", "fun_k_y_ta_g/1")]
    [InlineData("AlsO:œ#@ö))œk", "also_œ_ö_œk")]
    [InlineData("test\x99\x008faaa", "test_aaa")]
    [InlineData("test\x99\x8f", "test")]
    [InlineData(" regulartag ", "regulartag")]
    [InlineData("\u017Fodd_\u017Fcase\u017F", "\u017Fodd_\u017Fcase\u017F")]
    [InlineData("™Ö™Ö™™Ö™", "ö_ö_ö")]
    [InlineData("a�", "a")]
    [InlineData("a��", "a")]
    [InlineData("a��b", "a_b")]
    public void TestNormalization(string tagValue, string expectedValue)
    {
        ProcessTags.NormalizeTagValue(tagValue).Should().Be(expectedValue);
    }

    [Fact]
    public void TestNormalizationsTruncation()
    {
        // cannot write those as `Theory` because the parameters need to be constant values
        var tagValue = new string(c: 'a', count: 888);
        var expected = new string(c: 'a', count: 100);
        ProcessTags.NormalizeTagValue(tagValue).Should().Be(expected);

        tagValue = "a" + new string(c: '➰', count: 799);
        expected = "a";
        ProcessTags.NormalizeTagValue(tagValue).Should().Be(expected);
    }
}
