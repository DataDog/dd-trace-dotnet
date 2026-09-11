// <copyright file="SpanContextPropagatorTests_AddSecurityTestingHeadersAsTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using System.Net;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Http;
#endif

namespace Datadog.Trace.Tests
{
    [Collection(nameof(WebRequestCollection))]
    public class SpanContextPropagatorTests_AddSecurityTestingHeadersAsTags
    {
        private const string EndpointScanHeader = "x-datadog-endpoint-scan";
        private const string SecurityTestHeader = "x-datadog-security-test";
        private const string EndpointScanTag = "http.request.headers.x-datadog-endpoint-scan";
        private const string SecurityTestTag = "http.request.headers.x-datadog-security-test";

        // AddSecurityTestingHeadersAsTags doesn't read instance state — construct a bare
        // propagator with no injectors/extractors rather than spinning up a full Tracer.
        private static readonly SpanContextPropagator Propagator = SpanContextPropagatorFactory.GetSpanContextPropagator(
            requestedInjectors: Array.Empty<string>(),
            requestedExtractors: Array.Empty<string>(),
            propagationExtractFirst: false);

        public enum HeaderCollectionType
        {
            NameValueHeadersCollection,
            WebHeadersCollection,
        }

        public static TheoryData<HeaderCollectionType> GetHeaderCollections()
            => new() { HeaderCollectionType.NameValueHeadersCollection, HeaderCollectionType.WebHeadersCollection, };

        [Theory]
        [MemberData(nameof(GetHeaderCollections))]
        public void TagsBothMarkersWhenPresentAndIgnoresUnrelatedHeaders(HeaderCollectionType headersType)
        {
            var headers = GetHeadersCollection(headersType);
            headers.Add(EndpointScanHeader, "scan-uuid-1");
            headers.Add(SecurityTestHeader, "test-uuid-2");
            headers.Add("x-other-header", "ignored");

            var span = CreateSpan();
            Propagator.AddSecurityTestingHeadersAsTags(span, headers);

            span.GetTag(EndpointScanTag).Should().Be("scan-uuid-1");
            span.GetTag(SecurityTestTag).Should().Be("test-uuid-2");
            span.GetTag("http.request.headers.x-other-header").Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(GetHeaderCollections))]
        public void DoesNotTagWhenHeadersAreAbsent(HeaderCollectionType headersType)
        {
            var headers = GetHeadersCollection(headersType);
            // Use a custom header name rather than a well-known one (e.g. "content-type").
            // WebHeaderCollection treats well-known request headers as restricted on .NET
            // Framework and throws from `.Add(...)`.
            headers.Add("x-other-header", "value");

            var span = CreateSpan();
            Propagator.AddSecurityTestingHeadersAsTags(span, headers);

            span.GetTag(EndpointScanTag).Should().BeNull();
            span.GetTag(SecurityTestTag).Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(GetHeaderCollections))]
        public void TagsHeadersUnconditionallyWithoutAnyHeaderTagsConfig(HeaderCollectionType headersType)
        {
            // The RFC contract: collection happens regardless of DD_TRACE_HEADER_TAGS. The helper
            // is independent of the HeaderTags dictionary — this test confirms that calling it
            // with no header-tag configuration still produces the markers.
            var headers = GetHeadersCollection(headersType);
            headers.Add(EndpointScanHeader, "scan-uuid");
            headers.Add(SecurityTestHeader, "test-uuid");

            var span = CreateSpan();
            Propagator.AddSecurityTestingHeadersAsTags(span, headers);

            span.GetTag(EndpointScanTag).Should().Be("scan-uuid");
            span.GetTag(SecurityTestTag).Should().Be("test-uuid");
        }

        [Theory]
        [MemberData(nameof(GetHeaderCollections))]
        public void OnlyOneOfTheTwoHeadersIsCollectedIfOnlyOnePresent(HeaderCollectionType headersType)
        {
            var headers = GetHeadersCollection(headersType);
            headers.Add(EndpointScanHeader, "scan-uuid");

            var span = CreateSpan();
            Propagator.AddSecurityTestingHeadersAsTags(span, headers);

            span.GetTag(EndpointScanTag).Should().Be("scan-uuid");
            span.GetTag(SecurityTestTag).Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(GetHeaderCollections))]
        public void TagsHeadersEvenWhenValueIsEmptyString(HeaderCollectionType headersType)
        {
            // RFC: collect unconditionally — presence of the header with an empty value
            // is still a valid signal.
            var headers = GetHeadersCollection(headersType);
            headers.Add(EndpointScanHeader, string.Empty);
            headers.Add(SecurityTestHeader, "ok");

            var span = CreateSpan();
            Propagator.AddSecurityTestingHeadersAsTags(span, headers);

            span.GetTag(EndpointScanTag).Should().Be(string.Empty);
            span.GetTag(SecurityTestTag).Should().Be("ok");
        }

        [Theory]
        [MemberData(nameof(GetHeaderCollections))]
        public void MatchesHeaderNamesCaseInsensitively(HeaderCollectionType headersType)
        {
            // ASP.NET Core's IHeaderDictionary and System.Net's WebHeaderCollection are
            // case-insensitive by contract. Asserting it here locks the contract in regardless
            // of the underlying carrier the entry-span integration happens to use.
            var headers = GetHeadersCollection(headersType);
            headers.Add("X-Datadog-Endpoint-Scan", "scan-uuid");
            headers.Add("X-DATADOG-SECURITY-TEST", "test-uuid");

            var span = CreateSpan();
            Propagator.AddSecurityTestingHeadersAsTags(span, headers);

            span.GetTag(EndpointScanTag).Should().Be("scan-uuid");
            span.GetTag(SecurityTestTag).Should().Be("test-uuid");
        }

#if !NETFRAMEWORK
        [Fact]
        public void WorksWithAspNetCoreHeaderDictionary()
        {
            // The highest-traffic production carrier is HeadersCollectionAdapter, which wraps
            // IHeaderDictionary. Cover it directly here, including the mixed-case lookup that
            // IHeaderDictionary supports out of the box.
            var dictionary = new HeaderDictionary
            {
                ["X-Datadog-Endpoint-Scan"] = "scan-uuid",
                ["x-datadog-security-test"] = "test-uuid",
            };

            var span = CreateSpan();
            Propagator.AddSecurityTestingHeadersAsTags(span, new HeadersCollectionAdapter(dictionary));

            span.GetTag(EndpointScanTag).Should().Be("scan-uuid");
            span.GetTag(SecurityTestTag).Should().Be("test-uuid");
        }
#endif

        private static IHeadersCollection GetHeadersCollection(HeaderCollectionType type)
            => type switch
            {
                HeaderCollectionType.WebHeadersCollection => WebRequest.CreateHttp("http://localhost").Headers.Wrap(),
                HeaderCollectionType.NameValueHeadersCollection => new NameValueCollection().Wrap(),
                _ => throw new Exception("Unknown header collection type " + type),
            };

        private static Span CreateSpan()
            => new(new SpanContext(traceId: 42, RandomIdGenerator.Shared.NextSpanId()), DateTimeOffset.UtcNow);
    }
}
