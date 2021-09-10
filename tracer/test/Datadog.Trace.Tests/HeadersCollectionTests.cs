// <copyright file="HeadersCollectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.Tests
{
    // TODO: for now, these tests cover all of this,
    // but we should probably split them up into actual *unit* tests for:
    // - HttpHeadersCollection wrapper over HttpHeaders (Get, Set, Add, Remove)
    // - NameValueHeadersCollection wrapper over NameValueCollection (Get, Set, Add, Remove)
    // - SpanContextPropagator.Inject()
    // - SpanContextPropagator.Extract()
    public class HeadersCollectionTests
    {
        private static readonly string TestPrefix = "test.prefix";

        public static IEnumerable<object[]> GetHeaderCollectionImplementations()
        {
            yield return new object[] { WebRequest.CreateHttp("http://localhost").Headers.Wrap() };
            yield return new object[] { new NameValueCollection().Wrap() };
            yield return new object[] { new DictionaryHeadersCollection() };
        }

        public static IEnumerable<object[]> GetHeadersInvalidIdsCartesianProduct()
        {
            return from header in GetHeaderCollectionImplementations().SelectMany(i => i)
                   from invalidId in HeadersCollectionTestHelpers.GetInvalidIds().SelectMany(i => i)
                   select new[] { header, invalidId };
        }

        public static IEnumerable<object[]> GetHeadersInvalidSamplingPrioritiesCartesianProduct()
        {
            return from header in GetHeaderCollectionImplementations().SelectMany(i => i)
                   from invalidSamplingPriority in HeadersCollectionTestHelpers.GetInvalidSamplingPriorities().SelectMany(i => i)
                   select new[] { header, invalidSamplingPriority };
        }

        [Theory]
        [MemberData(nameof(GetHeaderCollectionImplementations))]
        internal void ExtractHeaderTags_MatchesCaseInsensitiveHeaders(IHeadersCollection headers)
        {
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
            var tagsFromHeader = SpanContextPropagator.Instance.ExtractHeaderTags(headers, headerTags, TestPrefix);

            // Assert
            Assert.NotNull(tagsFromHeader);
            Assert.Equal(expectedResults, tagsFromHeader);
        }

        [Theory]
        [MemberData(nameof(GetHeaderCollectionImplementations))]
        internal void ExtractHeaderTags_EmptyHeaders_AddsNoTags(IHeadersCollection headers)
        {
            // Do not add headers

            // Initialize header-tag arguments and expectations
            var headerTags = new Dictionary<string, string>();
            headerTags.Add("x-header-test-runner", "test-runner");

            var expectedResults = new Dictionary<string, string>();

            // Test
            var tagsFromHeader = SpanContextPropagator.Instance.ExtractHeaderTags(headers, headerTags, TestPrefix);

            // Assert
            Assert.NotNull(tagsFromHeader);
            Assert.Equal(expectedResults, tagsFromHeader);
        }

        [Theory]
        [MemberData(nameof(GetHeaderCollectionImplementations))]
        internal void ExtractHeaderTags_EmptyHeaderTags_AddsNoTags(IHeadersCollection headers)
        {
            // Add headers
            headers.Add("x-header-test-runner", "xunit");

            // Initialize header-tag arguments and expectations
            var headerToTagMap = new Dictionary<string, string>();
            var expectedResults = new Dictionary<string, string>();

            // Test
            var tagsFromHeader = SpanContextPropagator.Instance.ExtractHeaderTags(headers, headerToTagMap, TestPrefix);

            // Assert
            Assert.NotNull(tagsFromHeader);
            Assert.Equal(expectedResults, tagsFromHeader);
        }

        [Theory]
        [MemberData(nameof(GetHeaderCollectionImplementations))]
        internal void ExtractHeaderTags_ForEmptyStringMappings_CreatesNormalizedTagWithPrefix(IHeadersCollection headers)
        {
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
            var tagsFromHeader = SpanContextPropagator.Instance.ExtractHeaderTags(headers, headerToTagMap, TestPrefix);

            // Assert
            Assert.NotNull(tagsFromHeader);
            Assert.Equal(expectedResults, tagsFromHeader);
        }

        [Theory]
        [MemberData(nameof(GetHeaderCollectionImplementations))]
        internal void Extract_EmptyHeadersReturnsNull(IHeadersCollection headers)
        {
            var resultContext = SpanContextPropagator.Instance.Extract(headers);
            Assert.Null(resultContext);
        }

        [Theory]
        [MemberData(nameof(GetHeaderCollectionImplementations))]
        internal void InjectExtract_Identity(IHeadersCollection headers)
        {
            const int traceId = 9;
            const int spanId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;
            const string origin = "synthetics";

            var context = new SpanContext(traceId, spanId, samplingPriority, null, origin);
            SpanContextPropagator.Instance.Inject(context, headers);
            var resultContext = SpanContextPropagator.Instance.Extract(headers);

            Assert.NotNull(resultContext);
            Assert.Equal(context.SpanId, resultContext.SpanId);
            Assert.Equal(context.TraceId, resultContext.TraceId);
            Assert.Equal(context.SamplingPriority, resultContext.SamplingPriority);
            Assert.Equal(context.Origin, resultContext.Origin);
        }

        [Theory]
        [MemberData(nameof(GetHeadersInvalidIdsCartesianProduct))]
        internal void Extract_InvalidTraceId(IHeadersCollection headers, string traceId)
        {
            const string spanId = "7";
            const string samplingPriority = "2";
            const string origin = "synthetics";

            InjectContext(headers, traceId, spanId, samplingPriority, origin);
            var resultContext = SpanContextPropagator.Instance.Extract(headers);

            // invalid traceId should return a null context even if other values are set
            Assert.Null(resultContext);
        }

        [Theory]
        [MemberData(nameof(GetHeadersInvalidIdsCartesianProduct))]
        internal void Extract_InvalidSpanId(IHeadersCollection headers, string spanId)
        {
            const ulong traceId = 9;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;
            const string origin = "synthetics";

            InjectContext(
                headers,
                traceId.ToString(CultureInfo.InvariantCulture),
                spanId,
                ((int)samplingPriority).ToString(CultureInfo.InvariantCulture),
                origin);

            var resultContext = SpanContextPropagator.Instance.Extract(headers);

            Assert.NotNull(resultContext);
            Assert.Equal(traceId, resultContext.TraceId);
            Assert.Equal(default(ulong), resultContext.SpanId);
            Assert.Equal(samplingPriority, resultContext.SamplingPriority);
            Assert.Equal(origin, resultContext.Origin);
        }

        [Theory]
        [MemberData(nameof(GetHeadersInvalidSamplingPrioritiesCartesianProduct))]
        internal void Extract_InvalidSamplingPriority(IHeadersCollection headers, string samplingPriority)
        {
            const ulong traceId = 9;
            const ulong spanId = 7;
            const string origin = "synthetics";

            InjectContext(
                headers,
                traceId.ToString(CultureInfo.InvariantCulture),
                spanId.ToString(CultureInfo.InvariantCulture),
                samplingPriority,
                origin);

            var resultContext = SpanContextPropagator.Instance.Extract(headers);

            Assert.NotNull(resultContext);
            Assert.Equal(traceId, resultContext.TraceId);
            Assert.Equal(spanId, resultContext.SpanId);
            Assert.Null(resultContext.SamplingPriority);
            Assert.Equal(origin, resultContext.Origin);
        }

        private static void InjectContext(IHeadersCollection headers, string traceId, string spanId, string samplingPriority, string origin)
        {
            headers.Add(HttpHeaderNames.TraceId, traceId);
            headers.Add(HttpHeaderNames.ParentId, spanId);
            headers.Add(HttpHeaderNames.SamplingPriority, samplingPriority);
            headers.Add(HttpHeaderNames.Origin, origin);
        }
    }
}
