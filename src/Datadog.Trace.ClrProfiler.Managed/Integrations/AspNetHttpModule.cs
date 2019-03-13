#if !NETSTANDARD2_0

using System;
using System.Diagnostics.CodeAnalysis;
using System.Web;
using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.ClrProfiler.Models;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <inheritdoc />
    /// <summary>
    ///     IHttpModule used to trace within an ASP.NET HttpApplication request
    /// </summary>
    public class AspNetHttpModule : IHttpModule
    {
        private static readonly ILog Log = LogProvider.GetLogger(typeof(AspNetHttpModule));

        private readonly string _httpContextDelegateKey;
        private readonly string _operationName;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AspNetHttpModule" /> class.
        /// </summary>
        public AspNetHttpModule()
            : this("aspnet.request")
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="AspNetHttpModule" /> class.
        /// </summary>
        /// <param name="operationName">The operation name to be used for the trace/span data generated</param>
        public AspNetHttpModule(string operationName)
        {
            _operationName = operationName ?? throw new ArgumentNullException(nameof(operationName));

            _httpContextDelegateKey = string.Concat("__Datadog.Trace.ClrProfiler.Integrations.AspNetHttpModule-", _operationName);
        }

        /// <inheritdoc />
        public void Init(HttpApplication httpApplication)
        {
            httpApplication.BeginRequest += OnBeginRequest;
            httpApplication.EndRequest += OnEndRequest;
            httpApplication.Error += OnError;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Nothing to do...
        }

        private void OnBeginRequest(object sender, EventArgs eventArgs)
        {
            var scopeActive = false;

            Scope scope = null;

            try
            {
                if (!TryGetContext(sender, out var httpContext))
                {
                    return;
                }

                SpanContext propagatedContext = null;

                try
                {
                    // extract propagated http headers
                    var headers = httpContext.Request.Headers.Wrap();
                    propagatedContext = SpanContextPropagator.Instance.Extract(headers);
                }
                catch (Exception ex)
                {
                    Log.ErrorException("Error extracting propagated HTTP headers.", ex);
                }

                scope = Tracer.Instance.StartActive(_operationName, propagatedContext);

                scopeActive = true;

                httpContext.Items[_httpContextDelegateKey] = HttpContextSpanIntegrationDelegate.CreateAndBegin(httpContext, scope);
            }
            catch (Exception ex)
            {
                // Dispose here, as the scope won't be in context items and won't get disposed on request end in that case...
                if (scopeActive)
                {
                    scope.Dispose();
                }

                Log.ErrorException("Datadog ASP.NET HttpModule instrumentation error", ex);
            }
        }

        private void OnEndRequest(object sender, EventArgs eventArgs)
        {
            try
            {
                if (!TryGetContext(sender, out var httpContext) ||
                    !httpContext.Items.TryGetValueOrDefaultAs<ISpanIntegrationDelegate>(_httpContextDelegateKey, out var integrationDelegate))
                {
                    return;
                }

                integrationDelegate.OnEnd();
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
                    !httpContext.Items.TryGetValueOrDefaultAs<ISpanIntegrationDelegate>(_httpContextDelegateKey, out var integrationDelegate))
                {
                    return;
                }

                integrationDelegate.OnError();
            }
            catch (Exception ex)
            {
                Log.ErrorException("Datadog ASP.NET HttpModule instrumentation error", ex);
            }
        }

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1119:StatementMustNotUseUnnecessaryParenthesis", Justification = "Actually Needed")]
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
