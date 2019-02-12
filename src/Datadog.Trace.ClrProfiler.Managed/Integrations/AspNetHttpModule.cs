#if !NETSTANDARD2_0
using System;
using System.Web;
using Datadog.Trace.ClrProfiler.ExtensionMethods;
using Datadog.Trace.ClrProfiler.Models;
using Datadog.Trace.ClrProfiler.Services;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <inheritdoc />
    /// <summary>
    ///     IHttpModule used to trace within an ASP.NET HttpApplication request
    /// </summary>
    public abstract class AspNetHttpModule : IHttpModule
    {
        private const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations.AspNetHttpModule";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(AspNetHttpModule));

        private readonly string _operationName;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetHttpModule"/> class.
        /// </summary>
        /// <param name="operationName">The operation name to be used for the trace/span data generated</param>
        protected AspNetHttpModule(string operationName)
        {
            _operationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
        }

        /// <inheritdoc />
        public void Init(HttpApplication context)
        {
            if (!Instrumentation.ProfilerAttached)
            {
#if DEBUG
                context.Context.AddError(new ApplicationException("Datadog Profiler not attached to current process."));
#endif

                Log.Warn("Datadog ASP.NET HttpModule Profiler not attached");

                return;
            }

            context.BeginRequest += OnBeginRequest;
            context.EndRequest += OnEndRequest;
            context.Error += OnError;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Nothing to do...
        }

        private void OnBeginRequest(object sender, EventArgs eventArgs)
        {
            Scope scope = null;

            try
            {
                if (!TryGetContext(sender, out var httpContext))
                {
                    return;
                }

                scope = Tracer.Instance.StartActive(_operationName);

                var contextAdapter = HttpContextTagAdapter.Create(httpContext);

                var decorator = DefaultSpanDecorationBuilder.Create()
                                                            .With(contextAdapter.AllWebSpanDecorator())
                                                            .With(contextAdapter.ResourceNameDecorator())
                                                            .Build();

                scope.Span.DecorateWith(decorator);

                httpContext.Items[HttpContextKey] = scope;
            }
            catch (Exception ex)
            {
                // Dispose here, as the scope won't be in context items and won't get disposed on request end in that case...
                scope?.TryDispose();

                Log.ErrorException("Datadog ASP.NET HttpModule instrumentation error", ex);
            }
        }

        private void OnEndRequest(object sender, EventArgs eventArgs)
        {
            try
            {
                if (!TryGetContext(sender, out var httpContext) ||
                    !httpContext.Items.TryGetValueOrDefaultAs<Scope>(HttpContextKey, out var scope))
                {
                    return;
                }

                try
                {
                    scope?.Span?.SetTag(Tags.HttpStatusCode, httpContext.Response.StatusCode.ToString());
                }
                finally
                {
                    scope?.TryDispose();
                }
            }
            catch (Exception ex)
            {
                Log.ErrorException("Datadog ASP.NET HttpModule instrumentation error", ex);
            }
        }

        private void OnError(object sender, EventArgs eventArgs)
        {
            try
            {
                if (!TryGetContext(sender, out var httpContext) || httpContext.Error == null ||
                    !httpContext.Items.TryGetValueOrDefaultAs<Scope>(HttpContextKey, out var scope))
                {
                    return;
                }

                scope?.Span?.SetException(httpContext.Error);
            }
            catch (Exception ex)
            {
                Log.ErrorException("Datadog ASP.NET HttpModule instrumentation error", ex);
            }
        }

        private bool TryGetContext(object sender, out HttpContext httpContext)
        {
            if (sender == null || !(sender is HttpApplication httpApp) || httpApp?.Context?.Items == null)
            {
                httpContext = null;

                return false;
            }

            httpContext = httpApp.Context;

            return true;
        }
    }
}

#endif
