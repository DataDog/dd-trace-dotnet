using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// The ASP.NET MVC 5 integration.
    /// </summary>
    public sealed class AspNetMvc5Integration : IDisposable
    {
        private const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations.AspNetMvc5Integration";
        private readonly HttpContextBase _httpContext;
        private readonly Scope _scope;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetMvc5Integration"/> class.
        /// </summary>
        /// <param name="controllerContextObj">An array with all the arguments that were passed into the instrumented method. If it is an instance method, the first arguments is <c>this</c>.</param>
        public AspNetMvc5Integration(object controllerContextObj)
        {
            if (!Instrumentation.Enabled)
            {
                return;
            }

            try
            {
                if (controllerContextObj?.GetType().FullName != "System.Web.Mvc.ControllerContext")
                {
                    return;
                }

                // access the controller context without referencing System.Web.Mvc directly
                dynamic controllerContext = controllerContextObj;

                _httpContext = controllerContext.HttpContext;
                string httpMethod = _httpContext.Request.HttpMethod.ToUpperInvariant();

                string routeTemplate = controllerContext.RouteData.Route.Url;
                IDictionary<string, object> routeValues = controllerContext.RouteData.Values;
                var resourceName = new StringBuilder(routeTemplate);

                // replace all route values except "id"
                // TODO: make this filter configurable
                foreach (var routeValue in routeValues.Where(p => !string.Equals(p.Key, "id", StringComparison.InvariantCultureIgnoreCase)))
                {
                    string key = $"{{{routeValue.Key.ToLowerInvariant()}}}";
                    string value = routeValue.Value.ToString().ToLowerInvariant();
                    resourceName.Replace(key, value);
                }

                // TODO: define constants elsewhere instead of using magic strings
                Span span = Tracer.Instance.StartSpan("web.request");
                span.Type = "web";
                span.ResourceName = string.Join(" ", httpMethod, resourceName.ToString());
                span.SetTag("http.method", httpMethod);
                span.SetTag("http.url", _httpContext.Request.RawUrl.ToLowerInvariant());
                span.SetTag("http.route", routeTemplate);

                _httpContext.Items[HttpContextKey] = this;
                _scope = Tracer.Instance.ActivateSpan(span, finishOnClose: true);
            }
            catch
            {
                // TODO: logging
            }
        }

        public void RegisterException(Exception ex)
        {
            Span span = _scope?.Span;

            if (span != null)
            {
                span.Error = true;
                span.SetTag("exception.message", ex.Message);
                // TODO: log the exception
            }
        }

        public static object BeginInvokeAction(
            dynamic asyncControllerActionInvoker,
            dynamic controllerContext,
            dynamic actionName,
            dynamic callback,
            dynamic state)
        {
            var integration = new AspNetMvc5Integration((object)controllerContext);

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                return asyncControllerActionInvoker.BeginInvokeAction(controllerContext, actionName, callback, state);
            }
            catch (Exception ex)
            {
                integration.RegisterException(ex);
                throw;
            }
        }

        public static bool EndInvokeAction(dynamic asyncControllerActionInvoker, dynamic asyncResult)
        {
            var integration = HttpContext.Current?.Items[HttpContextKey] as AspNetMvc5Integration;

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                return asyncControllerActionInvoker.EndInvokeAction(asyncResult);
            }
            catch (Exception ex)
            {
                integration?.RegisterException(ex);
                throw;
            }
            finally
            {
                integration?.Dispose();
            }
        }

        public void Dispose()
        {
            try
            {
                if (_httpContext != null)
                {
                    _scope?.Span?.SetTag("http.status_code", _httpContext.Response.StatusCode.ToString());
                }
            }
            finally
            {
                _scope?.Dispose();
            }
        }
    }
}
