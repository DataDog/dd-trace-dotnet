using System;
using System.Text;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class OtelScopeFactoryTests
    {
        [Theory]
        [ClassData(typeof(TestData))]
        public void OutboundHttp(Input input, Result expected)
        {
            var settings = new TracerSettings();
            settings.Convention = ConventionType.OpenTelemetry;
            var tracer = new Tracer(settings, Mock.Of<IAgentWriter>(), Mock.Of<ISampler>(), scopeManager: null, statsd: null);

            using (var scope = ScopeFactory.CreateOutboundHttpScope(tracer, input.Method, new Uri(input.Uri), new IntegrationInfo((int)IntegrationIds.HttpMessageHandler), out var tags))
            {
                var span = scope.Span;
                var actual = new Result
                {
                    OperationName = span.OperationName,
                    HttpMethodTag = span.GetTag("http.method"),
                    HttpUrlTag = span.GetTag("http.url"),
                };

                Assert.Equal(expected, actual);
            }
        }

        public struct Input
        {
            public string Method;
            public string Uri;
        }

        public struct Result
        {
            public string OperationName;
            public string HttpMethodTag;
            public string HttpUrlTag;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append(Environment.NewLine);
                foreach (var field in GetType().GetFields())
                {
                    sb.Append($"{field.Name}: {field.GetValue(this)}{Environment.NewLine}");
                }

                return sb.ToString();
            }
        }

        public class TestData : TheoryData<Input, Result>
        {
#pragma warning disable SA1118 // The parameter spans multiple lines
            public TestData()
            {
                Add(
                    new Input
                    {
                        Method = "GET",
                        Uri = "https://username:password@example.com:8080/path/to/file.aspx?query=1#fragment",
                    },
                    new Result
                    {
                        OperationName = "HTTP GET",
                        HttpMethodTag = "GET",
                        HttpUrlTag = "https://example.com:8080/path/to/file.aspx?query=1#fragment",
                    });
                Add(
                    new Input
                    {
                        Method = "GET",
                        Uri = "https://username:password@example.com/path/to/file.aspx?query=1#fragment",
                    },
                    new Result
                    {
                        OperationName = "HTTP GET",
                        HttpMethodTag = "GET",
                        HttpUrlTag = "https://example.com/path/to/file.aspx?query=1#fragment",
                    });
                Add(
                    new Input
                    {
                        Method = "GET",
                        Uri = "https://username@example.com/path/to/file.aspx",
                    },
                    new Result
                    {
                        OperationName = "HTTP GET",
                        HttpMethodTag = "GET",
                        HttpUrlTag = "https://example.com/path/to/file.aspx",
                    });
                Add(
                    new Input
                    {
                        Method = "GET",
                        Uri = "https://example.com/path/to/file.aspx?query=1",
                    },
                    new Result
                    {
                        OperationName = "HTTP GET",
                        HttpMethodTag = "GET",
                        HttpUrlTag = "https://example.com/path/to/file.aspx?query=1",
                    });
                Add(
                    new Input
                    {
                        Method = "GET",
                        Uri = "https://example.com/path/to/file.aspx#fragment",
                    },
                    new Result
                    {
                        OperationName = "HTTP GET",
                        HttpMethodTag = "GET",
                        HttpUrlTag = "https://example.com/path/to/file.aspx#fragment",
                    });
                Add(
                    new Input
                    {
                        Method = "GET",
                        Uri = "https://example.com/path/to/file.aspx",
                    },
                    new Result
                    {
                        OperationName = "HTTP GET",
                        HttpMethodTag = "GET",
                        HttpUrlTag = "https://example.com/path/to/file.aspx",
                    });
                Add(
                    new Input
                    {
                        Method = "GET",
                        Uri = "https://example.com/path/123/file.aspx",
                    },
                    new Result
                    {
                        OperationName = "HTTP GET",
                        HttpMethodTag = "GET",
                        HttpUrlTag = "https://example.com/path/123/file.aspx",
                    });
                Add(
                    new Input
                    {
                        Method = "GET",
                        Uri = "https://example.com/path/123/",
                    },
                    new Result
                    {
                        OperationName = "HTTP GET",
                        HttpMethodTag = "GET",
                        HttpUrlTag = "https://example.com/path/123/",
                    });
                Add(
                    new Input
                    {
                        Method = "GET",
                        Uri = "https://example.com/path/123",
                    },
                    new Result
                    {
                        OperationName = "HTTP GET",
                        HttpMethodTag = "GET",
                        HttpUrlTag = "https://example.com/path/123",
                    });
                Add(
                    new Input
                    {
                        Method = "GET",
                        Uri = "https://example.com/path/E653C852-227B-4F0C-9E48-D30D83C68BF3",
                    },
                    new Result
                    {
                        OperationName = "HTTP GET",
                        HttpMethodTag = "GET",
                        HttpUrlTag = "https://example.com/path/E653C852-227B-4F0C-9E48-D30D83C68BF3",
                    });
                Add(
                    new Input
                    {
                        Method = "GET",
                        Uri = "https://example.com/path/E653C852227B4F0C9E48D30D83C68BF3",
                    },
                    new Result
                    {
                        OperationName = "HTTP GET",
                        HttpMethodTag = "GET",
                        HttpUrlTag = "https://example.com/path/E653C852227B4F0C9E48D30D83C68BF3",
                    });
            }
#pragma warning restore SA1118
        }
    }
}