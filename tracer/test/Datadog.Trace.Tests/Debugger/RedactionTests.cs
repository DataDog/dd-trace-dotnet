// <copyright file="RedactionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Security;
using System.Text;
using Datadog.Trace.Debugger.Snapshots;
using FluentAssertions;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    [CollectionDefinition(nameof(RedactionTests), DisableParallelization = true)]
    [Collection(nameof(RedactionTests))]
    [UsesVerify]
    public class RedactionTests : IDisposable
    {
        public RedactionTests()
        {
            Redaction.Instance.ResetInstance();
        }

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

        public void Dispose()
        {
            Redaction.Instance.ResetInstance();
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
            Assert.Equal(shouldYield, Redaction.Instance.IsRedactedKeyword(keyword));
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
            Assert.Equal(shouldRedacted, Redaction.Instance.ShouldRedact(keyword, typeof(string), out _));
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
            Redaction.Instance.SetConfig(["password", "x-api-key"], [.. excludedKeywords], new HashSet<string>());

            // Act
            var isRedacted = Redaction.Instance.IsRedactedKeyword(keyword);

            // Assert
            Assert.Equal(shouldRedact, isRedacted);
        }

        [Theory]
        [MemberData(nameof(GetLongStringTestData))]
        public void RedactedKeywords_LongStrings_Test(string keyword, string[] excludedKeywords, bool shouldRedact)
        {
            // Arrange
            var redactedIdentifiers = new HashSet<string>(GetLongRedactedIdentifiers());
            Redaction.Instance.SetConfig(redactedIdentifiers, [.. excludedKeywords], new HashSet<string>());

            // Act
            var isRedacted = Redaction.Instance.IsRedactedKeyword(keyword);

            // Assert
            Assert.Equal(shouldRedact, isRedacted);
        }

        [Theory]
        [MemberData(nameof(GetSpecialCharTestData))]
        public void RedactedKeywords_SpecialChars_Test(string keyword, string[] excludedKeywords, bool shouldRedact)
        {
            // Arrange
            var redactedIdentifiers = new HashSet<string> { "test@keyword", "$special-key", "api@token" };
            Redaction.Instance.SetConfig(redactedIdentifiers, [.. excludedKeywords], new HashSet<string>());

            // Act
            var isRedacted = Redaction.Instance.IsRedactedKeyword(keyword);

            // Assert
            Assert.Equal(shouldRedact, isRedacted);
        }

        [Theory]
        [InlineData(256)] // Largest stack buffer
        [InlineData(257)] // Smallest pooled buffer
        public void IsRedactedKeyword_NormalizesAroundStackAllocLimit(int normalizedLength)
        {
            // Mixed case and separators force normalization, which must produce exactly the configured keyword.
            var keyword = new StringBuilder(normalizedLength);
            var name = new StringBuilder();
            for (var i = 0; i < normalizedLength; i++)
            {
                var c = (char)('a' + (i % 26));
                keyword.Append(c);
                name.Append(i % 2 == 0 ? char.ToUpperInvariant(c) : c);
                if (i % 10 == 0)
                {
                    name.Append('_');
                }
            }

            Redaction.Instance.SetConfig([keyword.ToString()], [], []);

            Redaction.Instance.IsRedactedKeyword(name.ToString()).Should().BeTrue();
            Redaction.Instance.IsRedactedKeyword(name.ToString(1, name.Length - 1)).Should().BeFalse();
        }

        [Theory]
        [InlineData(typeof(string), false)]
        [InlineData(typeof(List<string>), false)]
        [InlineData(null, false)]
        public void IsRedactedType_BasicTypes_Test(Type type, bool expected)
        {
            Assert.Equal(expected, Redaction.Instance.IsRedactedType(type));
        }

        [Theory]
        [InlineData(null, typeof(object), true)]
        [InlineData(null, typeof(IDisposable), true)]
        [InlineData(null, typeof(System.Collections.IEnumerable), false)]
        [InlineData(null, typeof(Exception), false)]
        [InlineData(null, typeof(object[]), false)]
        [InlineData(null, typeof(int), false)]
        [InlineData("Namespace.Sensitive*", typeof(Exception), true)]
        [InlineData("Namespace.Sensitive*", typeof(System.Collections.IEnumerable), true)]
        [InlineData("Namespace.Sensitive*", typeof(Exception[]), true)]
        [InlineData("Namespace.Sensitive*", typeof(object[][]), true)]
        [InlineData("Namespace.Sensitive*", typeof(string[]), false)]
        [InlineData("Namespace.Sensitive*", typeof(int[][]), false)]
        [InlineData("Namespace.Sensitive*", typeof(string), false)]
        [InlineData("Namespace.Sensitive*", typeof(int), false)]
        [InlineData("Namespace.Sensitive*", typeof(int?), false)]
        public void CanHaveRedactedRuntimeType_Test(string redactedType, Type declaredType, bool expected)
        {
            if (redactedType is not null)
            {
                Redaction.Instance.SetConfig(new HashSet<string>(), new HashSet<string>(), new HashSet<string> { redactedType });
            }

            Assert.Equal(expected, Redaction.Instance.CanHaveRedactedRuntimeType(declaredType));
        }

        [Theory]
        [InlineData(null, typeof(System.Security.SecureString), true)]
        [InlineData(null, typeof(string), false)]
        [InlineData("System.Security.*", typeof(System.Security.SecureString), true)]
        [InlineData("Namespace.Sensitive*", typeof(string), false)]
        public void IsRedactedRuntimeType_Test(string redactedType, Type runtimeType, bool expected)
        {
            if (redactedType is not null)
            {
                Redaction.Instance.SetConfig(new HashSet<string>(), new HashSet<string>(), new HashSet<string> { redactedType });
            }

            Assert.Equal(expected, Redaction.Instance.IsRedactedRuntimeType(runtimeType));
        }

        [Fact]
        public void IsRedactedType_WithConfiguredTypes_Test()
        {
            // Arrange
            Redaction.Instance.SetConfig(
                new HashSet<string>(),
                new HashSet<string>(),
                new HashSet<string> { "System.Security.SecureString", "Namespace.Sensitive*" });

            // Act & Assert
            Redaction.Instance.IsRedactedType(typeof(SecureString)).Should().BeTrue();
        }

        [Fact]
        public void IsRedactedType_WithConfiguredUnderlyingNullableType_Test()
        {
            // Arrange
            Redaction.Instance.SetConfig(
                new HashSet<string>(),
                new HashSet<string>(),
                new HashSet<string> { "System.Guid" });

            // Act & Assert
            Assert.True(Redaction.Instance.IsRedactedType(typeof(Guid?)));
        }

        [Fact]
        public void IsRedactedType_WithConfiguredNullableWrapperType_Test()
        {
            // Arrange
            Redaction.Instance.SetConfig(
                new HashSet<string>(),
                new HashSet<string>(),
                new HashSet<string> { "System.Nullable*" });

            // Act & Assert
            Assert.True(Redaction.Instance.IsRedactedType(typeof(Guid?)));
        }

        [Fact]
        public void IsRedactedType_WithWildcardMatch_Test()
        {
            // Arrange
            Redaction.Instance.SetConfig(
                new HashSet<string>(),
                new HashSet<string>(),
                new HashSet<string> { "System.Security.*" });

            // Act & Assert
            Redaction.Instance.IsRedactedType(typeof(SecureString)).Should().BeTrue();
        }

        [Fact]
        public void SetConfig_EmptyConfigurations_Test()
        {
            // Arrange
            var emptySet = new HashSet<string>();

            // Act & Assert (should not throw)
            Redaction.Instance.SetConfig(emptySet, emptySet, emptySet);
        }

        [Fact]
        public void SetConfig_DuplicateEntries_Test()
        {
            // Arrange
            var redactedIds = new HashSet<string> { "password", "PASSWORD", "pass-word" };
            var excludedIds = new HashSet<string> { "good-password", "GOOD-PASSWORD" };

            // Act (should not throw)
            Redaction.Instance.SetConfig(redactedIds, excludedIds, new HashSet<string>());

            // Assert (all normalized versions should be treated the same)
            Assert.True(Redaction.Instance.IsRedactedKeyword("password"));
            Assert.True(Redaction.Instance.IsRedactedKeyword("PASSWORD"));
            Assert.False(Redaction.Instance.IsRedactedKeyword("good-password"));
            Assert.False(Redaction.Instance.IsRedactedKeyword("GOOD-PASSWORD"));
        }

        [Fact]
        public void IsRedactedKeyword_WhenCacheIsFull_StartsNewCache()
        {
            var redaction = Redaction.Instance;
            for (var i = 0; i < Redaction.MaxCachedKeywords; i++)
            {
                redaction.IsRedactedKeyword("field" + i).Should().BeFalse();
            }

            redaction.CachedKeywordsCount.Should().Be(Redaction.MaxCachedKeywords);

            redaction.IsRedactedKeyword("Pass_Word").Should().BeTrue();
            redaction.CachedKeywordsCount.Should().Be(1);

            redaction.IsRedactedKeyword("Pass_Word").Should().BeTrue();
            redaction.IsRedactedKeyword("field0").Should().BeFalse();
            redaction.ShouldRedact("x-api-key", typeof(string), out var reason).Should().BeTrue();
            reason.Should().Be(RedactionReason.Identifier);
            redaction.CachedKeywordsCount.Should().Be(3);
        }

        [Fact]
        public void IsRedactedKeyword_LongName_IsCheckedButNotCached()
        {
            var redaction = Redaction.Instance;

            redaction.IsRedactedKeyword("password" + new string('_', 600)).Should().BeTrue();
            redaction.IsRedactedKeyword(new string('a', 600)).Should().BeFalse();
            redaction.IsRedactedDataKey("Pass-Word" + new string('-', 600)).Should().BeTrue();
            redaction.CachedKeywordsCount.Should().Be(0);
            redaction.CachedDataKeysCount.Should().Be(0);
        }

        [Theory]
        [InlineData(512, 1)]
        [InlineData(513, 0)]
        public void IsRedactedKeyword_NameLength_IsCachedOnlyUpToTheLimit(int length, int expectedCachedCount)
        {
            var redaction = Redaction.Instance;

            redaction.IsRedactedKeyword(new string('a', length)).Should().BeFalse();

            redaction.CachedKeywordsCount.Should().Be(expectedCachedCount);
        }

#if NETCOREAPP3_1_OR_GREATER
        [Fact]
        public void CacheHits_DoNotAllocate()
        {
            var redaction = Redaction.Instance;
            const int Iterations = 1000;

            // The first calls fill the caches. Only the following hits are measured.
            for (var i = 0; i < 2; i++)
            {
                redaction.ShouldRedact("customerName", typeof(Uri), out _);
                redaction.ShouldRedactDataKey("customerKey", typeof(Uri), out _);
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < Iterations; i++)
            {
                redaction.ShouldRedact("customerName", typeof(Uri), out _);
                redaction.ShouldRedactDataKey("customerKey", typeof(Uri), out _);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            allocated.Should().Be(0);
        }
#endif

        [Fact]
        public void SetConfig_IdentifierLongerThanStackBuffer_IsRedacted()
        {
            var identifier = new string('x', 600) + "-api-key";

            Redaction.Instance.SetConfig([identifier], [], []);

            Redaction.Instance.IsRedactedKeyword(identifier).Should().BeTrue();
            Redaction.Instance.IsRedactedKeyword(identifier.ToUpperInvariant()).Should().BeTrue();
            Redaction.Instance.IsRedactedKeyword(identifier + "x").Should().BeFalse();
        }

        [Fact]
        public void IsRedactedDataKey_UsesSeparateCache()
        {
            var redaction = Redaction.Instance;
            redaction.IsRedactedKeyword("customerName").Should().BeFalse();

            for (var i = 0; i < Redaction.MaxCachedDataKeys; i++)
            {
                redaction.IsRedactedDataKey("key" + i).Should().BeFalse();
            }

            redaction.CachedDataKeysCount.Should().Be(Redaction.MaxCachedDataKeys);
            redaction.CachedKeywordsCount.Should().Be(1);

            redaction.ShouldRedactDataKey("Pass_Word", typeof(string), out var reason).Should().BeTrue();
            reason.Should().Be(RedactionReason.Identifier);
            redaction.CachedDataKeysCount.Should().Be(1);
            redaction.CachedKeywordsCount.Should().Be(1);
        }

#if NETCOREAPP3_1_OR_GREATER
        [Fact]
        public void IsRedactedType_CollectibleType_IsNotCached()
        {
            var redaction = Redaction.Instance;
            redaction.SetConfig([], [], ["Collectible.SecretHolder"]);
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("RedactionCollectibleTypeTests"), AssemblyBuilderAccess.RunAndCollect);
            var type = assembly.DefineDynamicModule("Main").DefineType("Collectible.SecretHolder", TypeAttributes.Public).CreateType();

            type.IsCollectible.Should().BeTrue();
            redaction.IsRedactedType(type).Should().BeTrue();
            redaction.IsRedactedType(type).Should().BeTrue();
            redaction.CachedTypesCount.Should().Be(0);
        }
#endif

        [Fact]
        public void IsRedactedType_CachesResult()
        {
            var redaction = Redaction.Instance;

            redaction.IsRedactedType(typeof(SecureString)).Should().BeTrue();
            redaction.IsRedactedType(typeof(SecureString)).Should().BeTrue();
            redaction.IsRedactedType(typeof(Uri)).Should().BeFalse();
            redaction.CachedTypesCount.Should().Be(2);
        }

        [Fact]
        public void SetConfig_ClearsCachedKeywordResults()
        {
            var redaction = Redaction.Instance;
            redaction.IsRedactedKeyword("customerEmail").Should().BeFalse();
            redaction.IsRedactedKeyword("password").Should().BeTrue();
            redaction.IsRedactedDataKey("customerEmail").Should().BeFalse();
            redaction.IsRedactedDataKey("password").Should().BeTrue();

            redaction.SetConfig(["customer_email"], ["password"], []);

            redaction.IsRedactedKeyword("customerEmail").Should().BeTrue();
            redaction.IsRedactedKeyword("password").Should().BeFalse();
            redaction.IsRedactedDataKey("customerEmail").Should().BeTrue();
            redaction.IsRedactedDataKey("password").Should().BeFalse();
        }

        [Fact]
        public void SetConfig_ClearsCachedTypeResults()
        {
            var redaction = Redaction.Instance;
            redaction.IsRedactedType(typeof(Uri)).Should().BeFalse();
            redaction.IsRedactedType(typeof(Version)).Should().BeFalse();

            redaction.SetConfig([], [], ["System.Uri", "System.Version"]);

            redaction.IsRedactedType(typeof(Uri)).Should().BeTrue();
            redaction.IsRedactedType(typeof(Version)).Should().BeTrue();
            redaction.ShouldRedact("endpoint", typeof(Uri), out var reason).Should().BeTrue();
            reason.Should().Be(RedactionReason.Type);
        }

        [Fact]
        public void ResetInstance_DropsConfiguredAndCachedResults()
        {
            Redaction.Instance.SetConfig(["customer_email"], [], ["System.Uri"]);
            Redaction.Instance.IsRedactedKeyword("customerEmail").Should().BeTrue();
            Redaction.Instance.IsRedactedType(typeof(Uri)).Should().BeTrue();

            Redaction.Instance.ResetInstance();

            Redaction.Instance.CachedKeywordsCount.Should().Be(0);
            Redaction.Instance.CachedTypesCount.Should().Be(0);
            Redaction.Instance.IsRedactedKeyword("customerEmail").Should().BeFalse();
            Redaction.Instance.IsRedactedType(typeof(Uri)).Should().BeFalse();
        }

        [Theory]
        [InlineData("password", typeof(System.Security.SecureString), RedactionReason.Identifier)]
        [InlineData("no-password", typeof(System.Security.SecureString), RedactionReason.Type)]
        [InlineData("nullable-guid", typeof(Guid?), RedactionReason.Type)]
        [InlineData("api_key", typeof(string), RedactionReason.Identifier)]
        [InlineData("normal", typeof(string), RedactionReason.None)]
        internal void ShouldRedact_CombinedScenarios_Test(string name, Type type, RedactionReason expectedReason)
        {
            // Arrange
            Redaction.Instance.SetConfig(
                new HashSet<string> { "api_key" },
                new HashSet<string>(),
                new HashSet<string> { "System.Security.SecureString", "System.Guid" });

            // Act
            bool result = Redaction.Instance.ShouldRedact(name, type, out var reason);

            // Assert
            Assert.Equal(expectedReason, reason);
            Assert.Equal(expectedReason != RedactionReason.None, result);
        }
    }
}
