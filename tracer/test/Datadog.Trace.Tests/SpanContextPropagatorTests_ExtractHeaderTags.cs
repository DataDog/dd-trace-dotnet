// <copyright file="SpanContextPropagatorTests_ExtractHeaderTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Xunit;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(WebRequestCollection))]
    public class SpanContextPropagatorTests_ExtractHeaderTags
    {
        private static readonly string TestPrefix = "test.prefix";

        public enum HeaderCollectionType
        {
            /// <summary>
            /// NameValueCollection
            /// </summary>
            NameValueHeadersCollection,

            /// <summary>
            /// WebHeadersCollection
            /// </summary>
            WebHeadersCollection,
        }

        public static TheoryData<HeaderCollectionType> GetHeaderCollections()
            => new() { HeaderCollectionType.NameValueHeadersCollection, HeaderCollectionType.WebHeadersCollection, };

        internal static IHeadersCollection GetHeadersCollection(HeaderCollectionType type)
            => type switch
            {
                HeaderCollectionType.WebHeadersCollection => WebRequest.CreateHttp("http://localhost").Headers.Wrap(),
                HeaderCollectionType.NameValueHeadersCollection => new NameValueCollection().Wrap(),
                _ => throw new Exception("Unknown header collection type " + type),
            };

        [Theory]
        [MemberData(nameof(GetHeaderCollections))]
        internal void ExtractHeaderTags_MatchesCaseInsensitiveHeaders(HeaderCollectionType headersType)
        {
            var headers = GetHeadersCollection(headersType);

            // Initialize constants
            const string customHeader1Name = "dd-custom-header1";
            const string customHeader1Value = "match1";
            const string customHeader1TagName = "custom-header1-tag";

            const string customHeader2Name = "DD-CUSTOM-HEADER-MISMATCHING-CASE";
            const string customHeader2Value = "match2";
            const string customHeader2TagName = "custom-header2-tag";
            string customHeader2LowercaseHeaderName = customHeader2Name.ToLowerInvariant();

            // Add headers
            headers.Add(customHeader1Name, customHeader1Value);
            headers.Add(customHeader2Name, customHeader2Value);

            // Initialize header-tag arguments and expectations
            var headerTags = new Dictionary<string, string>();
            headerTags.Add(customHeader1Name, customHeader1TagName);
            headerTags.Add(customHeader2LowercaseHeaderName, customHeader2TagName);

            var expectedResults = new Dictionary<string, string>();
            expectedResults.Add(customHeader1TagName, customHeader1Value);
            expectedResults.Add(customHeader2TagName, customHeader2Value);

            // Test
            var processor = new HeaderTagsProcessor();
            SpanContextPropagator.Instance.ExtractHeaderTags(ref processor, headers, headerTags, TestPrefix);
            var tagsFromHeader = processor.TagsFromHeader;

            // Assert
            Assert.NotNull(tagsFromHeader);
            Assert.Equal(expectedResults, tagsFromHeader);
        }

        [Theory]
        [MemberData(nameof(GetHeaderCollections))]
        internal void ExtractHeaderTags_EmptyHeaders_AddsNoTags(HeaderCollectionType headersType)
        {
            var headers = GetHeadersCollection(headersType);

            // Do not add headers

            // Initialize header-tag arguments and expectations
            var headerTags = new Dictionary<string, string>();
            headerTags.Add("x-header-test-runner", "test-runner");

            var expectedResults = new Dictionary<string, string>();

            // Test
            var processor = new HeaderTagsProcessor();
            SpanContextPropagator.Instance.ExtractHeaderTags(ref processor, headers, headerTags, TestPrefix);
            var tagsFromHeader = processor.TagsFromHeader;

            // Assert
            Assert.NotNull(tagsFromHeader);
            Assert.Equal(expectedResults, tagsFromHeader);
        }

        [Theory]
        [MemberData(nameof(GetHeaderCollections))]
        internal void ExtractHeaderTags_EmptyHeaderTags_AddsNoTags(HeaderCollectionType headersType)
        {
            var headers = GetHeadersCollection(headersType);

            // Add headers
            headers.Add("x-header-test-runner", "xunit");

            // Initialize header-tag arguments and expectations
            var headerToTagMap = new Dictionary<string, string>();
            var expectedResults = new Dictionary<string, string>();

            // Test
            var processor = new HeaderTagsProcessor();
            SpanContextPropagator.Instance.ExtractHeaderTags(ref processor, headers, headerToTagMap, TestPrefix);
            var tagsFromHeader = processor.TagsFromHeader;

            // Assert
            Assert.NotNull(tagsFromHeader);
            Assert.Equal(expectedResults, tagsFromHeader);
        }

        [Theory]
        [MemberData(nameof(GetHeaderCollections))]
        internal void ExtractHeaderTags_ForEmptyStringMappings_CreatesNormalizedTagWithPrefix(HeaderCollectionType headersType)
        {
            var headers = GetHeadersCollection(headersType);

            string invalidCharacterSequence = "*|&#$%&^`.";
            string normalizedReplacementSequence = new string('_', invalidCharacterSequence.Length);

            // Add headers
            headers.Add("x-header-test-runner", "xunit");
            headers.Add($"x-header-1DATADOG-{invalidCharacterSequence}", "true");

            // Initialize header-tag arguments and expectations
            var headerToTagMap = new Dictionary<string, string>
            {
                { "x-header-test-runner", string.Empty },
                { $"x-header-1DATADOG-{invalidCharacterSequence}", string.Empty },
            };

            var expectedResults = new Dictionary<string, string>
            {
                { TestPrefix + "." + "x-header-test-runner", "xunit" },
                { TestPrefix + "." + $"x-header-1datadog-{normalizedReplacementSequence}", "true" }
            };

            // Test
            var processor = new HeaderTagsProcessor();
            SpanContextPropagator.Instance.ExtractHeaderTags(ref processor, headers, headerToTagMap, TestPrefix);
            var tagsFromHeader = processor.TagsFromHeader;

            // Assert
            Assert.NotNull(tagsFromHeader);
            Assert.Equal(expectedResults, tagsFromHeader);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        internal void ExtractHeaderTags_WithUserAgent(bool provideUserAgent)
        {
            // Initialize constants
            const string uaInHeaders = "A wonderful useragent";
            const string uaInParameter = "A wonderful useragent truncated in the headers";
            const string tagName = "user-agent-tag";

            var headers = new NameValueCollection().Wrap();

            // Add headers
            headers.Add(HttpHeaderNames.UserAgent, uaInHeaders);

            // Initialize header-tag arguments and expectations
            var headerTags = new Dictionary<string, string>();
            headerTags.Add(HttpHeaderNames.UserAgent, tagName);

            // Test no user agent
            var processor = new HeaderTagsProcessor();
            if (provideUserAgent)
            {
                SpanContextPropagator.Instance.ExtractHeaderTags(ref processor, headers, headerTags, TestPrefix, uaInParameter);
            }
            else
            {
                SpanContextPropagator.Instance.ExtractHeaderTags(ref processor, headers, headerTags, TestPrefix);
            }

            var tagsFromHeader = processor.TagsFromHeader;

            // Assert
            Assert.Single(tagsFromHeader);
            var normalizedHeader = tagsFromHeader.First();
            Assert.Equal(tagName, normalizedHeader.Key);
            Assert.Equal(provideUserAgent ? uaInParameter : uaInHeaders, normalizedHeader.Value);
        }

        private readonly struct HeaderTagsProcessor : SpanContextPropagator.IHeaderTagProcessor
        {
            public HeaderTagsProcessor()
            {
                TagsFromHeader = new();
            }

            public Dictionary<string, string> TagsFromHeader { get; }

            public void ProcessTag(string key, string value)
            {
                TagsFromHeader[key] = value;
            }
        }
    }
}
