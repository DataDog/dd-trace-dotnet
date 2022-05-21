// <copyright file="RunHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests
{
    public class RunHelperTests
    {
        [Theory]
        [InlineData("-a:value", "-a:value")]
        [InlineData("-a:value with spaces", "-a:\"value with spaces\"")]
        [InlineData("-a:c:\\temp\\adapter.dll", "-a:c:\\temp\\adapter.dll")]
        [InlineData("-a:c:\\temp folder\\adapter.dll", "-a:\"c:\\temp folder\\adapter.dll\"")]
        [InlineData("-a:/temp/adapter.dll", "-a:/temp/adapter.dll")]
        [InlineData("-a:/temp folder/adapter.dll", "-a:\"/temp folder/adapter.dll\"")]
        [InlineData("-a:/temp_folder/adapter.dll", "-a:/temp_folder/adapter.dll")]
        [InlineData("-a:/temp_folder spaces/adapter.dll", "-a:\"/temp_folder spaces/adapter.dll\"")]
        [InlineData("--adapter:value", "--adapter:value")]
        [InlineData("--adapter:value with spaces", "--adapter:\"value with spaces\"")]
        [InlineData("--adapter:c:\\temp\\adapter.dll", "--adapter:c:\\temp\\adapter.dll")]
        [InlineData("--adapter:c:\\temp folder\\adapter.dll", "--adapter:\"c:\\temp folder\\adapter.dll\"")]
        [InlineData("--adapter:/temp/adapter.dll", "--adapter:/temp/adapter.dll")]
        [InlineData("--adapter:/temp folder/adapter.dll", "--adapter:\"/temp folder/adapter.dll\"")]
        [InlineData("--adapter:/temp_folder/adapter.dll", "--adapter:/temp_folder/adapter.dll")]
        [InlineData("--adapter:/temp_folder spaces/adapter.dll", "--adapter:\"/temp_folder spaces/adapter.dll\"")]
        [InlineData("-a:c:\\temp folder\\adapter.dll -b:c:\\bla bla bla\\bla.dll", "-a:\"c:\\temp folder\\adapter.dll\" -b:\"c:\\bla bla bla\\bla.dll\"")]
        [InlineData("--adapter:/temp_folder spaces/adapter.dll --other:/temp_folder with more spaces/adapter.dll", "--adapter:\"/temp_folder spaces/adapter.dll\" --other:\"/temp_folder with more spaces/adapter.dll\"")]
        [InlineData("-a:c:\\temp folder\\adapter.dll -b:c:\\bla bla bla\\bla.dll -c:c:\\bla2 bla bla\\bla.dll", "-a:\"c:\\temp folder\\adapter.dll\" -b:\"c:\\bla bla bla\\bla.dll\" -c:\"c:\\bla2 bla bla\\bla.dll\"")]
        public void FixDoubleQuotes(string arguments, string expected)
        {
            RunHelper.FixDoubleQuotes(ref arguments);
            Assert.Equal(expected, arguments);
        }
    }
}
