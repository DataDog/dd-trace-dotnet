// <copyright file="ProcessStartCommandsCollectionUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.Process;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class ProcessStartCommandsCollectionUnitTests
    {
        [Theory]
        [InlineData("cmd.exe", "", new[] { "cmd.exe" })]
        [InlineData("cmd.exe", "arg1", new[] { "cmd.exe", "arg1" })]
        [InlineData("cmd.exe", " /c \"echo hello\"", new[] { "cmd.exe", "/c", "echo hello" })]
        [InlineData("cmd.exe", " /c arg1 arg2 arg3", new[] { "cmd.exe", "/c", "arg1", "arg2", "arg3" })]
        [InlineData("cmd.exe", " /c \"echo hello\" \"echo world\"", new[] { "cmd.exe", "/c", "echo hello", "echo world" })]
        [InlineData("cmd.exe", " /c 'echo hello'", new[] { "cmd.exe", "/c", "echo hello" })]
        [InlineData("bin.exe", "'arg1' arg2 arg3 \"arg4\" arg5 a'r'g6", new[] { "bin.exe", "arg1", "arg2", "arg3", "arg4", "arg5", "arg6" })]
        [InlineData("bin.exe", "'h''e'llo", new[] { "bin.exe", "hello" })]
        [InlineData("bin.exe", "\"'h''e'llo\"", new[] { "bin.exe", "'h''e'llo" })]
        [InlineData("bin.exe", "arg1 \"\"hey\"\" arg2", new[] { "bin.exe", "arg1", "hey", "arg2" })]
        [InlineData("bin.exe", "\"\\\"h'e'y\\\"\"", new[] { "bin.exe", "\"h'e'y\"" })]
        [InlineData("bin.exe", "\"\\\"h'e'y\\\"\" arg1", new[] { "bin.exe", "\"h'e'y\"", "arg1" })]
        [InlineData("bin.exe", "'he'l\\'l'o'\"o\"", new[] { "bin.exe", "hel'loo" })]
        [InlineData("bin.exe", "--pass=1 --token=XXX --user root doAction --printFormat \"%s/%s/%s '%d' - %x\"", new[] { "bin.exe", "--pass=1", "--token=XXX", "--user", "root", "doAction", "--printFormat", "%s/%s/%s '%d' - %x" })]
        [InlineData("bin.exe", "type json \"{\\\"root\\\":\\\"ok\\\",\\\"number\\\":200}\\\"", new[] { "bin.exe", "type", "json", "{\"root\":\"ok\",\"number\":200}\"" })]
        // Weird quotes
        [InlineData("bin.exe", "\"", new[] { "bin.exe" })]
        [InlineData("bin.exe", "\"\\\\\"", new[] { "bin.exe", "\\" })]
        [InlineData("bin.exe", "\"\\\\\"\"", new[] { "bin.exe", "\\" })]
        [InlineData("bin.exe", "arg1 'arg2", new[] { "bin.exe", "arg1", "arg2" })]
        [InlineData("bin.exe", "arg1 'arg2 ", new[] { "bin.exe", "arg1", "arg2 " })]
        [InlineData("bin.exe", "arg1 \"arg2", new[] { "bin.exe", "arg1", "arg2" })]
        [InlineData("bin.exe", "arg1 \"arg2 ", new[] { "bin.exe", "arg1", "arg2 " })]
        [InlineData("bin.exe", "arg1 arg2\" \"", new[] { "bin.exe", "arg1", "arg2 " })]
        [InlineData("bin.exe", "arg1 arg2\" ", new[] { "bin.exe", "arg1", "arg2 " })]
        // Truncated
        [InlineData("bin.exe", "all arguments are less than 100 chars no truncate", new[] { "bin.exe", "all", "arguments", "are", "less", "than", "100", "chars", "no", "truncate" }, false, 100)]
        [InlineData("bin.exe", "a big aaarrrggguuummmeeennnttt", new[] { "bin.exe", "a", "big", "aaarrrggg" }, true, 20)]
        [InlineData("bin.exe", "a big aaarrrggguuummmeeennnttt and other args", new[] { "bin.exe", "a", "big", "aaarrrggg" }, true, 20)]
        [InlineData("bin.exe", "a big aaarrrggguuummmeeennnttt and other args", new[] { "bin.exe" }, true, 1)]
        [InlineData("bin.exe", "a big 'aaarrrggguuummmeeennnttt' 'and' other 'args'", new[] { "bin.exe", "a", "big", "aaarrrggguuummmeeennnttt", "and", "other", "args" }, false, 47)]
        [InlineData("bin.exe", "a big 'aaarrrggguuummmeeennnttt and other args", new[] { "bin.exe", "a", "big", "aaarrrggguuummmeeennnttt and " }, true, 40)]
        public void Test_SplitStringsIntoArguments(string filename, string arguments, string[] expected, bool expectedTruncated = false, int maxLength = ProcessStartCommon.MaxCommandLineLength)
        {
            var maxCommandlineLength = maxLength - filename.Length;
            var result = ProcessStartCommon.SplitStringIntoArguments(arguments, maxCommandlineLength, out var truncated);
            result.Insert(0, filename);
            result.Should().BeEquivalentTo(expected);
            truncated.Should().Be(expectedTruncated);
        }
    }
}
