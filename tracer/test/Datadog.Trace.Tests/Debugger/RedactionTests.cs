// <copyright file="RedactionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Debugger.Snapshots;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    [UsesVerify]
    public class RedactionTests
    {
        public static IEnumerable<object[]> GetLongStringTestData()
        {
            var longKeyword = new string('x', 350) + "-api-key";
            var longExcludedKeyword = new string('y', 350) + "-api-key";

            return new List<object[]>
            {
                // Test long keywords
                new object[] { longKeyword, new[] { longKeyword }, false },
                new object[] { longKeyword.ToUpper(), new[] { longKeyword }, false },

                // Test long excluded keywords
                new object[] { "api-key", new[] { longExcludedKeyword }, true },

                // Test mixed lengths
                new object[] { longKeyword, new[] { "short-key" }, true },
                new object[] { "short-key", new[] { longExcludedKeyword }, true }
            };
        }

        public static IEnumerable<object[]> GetSpecialCharTestData()
        {
            return new List<object[]>
            {
                // Special characters in keywords
                new object[] { "test@keyword", Array.Empty<string>(), true },
                new object[] { "test@keyword", new[] { "testkeyword" }, false },
                new object[] { "$special-key", Array.Empty<string>(), true },
                new object[] { "$SPECIAL-KEY", new[] { "specialkey" }, false },

                // Multiple special characters
                new object[] { "@test$keyword@", new[] { "testkeyword" }, false },
                new object[] { "_test@keyword_", new[] { "test@keyword" }, false },

                // Mixed case with special chars
                new object[] { "API@TOKEN", new[] { "apitoken" }, false }
            };
        }

        public static IEnumerable<string> GetLongRedactedIdentifiers()
        {
            // Generate a mix of short and long identifiers
            yield return "api-key";
            yield return new string('x', 350) + "-api-key";
            yield return "short-key";
            yield return new string('y', 256) + "-token";
        }

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
        [InlineData("password", new[] { " " }, true)] // Basic case - no exclusions - empty
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
            Redaction.SetConfig(["password", "x-api-key"], [.. excludedKeywords], new HashSet<string>());

            // Act
            var isRedacted = Redaction.IsRedactedKeyword(keyword);

            // Assert
            Assert.Equal(shouldRedact, isRedacted);
        }

        [Theory]
        [MemberData(nameof(GetLongStringTestData))]
        public void RedactedKeywords_LongStrings_Test(string keyword, string[] excludedKeywords, bool shouldRedact)
        {
            // Arrange
            var redactedIdentifiers = new HashSet<string>(GetLongRedactedIdentifiers());
            Redaction.SetConfig(redactedIdentifiers, [.. excludedKeywords], new HashSet<string>());

            // Act
            var isRedacted = Redaction.IsRedactedKeyword(keyword);

            // Assert
            Assert.Equal(shouldRedact, isRedacted);
        }

        [Theory]
        [MemberData(nameof(GetSpecialCharTestData))]
        public void RedactedKeywords_SpecialChars_Test(string keyword, string[] excludedKeywords, bool shouldRedact)
        {
            // Arrange
            var redactedIdentifiers = new HashSet<string> { "test@keyword", "$special-key", "api@token" };
            Redaction.SetConfig(redactedIdentifiers, [.. excludedKeywords], new HashSet<string>());

            // Act
            var isRedacted = Redaction.IsRedactedKeyword(keyword);

            // Assert
            Assert.Equal(shouldRedact, isRedacted);
        }

        [Theory]
        [InlineData(typeof(string), false)]
        [InlineData(typeof(List<string>), false)]
        [InlineData(null, false)]
        public void IsRedactedType_BasicTypes_Test(Type type, bool expected)
        {
            Assert.Equal(expected, Redaction.IsRedactedType(type));
        }

        [Fact]
        public void IsRedactedType_WithConfiguredTypes_Test()
        {
            // Arrange
            Redaction.SetConfig(
                new HashSet<string>(),
                new HashSet<string>(),
                new HashSet<string> { "System.Security.SecureString", "Namespace.Sensitive*" });

            // Act & Assert
            Assert.True(Redaction.IsRedactedType(typeof(System.Security.SecureString)));
        }

        [Fact]
        public void IsRedactedType_WithWildcardMatch_Test()
        {
            // Arrange
            Redaction.SetConfig(
                new HashSet<string>(),
                new HashSet<string>(),
                new HashSet<string> { "System.Security.*" });

            // Act & Assert
            Assert.True(Redaction.IsRedactedType(typeof(System.Security.SecureString)));
        }

        [Fact]
        public void SetConfig_EmptyConfigurations_Test()
        {
            // Arrange
            var emptySet = new HashSet<string>();

            // Act & Assert (should not throw)
            Redaction.SetConfig(emptySet, emptySet, emptySet);
        }

        [Fact]
        public void SetConfig_DuplicateEntries_Test()
        {
            // Arrange
            var redactedIds = new HashSet<string> { "password", "PASSWORD", "pass-word" };
            var excludedIds = new HashSet<string> { "good-password", "GOOD-PASSWORD" };

            // Act (should not throw)
            Redaction.SetConfig(redactedIds, excludedIds, new HashSet<string>());

            // Assert (all normalized versions should be treated the same)
            Assert.True(Redaction.IsRedactedKeyword("password"));
            Assert.True(Redaction.IsRedactedKeyword("PASSWORD"));
            Assert.False(Redaction.IsRedactedKeyword("good-password"));
            Assert.False(Redaction.IsRedactedKeyword("GOOD-PASSWORD"));
        }

        [Theory]
        [InlineData("", typeof(System.Security.SecureString), RedactionReason.Type)]
        [InlineData("password", typeof(System.Security.SecureString), RedactionReason.Identifier)]
        [InlineData("api_key", typeof(string), RedactionReason.Identifier)]
        [InlineData("normal", typeof(string), RedactionReason.None)]
        internal void ShouldRedact_CombinedScenarios_Test(string name, Type type, RedactionReason expectedReason)
        {
            // Arrange
            Redaction.SetConfig(
                new HashSet<string> { "api_key" },
                new HashSet<string>(),
                new HashSet<string> { "System.Security.SecureString" });

            // Act
            bool result = Redaction.ShouldRedact(name, type, out var reason);

            // Assert
            Assert.Equal(expectedReason, reason);
            Assert.Equal(expectedReason != RedactionReason.None, result);
        }
    }
}
