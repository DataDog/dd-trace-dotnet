// <copyright file="LogsApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Logging.DirectSubmission.Sink;
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

        private static readonly Func<Uri, TestApiRequest> SingleFaultyRequest = x => new FaultyApiRequest(x);

        [Theory]
        [InlineData("http://http-intake.logs.datadoghq.com", "http://http-intake.logs.datadoghq.com/api/v2/logs")]
        [InlineData("http://http-intake.logs.datadoghq.com/", "http://http-intake.logs.datadoghq.com/api/v2/logs")]
        [InlineData("https://http-intake.logs.datadoghq.com:443", "https://http-intake.logs.datadoghq.com:443/api/v2/logs")]
        [InlineData("http://localhost:8080", "http://localhost:8080/api/v2/logs")]
        [InlineData("http://localhost:8080/sub-path", "http://localhost:8080/sub-path/api/v2/logs")]
        [InlineData("http://localhost:8080/sub-path/", "http://localhost:8080/sub-path/api/v2/logs")]
        public async Task SendsRequestToCorrectUrl(string baseUri, string expected)
        {
            var baseEndpoint = new Uri(baseUri);
            var requestFactory = new TestRequestFactory();

            var api = new LogsApi(baseEndpoint, "SECR3TZ", requestFactory);
            var result = await api.SendLogsAsync(Logs, NumberOfLogs);

            requestFactory.RequestsSent.Should()
                          .OnlyContain(x => x.Endpoint == new Uri(expected));

            result.Should().BeTrue();
        }

        [Fact]
        public async Task ShouldRetryRequestsWhenTheyFail()
        {
            // two faults, then success
            var requestFactory = new TestRequestFactory(SingleFaultyRequest, SingleFaultyRequest);

            var apiKey = "SECR3TZ";
            var api = new LogsApi(new Uri(DefaultIntake), apiKey, requestFactory);
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
        public async Task ShouldAddApiKeyToAllRequests()
        {
            var requestFactory = new TestRequestFactory(SingleFaultyRequest);

            var apiKey = "SECR3TZ";
            var api = new LogsApi(new Uri(DefaultIntake), apiKey, requestFactory);
            await api.SendLogsAsync(Logs, NumberOfLogs);

            requestFactory.RequestsSent.Should()
                          .NotBeEmpty()
                          .And.OnlyContain(x => x.ExtraHeaders.ContainsKey(LogsApi.IntakeHeaderNameApiKey))
                          .And.OnlyContain(x => x.ExtraHeaders[LogsApi.IntakeHeaderNameApiKey] == apiKey);
        }

        [Fact]
        public async Task ShouldSetContentTypeForAllRequests()
        {
            var requestFactory = new TestRequestFactory(SingleFaultyRequest);

            var api = new LogsApi(new Uri(DefaultIntake), "SECR3TZ", requestFactory);
            await api.SendLogsAsync(Logs, NumberOfLogs);

            using var scope = new AssertionScope();
            requestFactory.RequestsSent.Should().NotBeEmpty();
            foreach (var request in requestFactory.RequestsSent)
            {
                request.Responses.Should()
                       .ContainSingle()
                       .And.OnlyContain(x => x.ContentType == "application/json");
            }
        }

        [Fact]
        public async Task ShouldNotRetryWhenClientError()
        {
            var requestFactory = new TestRequestFactory(x => new FaultyApiRequest(x, statusCode: 400));

            var api = new LogsApi(new Uri(DefaultIntake), "SECR3TZ", requestFactory);
            var result = await api.SendLogsAsync(Logs, NumberOfLogs);

            using var scope = new AssertionScope();
            var request = requestFactory.RequestsSent.Should().ContainSingle().Subject;
            request.Responses.Should().ContainSingle().Which.StatusCode.Should().Be(400);

            result.Should().BeFalse();
        }

        internal class TestRequestFactory : IApiRequestFactory
        {
            private readonly Func<Uri, TestApiRequest>[] _requestsToSend;

            public TestRequestFactory(params Func<Uri, TestApiRequest>[] requestsToSend)
            {
                _requestsToSend = requestsToSend;
            }

            public List<TestApiRequest> RequestsSent { get; } = new();

            public string Info(Uri endpoint) => endpoint.ToString();

            public IApiRequest Create(Uri endpoint)
            {
                var request = (_requestsToSend is null || RequestsSent.Count >= _requestsToSend.Length)
                                  ? new TestApiRequest(endpoint)
                                  : _requestsToSend[RequestsSent.Count](endpoint);

                RequestsSent.Add(request);

                return request;
            }

            public void SetProxy(WebProxy proxy, NetworkCredential credential)
            {
            }
        }

        internal class TestApiRequest : IApiRequest
        {
            private readonly int _statusCode;

            public TestApiRequest(Uri endpoint, int statusCode = 200)
            {
                _statusCode = statusCode;
                Endpoint = endpoint;
            }

            public Uri Endpoint { get; }

            public Dictionary<string, string> ExtraHeaders { get; } = new();

            public List<TestApiResponse> Responses { get; } = new();

            public void AddHeader(string name, string value)
            {
                ExtraHeaders.Add(name, value);
            }

            public Task<IApiResponse> PostAsync(ArraySegment<byte> traces, string contentType)
            {
                var response = new TestApiResponse(_statusCode, "The message body", contentType);
                Responses.Add(response);

                return Task.FromResult((IApiResponse)response);
            }
        }

        internal class FaultyApiRequest : TestApiRequest
        {
            public FaultyApiRequest(Uri endpoint, int statusCode = 500)
                : base(endpoint, statusCode)
            {
            }
        }

        internal class TestApiResponse : IApiResponse
        {
            private readonly string _body;

            public TestApiResponse(int statusCode, string body, string contentType)
            {
                StatusCode = statusCode;
                _body = body;
                ContentType = contentType;
            }

            public string ContentType { get; }

            public int StatusCode { get; }

            public long ContentLength => _body?.Length ?? 0;

            public void Dispose()
            {
            }

            public string GetHeader(string headerName) => throw new NotImplementedException();

            public Task<string> ReadAsStringAsync() => Task.FromResult(_body);
        }
    }
}
