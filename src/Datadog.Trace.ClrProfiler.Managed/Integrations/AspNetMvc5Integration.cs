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
    public sealed class AspNetMvc5Integration : Integration
    {
        private readonly dynamic _controllerContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetMvc5Integration"/> class.
        /// </summary>
        /// <param name="args">An array with all the arguments that were passed into the instrumented method. If it is an instance method, the first arguments is <c>this</c>.</param>
        public AspNetMvc5Integration(object[] args)
        {
            if (args.Length < 2 || args[1].GetType().FullName != "System.Web.Mvc.ControllerContext")
            {
                return;
            }

            // [System.Web.Mvc]System.Web.Mvc.ControllerContext
            _controllerContext = args[1];

            HttpContextBase httpContext = _controllerContext.HttpContext;
            string httpMethod = httpContext.Request.HttpMethod.ToUpperInvariant();

            string routeTemplate = _controllerContext.RouteData.Route.Url;
            IDictionary<string, object> routeValues = _controllerContext.RouteData.Values;
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
            Span span = Scope.Span;
            span.Type = "web";
            span.OperationName = "web.request";
            span.ResourceName = string.Join(" ", httpMethod, resourceName.ToString());
            span.SetTag("http.method", httpMethod);
            span.SetTag("http.url", httpContext.Request.RawUrl.ToLowerInvariant());
            span.SetTag("http.route", routeTemplate);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                HttpContextBase httpContext = _controllerContext?.HttpContext;

                if (httpContext != null)
                {
                    Scope.Span.SetTag("http.status_code", httpContext.Response.StatusCode.ToString());
                }
            }

            base.Dispose(disposing);
        }
    }
}
