using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MessagePack;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class MockTracerAgent : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly SpanCollector _collector;

        public MockTracerAgent()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:9696/");
            _listener.Start();
            _collector = new SpanCollector(_listener);

            Environment.SetEnvironmentVariable("DD_TRACE_AGENT_HOSTNAME", "localhost");
            Environment.SetEnvironmentVariable("DD_TRACE_AGENT_PORT", "9696");
        }

        public List<Span> GetSpans()
        {
            return _collector.GetSpans();
        }

        public void Dispose()
        {
            _listener.Stop();
            _collector.Wait();
        }

        public struct Span
        {
            public ulong TraceId { get; set; }

            public ulong SpanId { get; set; }

            public string Name { get; set; }

            public string Resource { get; set; }

            public string Service { get; set; }

            public string Type { get; set; }

            public ulong Start { get; set; }

            public ulong Duration { get; set; }

            public Dictionary<string, string> Tags { get; set; }
        }

        private class SpanCollector
        {
            private readonly List<Span> _spans = new List<Span>();
            private readonly Task _task;

            public SpanCollector(HttpListener listener)
            {
                _task = new Task(() => Handle(listener));
                _task.Start();
            }

            public List<Span> GetSpans()
            {
                lock (this)
                {
                    return new List<Span>(_spans);
                }
            }

            public void Wait()
            {
                _task.Wait();
            }

            private static List<Span> ToSpans(dynamic data)
            {
                if (data is IDictionary dict)
                {
                    var span = new Span
                    {
                        TraceId = dict.Get<ulong>("trace_id"),
                        SpanId = dict.Get<ulong>("span_id"),
                        Name = dict.Get<string>("name"),
                        Resource = dict.Get<string>("resource"),
                        Service = dict.Get<string>("service"),
                        Type = dict.Get<string>("type"),
                        Start = dict.Get<ulong>("start"),
                        Duration = dict.Get<ulong>("duration"),
                        Tags = dict.Get<Dictionary<string, string>>("meta"),
                    };
                    return new List<Span> { span };
                }

                if (data is IEnumerable rawSpans)
                {
                    var allSpans = new List<Span>();

                    foreach (var rawSpan in rawSpans)
                    {
                        allSpans.AddRange(ToSpans(rawSpan));
                    }

                    return allSpans;
                }

                return new List<Span>();
            }

            private void Handle(HttpListener listener)
            {
                while (true)
                {
                    try
                    {
                        var ctx = listener.GetContext();
                        try
                        {
                            var rawSpans = MessagePackSerializer.Deserialize<dynamic>(ctx.Request.InputStream);
                            var spans = ToSpans(rawSpans);
                            lock (this)
                            {
                                _spans.AddRange(spans);
                            }

                            ctx.Response.ContentType = "application/json";
                            var buffer = Encoding.UTF8.GetBytes("{}");
                            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
                            ctx.Response.Close();
                        }
                        catch
                        {
                        }
                    }
                    catch
                    {
                        return;
                    }
                }
            }
        }
    }
}
