// <copyright file="LogsApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.TestHelpers.TransportHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission.Sink
{
    public class LogsApiTests
    {
        private const string DefaultIntake = "https://http-intake.logs.datadoghq.com:443";
        private const int NumberOfLogs = 1;

        private static readonly ArraySegment<byte> Logs = new(
            Encoding.UTF8.GetBytes("{\"Level\":\"Debug\",\"Message\":\"Well done, you sent a message\"}"));

        private static readonly Func<Uri, TestApiRequest> SingleFaultyRequest
            = x => new FaultyApiRequest(x);

        [Theory]
        [InlineData("https://http-intake.logs.datadoghq.com", "https://http-intake.logs.datadoghq.com/api/v2/logs")]
        [InlineData("https://http-intake.logs.datadoghq.com/", "https://http-intake.logs.datadoghq.com/api/v2/logs")]
        [InlineData("https://http-intake.logs.datadoghq.com:443", "https://http-intake.logs.datadoghq.com:443/api/v2/logs")]
        [InlineData("http://localhost:8080", "http://localhost:8080/api/v2/logs")]
        [InlineData("http://localhost:8080/sub-path", "http://localhost:8080/sub-path/api/v2/logs")]
        [InlineData("http://localhost:8080/sub-path/", "http://localhost:8080/sub-path/api/v2/logs")]
        public async Task SendsRequestToCorrectUrl(string baseUri, string expected)
        {
            var baseEndpoint = new Uri(baseUri);
            var requestFactory = new TestRequestFactory(baseEndpoint);

            var api = new LogsApi(requestFactory);
            var result = await api.SendLogsAsync(Logs, NumberOfLogs);

            requestFactory.RequestsSent.Should()
                          .OnlyContain(x => x.Endpoint == new Uri(expected));

            result.Should().BeTrue();
        }

        [Fact]
        public async Task RejectsUnsafeUrlWithProductionTransport()
        {
            var source = new NameValueConfigurationSource(
                new NameValueCollection
                {
                    { ConfigurationKeys.ApiKey, "test-key" },
                    { ConfigurationKeys.DirectLogSubmission.Url, "http://example.com" }
                });
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);
            var api = new LogsApi(LogsTransportStrategy.Get(settings));

            var result = await api.SendLogsAsync(Logs, NumberOfLogs);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task ShouldRetryRequestsWhenTheyFail()
        {
            // two faults, then success
            var requestFactory = new TestRequestFactory(new Uri(DefaultIntake), SingleFaultyRequest, SingleFaultyRequest);

            var api = new LogsApi(requestFactory);
            var result = await api.SendLogsAsync(Logs, NumberOfLogs);

            requestFactory.RequestsSent
                          .Where(x => x is FaultyApiRequest)
                          .Should()
                          .HaveCount(2);

            requestFactory.RequestsSent
                          .Where(x => x is not FaultyApiRequest)
                          .Should()
                          .HaveCount(1);
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ShouldNotRetryAfterApiKeyTransportRejection()
        {
            var requestFactory = new TestRequestFactory(new Uri(DefaultIntake), x => new UnsafeApiKeyTransportRequest(x));
            var api = new LogsApi(requestFactory);

            var firstResult = await api.SendLogsAsync(Logs, NumberOfLogs);
            var secondResult = await api.SendLogsAsync(Logs, NumberOfLogs);

            firstResult.Should().BeFalse();
            secondResult.Should().BeFalse();
            requestFactory.RequestsSent.Should().ContainSingle();
        }

        [Fact]
        public async Task ShouldNotRetryAfterApiKeyTransportRejectionDuringRequestCreation()
        {
            var requestFactory = new RejectingRequestFactory(new Uri(DefaultIntake));
            var api = new LogsApi(requestFactory);

            var firstResult = await api.SendLogsAsync(Logs, NumberOfLogs);
            var secondResult = await api.SendLogsAsync(Logs, NumberOfLogs);

            firstResult.Should().BeFalse();
            secondResult.Should().BeFalse();
            requestFactory.CreationAttempts.Should().Be(1);
        }

        [Fact]
        public async Task ShouldSetContentTypeForAllRequests()
        {
            var requestFactory = new TestRequestFactory(new Uri(DefaultIntake), SingleFaultyRequest);

            var api = new LogsApi(requestFactory);
            await api.SendLogsAsync(Logs, NumberOfLogs);

            using var scope = new AssertionScope();
            requestFactory.RequestsSent.Should().NotBeEmpty();
            foreach (var request in requestFactory.RequestsSent)
            {
                request.Responses.Should()
                       .ContainSingle()
                       .And.OnlyContain(x => x.ContentTypeHeader == "application/json");
            }
        }

        [Fact]
        public async Task ShouldNotRetryWhenClientError()
        {
            var requestFactory = new TestRequestFactory(new Uri(DefaultIntake), x => new FaultyApiRequest(x, statusCode: 400));

            var api = new LogsApi(requestFactory);
            var result = await api.SendLogsAsync(Logs, NumberOfLogs);

            using var scope = new AssertionScope();
            var request = requestFactory.RequestsSent.Should().ContainSingle().Subject;
            request.Responses.Should().ContainSingle().Which.StatusCode.Should().Be(400);

            result.Should().BeFalse();
        }

        private sealed class UnsafeApiKeyTransportRequest : TestApiRequest
        {
            public UnsafeApiKeyTransportRequest(Uri endpoint)
                : base(endpoint)
            {
            }

            public override Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding)
                => Task.FromException<IApiResponse>(new ApiKeyHttpTransportException("Unsafe API-key transport."));
        }

        private sealed class RejectingRequestFactory : IApiRequestFactory
        {
            private readonly Uri _baseEndpoint;

            public RejectingRequestFactory(Uri baseEndpoint)
            {
                _baseEndpoint = baseEndpoint;
            }

            public int CreationAttempts { get; private set; }

            public Uri GetEndpoint(string relativePath) => new(_baseEndpoint, relativePath);

            public string Info(Uri endpoint) => endpoint.ToString();

            public IApiRequest Create(Uri endpoint)
            {
                CreationAttempts++;
                throw new ApiKeyHttpTransportException("Unsafe API-key transport.");
            }

            public void SetProxy(WebProxy proxy, NetworkCredential credential)
            {
            }
        }
    }
}
