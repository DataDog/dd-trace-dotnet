// <copyright file="SpanContextPropagatorTests_ExtractBaggageHeaderTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(WebRequestCollection))]
    public class SpanContextPropagatorTests_ExtractBaggageHeaderTags
    {
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

        [Fact]
        internal async Task AddBaggageTags()
        {
            // Add Baggage items using a key that's in the default configuration
            var baggageItems = new Dictionary<string, string>();
            baggageItems.Add("user.id", "doggo");
            var baggage = new Baggage(baggageItems);

            var expectedResult = "doggo";

            // Test
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var scope = (Scope)tracer.StartActive("operation");
            tracer.TracerManager.SpanContextPropagator.AddBaggageToSpanAsTags(tracer, scope.Span, baggage);
            scope.Span.GetTag("baggage.user.id").Should().Be(expectedResult);
        }

        [Fact]
        internal async Task AddBaggageTags_WithCustomConfiguration_WildcardEnabled()
        {
            // Custom configuration: enable all baggage keys with wildcard
            var settings = TracerSettings.Create(new()
            {
                { ConfigurationKeys.BaggageTagKeys, "*" }
            });

            // Add Baggage items with a custom key not in default config
            var baggageItems = new Dictionary<string, string>();
            baggageItems.Add("custom.key", "custom-value");
            baggageItems.Add("session.id", "test-session");
            var baggage = new Baggage(baggageItems);

            // Test
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            var scope = (Scope)tracer.StartActive("operation");
            tracer.TracerManager.SpanContextPropagator.AddBaggageToSpanAsTags(tracer, scope.Span, baggage);

            // With wildcard, all baggage items should be added as tags
            scope.Span.GetTag("custom.key").Should().Be("custom-value");
            scope.Span.GetTag("session.id").Should().Be("test-session");
        }

        [Fact]
        internal async Task AddBaggageTags_WithCustomConfiguration_SpecificKeys()
        {
            // Custom configuration: only specific keys
            var settings = TracerSettings.Create(new()
            {
                { ConfigurationKeys.BaggageTagKeys, "custom.id,tenant.id" }
            });

            // Add Baggage items - some match config, some don't
            var baggageItems = new Dictionary<string, string>();
            baggageItems.Add("custom.id", "my-custom-id");
            baggageItems.Add("tenant.id", "my-tenant");
            baggageItems.Add("user.id", "should-be-ignored"); // not in custom config
            var baggage = new Baggage(baggageItems);

            // Test
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            var scope = (Scope)tracer.StartActive("operation");
            tracer.TracerManager.SpanContextPropagator.AddBaggageToSpanAsTags(tracer, scope.Span, baggage);

            // Only configured keys should be added as tags
            scope.Span.GetTag("custom.id").Should().Be("my-custom-id");
            scope.Span.GetTag("tenant.id").Should().Be("my-tenant");
            scope.Span.GetTag("user.id").Should().BeNull(); // not in config, so not added
        }

        [Fact]
        internal async Task AddBaggageTags_WithCustomConfiguration_Disabled()
        {
            // Custom configuration: disable baggage tags with empty string
            var settings = TracerSettings.Create(new()
            {
                { ConfigurationKeys.BaggageTagKeys, string.Empty }
            });

            // Add Baggage items
            var baggageItems = new Dictionary<string, string>();
            baggageItems.Add("user.id", "should-be-ignored");
            var baggage = new Baggage(baggageItems);

            // Test
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            var scope = (Scope)tracer.StartActive("operation");
            tracer.TracerManager.SpanContextPropagator.AddBaggageToSpanAsTags(tracer, scope.Span, baggage);

            // No baggage tags should be added when feature is disabled
            scope.Span.GetTag("user.id").Should().BeNull();
        }
    }
}
