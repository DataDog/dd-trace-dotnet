// <copyright file="DataStreamsContextPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.ExtensionMethods;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring
{
    public class DataStreamsContextPropagatorTests
    {
        [Theory]
        [CombinatorialData]
        public void CanRoundTripPathwayContext(bool isDataStreamsLegacyHeadersEnabled)
        {
            var oneMs = TimeSpan.FromMilliseconds(1);
            var headers = new TestHeadersCollection();
            var context = new PathwayContext(
                new PathwayHash(1234),
                DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeNanoseconds(),
                DateTimeOffset.UtcNow.ToUnixTimeNanoseconds());

            DataStreamsContextPropagator.Instance.Inject(context, headers, isDataStreamsLegacyHeadersEnabled);

            var extracted = DataStreamsContextPropagator.Instance.Extract(headers);

            extracted.Should().NotBeNull();
            extracted.Value.Hash.Value.Should().Be(context.Hash.Value);
            FromUnixTimeNanoseconds(extracted.Value.PathwayStart)
               .Should()
               .BeCloseTo(FromUnixTimeNanoseconds(context.PathwayStart), oneMs);
            FromUnixTimeNanoseconds(extracted.Value.EdgeStart)
               .Should()
               .BeCloseTo(FromUnixTimeNanoseconds(context.EdgeStart), oneMs);
        }

        [Fact]
        public void Inject_WhenLegacyHeadersDisabled_DoesNotIncludeBinaryHeader()
        {
            var headers = new TestHeadersCollection();
            var context = new PathwayContext(
                new PathwayHash(1234),
                DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeNanoseconds(),
                DateTimeOffset.UtcNow.ToUnixTimeNanoseconds());

            DataStreamsContextPropagator.Instance.Inject(context, headers, isDataStreamsLegacyHeadersEnabled: false);

            headers.Values.Should().ContainKey(DataStreamsPropagationHeaders.PropagationKeyBase64);
            headers.Values[DataStreamsPropagationHeaders.PropagationKeyBase64].Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Extract_WhenBothHeadersPresent_PrefersBase64Header()
        {
            var headers = new TestHeadersCollection();

            var base64Context = new PathwayContext(
                new PathwayHash(1234),
                DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeNanoseconds(),
                DateTimeOffset.UtcNow.ToUnixTimeNanoseconds());

            var binaryContext = new PathwayContext(
                new PathwayHash(5678),
                DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeNanoseconds(),
                DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeNanoseconds());

            var encodedBase64ContextBytes = PathwayContextEncoder.Encode(base64Context);
            var base64EncodedContext = Convert.ToBase64String(encodedBase64ContextBytes);
            headers.Add(DataStreamsPropagationHeaders.PropagationKeyBase64, Encoding.UTF8.GetBytes(base64EncodedContext));

            var encodedBinaryContextBytes = PathwayContextEncoder.Encode(binaryContext);
            headers.Add(DataStreamsPropagationHeaders.PropagationKey, encodedBinaryContextBytes);

            var extractedContext = DataStreamsContextPropagator.Instance.Extract(headers);

            extractedContext.Should().NotBeNull();
            extractedContext.Value.Hash.Value.Should().Be(base64Context.Hash.Value);

            extractedContext.Value.Hash.Value.Should().NotBe(binaryContext.Hash.Value);
            extractedContext.Value.PathwayStart.Should().NotBe(binaryContext.PathwayStart);
            extractedContext.Value.EdgeStart.Should().NotBe(binaryContext.EdgeStart);
        }

        [Theory]
        [CombinatorialData]
        public void InjectedHeaders_HaveCorrectFormat(bool isDataStreamsLegacyHeadersEnabled)
        {
            var headers = new TestHeadersCollection();
            var context = new PathwayContext(
                new PathwayHash(0x12345678),
                0x1122334455667788,
                unchecked((long)0x99AABBCCDDEEFF00));

            DataStreamsContextPropagator.Instance.Inject(context, headers, isDataStreamsLegacyHeadersEnabled);

            headers.Values.Should().ContainKey(DataStreamsPropagationHeaders.PropagationKeyBase64);
            var base64HeaderValueBytes = headers.Values[DataStreamsPropagationHeaders.PropagationKeyBase64];
            var base64HeaderValue = Encoding.UTF8.GetString(base64HeaderValueBytes);
            Assert.True(IsBase64String(base64HeaderValue), "Base64 header is not a valid Base64 string.");
        }

        [Fact]
        public void InjectHeaders_WhenLegacyHeadersDisabled_DoesNotIncludeLegacyHeader()
        {
            var headers = new TestHeadersCollection();
            var context = new PathwayContext(
                new PathwayHash(4321),
                DateTimeOffset.UtcNow.AddSeconds(-15).ToUnixTimeNanoseconds(),
                DateTimeOffset.UtcNow.ToUnixTimeNanoseconds());

            DataStreamsContextPropagator.Instance.Inject(context, headers, isDataStreamsLegacyHeadersEnabled: false);

            headers.Values.Should().ContainKey(DataStreamsPropagationHeaders.PropagationKeyBase64);
            headers.Values[DataStreamsPropagationHeaders.PropagationKeyBase64].Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Inject_WhenLegacyHeadersEnabled_IncludesBothHeaders()
        {
            var headers = new TestHeadersCollection();
            var context = new PathwayContext(
                new PathwayHash(7890),
                DateTimeOffset.UtcNow.AddSeconds(-20).ToUnixTimeNanoseconds(),
                DateTimeOffset.UtcNow.ToUnixTimeNanoseconds());

            DataStreamsContextPropagator.Instance.Inject(context, headers, isDataStreamsLegacyHeadersEnabled: true);

            headers.Values.Should().ContainKey(DataStreamsPropagationHeaders.PropagationKeyBase64);
            var base64HeaderValueBytes = headers.Values[DataStreamsPropagationHeaders.PropagationKeyBase64];
            var base64HeaderValue = Encoding.UTF8.GetString(base64HeaderValueBytes);
            Assert.True(IsBase64String(base64HeaderValue), "Base64 header is not a valid Base64 string.");

            headers.Values.Should().ContainKey(DataStreamsPropagationHeaders.PropagationKey);
            var binaryHeaderValueBytes = headers.Values[DataStreamsPropagationHeaders.PropagationKey];
            binaryHeaderValueBytes.Should().NotBeNullOrEmpty();

            try
            {
                var binaryAsBase64 = Encoding.UTF8.GetString(binaryHeaderValueBytes);
                Convert.FromBase64String(binaryAsBase64);
                // If no exception then the binary header was incorrectly Base64-encoded
                Assert.Fail("Binary header should not be Base64-encoded.");
            }
            catch (FormatException)
            {
                // Expected if binary data is not valid Base64
            }
        }

        private static DateTimeOffset FromUnixTimeNanoseconds(long nanoseconds)
            => DateTimeOffset.FromUnixTimeMilliseconds(nanoseconds / 1_000_000);

        private bool IsBase64String(string base64)
        {
            try
            {
                Convert.FromBase64String(base64);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
