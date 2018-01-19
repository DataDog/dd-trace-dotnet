using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DiagnosticAdapter;

namespace Datadog.Trace.AspNetCore
{
    internal class AspNetCoreListener : IDisposable
    {
        private static readonly object ScopeKey = new object();
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

        [DiagnosticName("Microsoft.AspNetCore.Hosting.BeginRequest")]
        public void OnBeginRequest(HttpContext httpContext)
        {
            var scope = _tracer.StartActive("aspnet.request");

            // The scope is stored here to reduce the risk of getting the wrong active scope later
            // because of an interference with other instrumentations
            httpContext.Items[ScopeKey] = scope;
            scope.Span.Type = "web";
            scope.Span.SetTag(Tags.HttpMethod, httpContext.Request.Method);
            scope.Span.SetTag(Tags.HttpUrl, httpContext.Request.Path);
        }

        [DiagnosticName("Microsoft.AspNetCore.Hosting.EndRequest")]
        public void OnEndRequest(HttpContext httpContext)
        {
            httpContext.Items.TryGetValue(ScopeKey, out object value);
            var scope = value as Scope;
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
            httpContext.Items.TryGetValue(ScopeKey, out object value);
            var scope = value as Scope;
            if (scope == null)
            {
                return;
            }

            scope.Span.SetException(exception);
            OnEndRequest(httpContext);
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.BeforeAction")]
        public void OnBeforeAction(HttpContext httpContext, RouteData routeData)
        {
            httpContext.Items.TryGetValue(ScopeKey, out object value);
            var scope = value as Scope;
            if (scope == null)
            {
                return;
            }

            routeData.Values.TryGetValue("controller", out object controllerObject);
            routeData.Values.TryGetValue("action", out object actionObject);
            var controller = controllerObject as string;
            controller = controller ?? "UnknownController";
            var action = actionObject as string;
            action = action ?? "UnknownAction";
            scope.Span.ResourceName = $"{controller}.{action}";
       }
    }
}