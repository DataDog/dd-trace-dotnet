using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.TestUtils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datadog.Trace.AspNetCore.Tests
{
    public class FunctionalTests : IDisposable
    {
        private const string MethodTag = "http.method";
        private const string UrlTag = "http.url";
        private const string StatusCodeTag = "http.status_code";
        private const string ErrorMsgTag = "error.msg";
        private const string ErrorTypeTag = "error.type";
        private const string ErrorStackTag = "error.stack";

        private const string Content = "Hello World!";
        private const string DefaultServiceName = "testhost";

        private readonly IWebHost _host;
        private readonly HttpClient _client;
        private readonly MockWriter _writer;
        private readonly Tracer _tracer;
        private readonly EndRequestWaiter _waiter;

        public FunctionalTests()
        {
            _writer = new MockWriter();
            _tracer = new Tracer(_writer);
            _waiter = new EndRequestWaiter();
            _host = new WebHostBuilder()
                .UseUrls("http://localhost:5050")
                .UseKestrel()
                .ConfigureServices(s => s.AddDatadogTrace(_tracer)
                                         .AddMvc())
                .Configure(app => app
                .Map("/error", HandleError)
                .Map("/child", HandleWithChild)
                .UseMvcWithDefaultRoute()
                .Run(HandleNormal))
                .Build();
            _host.Start();
            _client = new HttpClient() { BaseAddress = new Uri("http://localhost:5050") };
        }

        public void Dispose()
        {
            _host.Dispose();
            _waiter.Dispose();
        }

        [Fact]
        public async void OkResponse()
        {
            var response = await _client.GetAsync("/");
            var content = await response.Content.ReadAsStringAsync();
            _waiter.Wait();

            Assert.Equal(Content, content);
            var span = _writer.Traces.Single().Single();
            Assert.Equal("GET", span.Tags[MethodTag]);
            Assert.Equal("/", span.Tags[UrlTag]);
            Assert.Equal("200", span.Tags[StatusCodeTag]);
            Assert.Equal("GET 200", span.ResourceName);
            Assert.Equal(DefaultServiceName, span.ServiceName);
            Assert.True(span.IsRootSpan);
        }

        [Fact]
        public async void OkResponse_WithContextPropagationDisabled()
        {
            const ulong parentId = 7;
            const ulong traceId = 9;
            var context = new SpanContext(traceId, parentId);
            var request = new HttpRequestMessage(HttpMethod.Get, "/");
            request.Headers.Inject(context);
            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            _waiter.Wait();

            Assert.Equal(Content, content);
            var span = _writer.Traces.Single().Single();
            Assert.Equal("GET", span.Tags[MethodTag]);
            Assert.Equal("/", span.Tags[UrlTag]);
            Assert.Equal("200", span.Tags[StatusCodeTag]);
            Assert.Equal("GET 200", span.ResourceName);
            Assert.Equal(DefaultServiceName, span.ServiceName);
            Assert.True(span.IsRootSpan);
            Assert.Null(span.Context.ParentId);
            Assert.NotEqual(traceId, span.Context.TraceId);
        }

        [Fact]
        public async void OkResponse_WithContextPropagationEnabled()
        {
            _host.Dispose();
            using (var host = new WebHostBuilder()
                .UseUrls("http://localhost:5050")
                .UseKestrel()
                .ConfigureServices(s => s.AddDatadogTrace(_tracer, enableDistributedTracing: true))
                .Configure(app => app
                    .UseDeveloperExceptionPage()
                    .Run(HandleNormal))
                .Build())
            {
                host.Start();
                const ulong parentId = 7;
                const ulong traceId = 9;
                var context = new SpanContext(traceId, parentId);
                var request = new HttpRequestMessage(HttpMethod.Get, "/");
                request.Headers.Inject(context);
                var response = await _client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                _waiter.Wait();

                Assert.Equal(Content, content);
                var span = _writer.Traces.Single().Single();
                Assert.Equal("GET", span.Tags[MethodTag]);
                Assert.Equal("/", span.Tags[UrlTag]);
                Assert.Equal("200", span.Tags[StatusCodeTag]);
                Assert.Equal("GET 200", span.ResourceName);
                Assert.Equal(DefaultServiceName, span.ServiceName);
                Assert.True(span.IsRootSpan);
                Assert.Equal(parentId, span.Context.ParentId);
                Assert.Equal(traceId, span.Context.TraceId);
            }
        }

        [Fact]
        public async void OkResponseOverrideServiceName()
        {
            const string serviceNameOverride = "Blublu";
            _host.Dispose();
            using (var host = new WebHostBuilder()
                .UseUrls("http://localhost:5050")
                .UseKestrel()
                .ConfigureServices(s => s.AddDatadogTrace(_tracer, serviceNameOverride))
                .Configure(app => app
                    .UseDeveloperExceptionPage()
                    .Run(HandleNormal))
                .Build())
            {
                host.Start();
                var response = await _client.GetAsync("/");
                var content = await response.Content.ReadAsStringAsync();
                _waiter.Wait();

                Assert.Equal(Content, content);
                var span = _writer.Traces.Single().Single();
                Assert.Equal("GET", span.Tags[MethodTag]);
                Assert.Equal("/", span.Tags[UrlTag]);
                Assert.Equal("200", span.Tags[StatusCodeTag]);
                Assert.Equal("GET 200", span.ResourceName);
                Assert.Equal(serviceNameOverride, span.ServiceName);
            }
        }

        [Fact]
        public async void OkResponseWithChildSpan()
        {
            var response = await _client.GetAsync("/child");
            var content = await response.Content.ReadAsStringAsync();
            _waiter.Wait();

            Assert.Equal(Content, content);
            var trace = _writer.Traces.Single();
            Assert.Equal(2, trace.Count);
            var root = trace[0];
            Assert.Equal("GET", root.Tags[MethodTag]);
            Assert.Equal("/child", root.Tags[UrlTag]);
            Assert.Equal("200", root.Tags[StatusCodeTag]);
            Assert.Equal("GET 200", root.ResourceName);
            Assert.Equal(DefaultServiceName, root.ServiceName);
            var child = trace[1];
            Assert.Equal("Child", child.OperationName);
            Assert.Equal(root.Context, child.Context.Parent);
        }

        [Fact]
        public async void Error()
        {
            var response = await _client.GetAsync("/error");
            _waiter.Wait();

            var span = _writer.Traces.Single().Single();
            Assert.Equal("GET", span.Tags[MethodTag]);
            Assert.Equal("/error", span.Tags[UrlTag]);
            Assert.Equal("500", span.Tags[StatusCodeTag]);
            Assert.True(span.Error);
            Assert.Equal(typeof(InvalidOperationException).ToString(), span.GetTag(ErrorTypeTag));
            Assert.Equal("Invalid", span.GetTag(ErrorMsgTag));
            Assert.False(string.IsNullOrEmpty(span.GetTag(ErrorStackTag)));
            Assert.Equal("GET 500", span.ResourceName);
            Assert.Equal(DefaultServiceName, span.ServiceName);
        }

        [Fact]
        public async void MvcOkResponse()
        {
            var response = await _client.GetAsync("/Test");
            var content = await response.Content.ReadAsStringAsync();
            _waiter.Wait();

            Assert.Equal("ActionContent", content);
            var span = _writer.Traces.Single().Single();
            Assert.Equal("GET", span.Tags[MethodTag]);
            Assert.Equal("/Test", span.Tags[UrlTag]);
            Assert.Equal("200", span.Tags[StatusCodeTag]);
            Assert.Equal("Test.Index", span.ResourceName);
            Assert.Equal(DefaultServiceName, span.ServiceName);
        }

        [Fact]
        public async void DeveloperExceptionPage()
        {
            _host.Dispose();
            using (var host = new WebHostBuilder()
                .UseUrls("http://localhost:5050")
                .UseKestrel()
                .ConfigureServices(s => s.AddDatadogTrace(_tracer))
                .Configure(app => app
                    .UseDeveloperExceptionPage()
                    .Map("/error", HandleError))
                .Build())
            {
                host.Start();
                var response = await _client.GetAsync("/error");
                _waiter.Wait();

                var span = _writer.Traces.Single().Single();
                Assert.Equal("GET", span.Tags[MethodTag]);
                Assert.Equal("/error", span.Tags[UrlTag]);
                Assert.Equal("500", span.Tags[StatusCodeTag]);
                Assert.True(span.Error);
                Assert.Equal(typeof(InvalidOperationException).ToString(), span.GetTag(ErrorTypeTag));
                Assert.Equal("Invalid", span.GetTag(ErrorMsgTag));
                Assert.False(string.IsNullOrEmpty(span.GetTag(ErrorStackTag)));
                Assert.Equal("GET 500", span.ResourceName);
                Assert.Equal(DefaultServiceName, span.ServiceName);
            }
        }

        [Fact]
        public async void ExceptionHandler()
        {
            _host.Dispose();
            using (var host = new WebHostBuilder()
                .UseUrls("http://localhost:5050")
                .UseKestrel()
                .ConfigureServices(s => s.AddDatadogTrace(_tracer))
                .Configure(app => app
                    .UseExceptionHandler("/index")
                    .Map("/error", HandleError)
                    .Run(HandleNormal))
                .Build())
            {
                host.Start();
                var response = await _client.GetAsync("/error");
                _waiter.Wait();

                var span = _writer.Traces.Single().Single();
                Assert.Equal("GET", span.Tags[MethodTag]);
                Assert.Equal("/error", span.Tags[UrlTag]);
                Assert.Equal("500", span.Tags[StatusCodeTag]);
                Assert.True(span.Error);
                Assert.Equal(typeof(InvalidOperationException).ToString(), span.GetTag(ErrorTypeTag));
                Assert.Equal("Invalid", span.GetTag(ErrorMsgTag));
                Assert.False(string.IsNullOrEmpty(span.GetTag(ErrorStackTag)));
                Assert.Equal("GET 500", span.ResourceName);
                Assert.Equal(DefaultServiceName, span.ServiceName);
            }
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
