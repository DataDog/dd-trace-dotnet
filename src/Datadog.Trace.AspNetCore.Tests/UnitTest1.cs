using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Datadog.Trace.AspNetCore.Tests
{
    public class UnitTest1 : IDisposable
    {
        private const string MethodTag = "http.method";
        private const string UrlTag = "http.url";
        private const string StatusCodeTag = "http.status_code";
        private const string ErrorMsgTag = "error.msg";
        private const string ErrorTypeTag = "error.type";
        private const string ErrorStackTag = "error.stack";

        private const string Content = "Hello World!";

        private readonly IWebHost _host;
        private readonly HttpClient _client;
        private readonly MockWriter _writer;
        private readonly Tracer _tracer;

        public UnitTest1()
        {
            _writer = new MockWriter();
            _tracer = new Tracer(_writer);
            _host = new WebHostBuilder()
                .UseUrls("http://localhost:5050")
                .UseKestrel()
                .ConfigureServices(s => s.AddDatadogTrace(_tracer))
                .Configure(app => app
                .Map("/error", HandleError)
                .Map("/child", HandleWithChild)
                .Run(HandleNormal))
                .Build();
            _host.StartAsync().Wait();
            _client = new HttpClient() { BaseAddress = new Uri("http://localhost:5050") };
        }

        public void Dispose()
        {
            _host.StopAsync().Wait();
        }

        [Fact]
        public async void OkResponse()
        {
            var response = await _client.GetAsync("/");
            var content = await response.Content.ReadAsStringAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(1));

            Assert.Equal(Content, content);
            var span = _writer.Traces.Single().Single();
            Assert.Equal("GET", span.Tags[MethodTag]);
            Assert.Equal("/", span.Tags[UrlTag]);
            Assert.Equal("200", span.Tags[StatusCodeTag]);
        }

        [Fact]
        public async void OkResponseWithChildSpan()
        {
            var response = await _client.GetAsync("/child");
            var content = await response.Content.ReadAsStringAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(1));

            Assert.Equal(Content, content);
            var trace = _writer.Traces.Single();
            Assert.Equal(2, trace.Count);
            var root = trace[0];
            Assert.Equal("GET", root.Tags[MethodTag]);
            Assert.Equal("/child", root.Tags[UrlTag]);
            Assert.Equal("200", root.Tags[StatusCodeTag]);
            var child = trace[1];
            Assert.Equal("Child", child.OperationName);
            Assert.Equal(root.Context, child.Context.Parent);
        }

        [Fact]
        public async void Error()
        {
            var response = await _client.GetAsync("/error");
            await Task.Delay(TimeSpan.FromMilliseconds(1));

            var span = _writer.Traces.Single().Single();
            Assert.Equal("GET", span.Tags[MethodTag]);
            Assert.Equal("/error", span.Tags[UrlTag]);
            Assert.Equal("500", span.Tags[StatusCodeTag]);
            Assert.True(span.Error);
            Assert.Equal(typeof(InvalidOperationException).ToString(), span.GetTag(ErrorTypeTag));
            Assert.Equal("Invalid", span.GetTag(ErrorMsgTag));
            Assert.False(string.IsNullOrEmpty(span.GetTag(ErrorStackTag)));
        }

        private static async Task HandleNormal(HttpContext context)
        {
                await context.Response.WriteAsync(Content);
        }

        private static void HandleError(IApplicationBuilder app)
        {
            app.Run(context =>
            {
                throw new InvalidOperationException("Invalid");
            });
        }

        private void HandleWithChild(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                using (_tracer.StartActive("Child"))
                {
                    await context.Response.WriteAsync(Content);
                }
            });
        }
    }
}
