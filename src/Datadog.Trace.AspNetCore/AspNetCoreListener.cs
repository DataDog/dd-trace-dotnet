using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DiagnosticAdapter;

namespace Datadog.Trace.AspNetCore
{
    internal class AspNetCoreListener : IDisposable
    {
        private readonly DiagnosticListener _listener;
        private readonly Tracer _tracer;
        private IDisposable _subscription;

        public AspNetCoreListener(DiagnosticListener listener, Tracer tracer)
        {
            _listener = listener;
            _tracer = tracer;
        }

        public void Listen()
        {
            _subscription = _listener.SubscribeWithAdapter(this);
        }

        public void Dispose()
        {
            if (_subscription != null)
            {
                _subscription.Dispose();
            }
        }

        // This is needed to enable the Activity logging in Asp.Net DiagnosticSource
        // If it's not present the other events are not writen
        [DiagnosticName("Microsoft.AspNetCore.Hosting.HttpRequestIn")]
        public void OnHttpRequestIn()
        {
        }

        [DiagnosticName("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start")]
        public void OnHttpRequestInStart(HttpContext httpContext)
        {
            var span = _tracer.StartActive("aspnet.request").Span;
            span.Type = "web";
            span.SetTag(Tags.HttpMethod, httpContext.Request.Method);
            span.SetTag(Tags.HttpUrl, httpContext.Request.Path);
        }

        [DiagnosticName("Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop")]
        public void OnHttpRequestInStop(HttpContext httpContext)
        {
            var scope = _tracer.ActiveScope;
            if (scope == null)
            {
                return;
            }

            scope.Span.SetTag(Tags.HttpStatusCode, httpContext.Response.StatusCode.ToString());
            scope.Dispose();
        }

        [DiagnosticName("Microsoft.AspNetCore.Hosting.UnhandledException")]
        public void OnUnhandledException(HttpContext httpContext, Exception exception)
        {
            var scope = _tracer.ActiveScope;
            if (scope == null)
            {
                return;
            }

            scope.Span.SetException(exception);
        }
    }
}