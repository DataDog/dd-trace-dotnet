// <copyright file="RedactionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Snapshots;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    [UsesVerify]
    public class RedactionTests
    {
        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("foobar", false)]
        [InlineData("@-_$", false)]
        [InlineData("password", true)]
        [InlineData("PassWord", true)]
        [InlineData("pass-word", true)]
        [InlineData("_Pass-Word_", true)]
        [InlineData("$pass_worD", true)]
        [InlineData("@passWord@", true)]
        [InlineData(" p@sswOrd ", false)]
        [InlineData("PASSWORD", true)]
        [InlineData("paSS@Word", true)]
        [InlineData("someprefix_password", false)]
        [InlineData("password_suffix", false)]
        [InlineData("some_password_suffix", false)]
        [InlineData("Password!", false)]
        [InlineData("!Password", false)]
        public void RedactedKeywordsTest(string keyword, bool shouldYield)
        {
            Assert.Equal(shouldYield, Redaction.IsRedactedKeyword(keyword));
        }

        [Theory]
        [InlineData("x-api-key", true)]
        [InlineData("x_api_key", true)]
        [InlineData("xapikey", true)]
        [InlineData("XApiKey", true)]
        [InlineData("X_Api-Key", true)]
        [InlineData("x_key", false)]
        public void ShouldRedactKeywordsTest(string keyword, bool shouldRedacted)
        {
            Assert.Equal(shouldRedacted, Redaction.ShouldRedact(keyword, typeof(string), out _));
        }

        [Theory]
        [InlineData("password", null, true)] // Basic case - no exclusions - null
        [InlineData("password", new string[] { " " }, true)] // Basic case - no exclusions - empty
        [InlineData("password", new[] { "otherword" }, true)] // Exclusion list doesn't affect non-excluded word
        [InlineData("password", new[] { "password" }, false)] // Basic exclusion
        [InlineData("PassWord", new[] { "password" }, false)] // Case-insensitive exclusion
        [InlineData("pass-word", new[] { "password" }, false)] // Normalized form exclusion
        [InlineData("_Pass-Word_", new[] { "password" }, false)] // Complex normalization exclusion
        [InlineData("password", new[] { "_Pass-Word_" }, false)] // Complex normalization exclusion
        [InlineData("_Pass-Word_", new[] { "_Pass-Word_" }, false)] // Complex normalization exclusion
        [InlineData("password", new[] { "pass" }, true)] // Partial match shouldn't exclude
        [InlineData("x-api-key", new[] { "password" }, true)] // Different keyword not affected by exclusion
        [InlineData("x-api-key", new[] { "x-api-key" }, false)] // Exclude specific API keyword
        public void RedactedKeywords_WithExclusions_Test(string keyword, string[] excludedKeywords, bool shouldRedact)
        {
            // Arrange
            var settings = new DebuggerSettings(
                new NameValueConfigurationSource(new()
                                                 {
                                                     { ConfigurationKeys.Debugger.RedactedIdentifiers, "password,x-api-key" },
                                                     { ConfigurationKeys.Debugger.RedactedExcludedIdentifiers, excludedKeywords?[0] }
                                                 }),
                NullConfigurationTelemetry.Instance);

            Redaction.SetConfig(settings);

            // Act
            var isRedacted = Redaction.IsRedactedKeyword(keyword);

            // Assert
            Assert.Equal(shouldRedact, isRedacted);
        }
    }
}
