#if !NETSTANDARD2_0

using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Routing;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// The ASP.NET MVC integration.
    /// </summary>
    public sealed class AspNetMvcIntegration : IDisposable
    {
        internal const string OperationName = "aspnet-mvc.request";
        private const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations.AspNetMvcIntegration";

        private static readonly Type ControllerContextType = Type.GetType("System.Web.Mvc.ControllerContext, System.Web.Mvc", throwOnError: false);
        private static readonly Type RouteCollectionRouteType = Type.GetType("System.Web.Mvc.Routing.RouteCollectionRoute, System.Web.Mvc", throwOnError: false);

        private readonly HttpContextBase _httpContext;
        private readonly Scope _scope;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetMvcIntegration"/> class.
        /// </summary>
        /// <param name="controllerContextObj">The System.Web.Mvc.ControllerContext that was passed as an argument to the instrumented method.</param>
        public AspNetMvcIntegration(object controllerContextObj)
        {
            if (controllerContextObj == null || ControllerContextType == null)
            {
                // bail out early
                return;
            }

            try
            {
                if (controllerContextObj.GetType() != ControllerContextType)
                {
                    return;
                }

                // access the controller context without referencing System.Web.Mvc directly
                dynamic controllerContext = controllerContextObj;

                _httpContext = controllerContext.HttpContext;

                if (_httpContext == null)
                {
                    return;
                }

                string host = _httpContext.Request.Headers.Get("Host");
                string httpMethod = _httpContext.Request.HttpMethod.ToUpperInvariant();
                string url = _httpContext.Request.RawUrl.ToLowerInvariant();

                RouteData routeData = controllerContext.RouteData as RouteData;
                Route route = routeData?.Route as Route;
                RouteValueDictionary routeValues = routeData?.Values;

                if (route == null && routeData?.Route.GetType() == RouteCollectionRouteType)
                {
                    var routeMatches = routeValues?.GetValueOrDefault("MS_DirectRouteMatches") as List<RouteData>;

                    if (routeMatches?.Count > 0)
                    {
                        // route was defined using attribute routing i.e. [Route("/path/{id}")]
                        // get route and routeValues from the RouteData in routeMatches
                        route = routeMatches[0].Route as Route;
                        routeValues = routeMatches[0].Values;
                    }
                }

                string controllerName = (routeValues?.GetValueOrDefault("controller") as string)?.ToLowerInvariant();
                string actionName = (routeValues?.GetValueOrDefault("action") as string)?.ToLowerInvariant();
                string resourceName = $"{httpMethod} {controllerName}.{actionName}";

                _scope = Tracer.Instance.StartActive(OperationName);
                Span span = _scope.Span;
                span.Type = SpanTypes.Web;
                span.ResourceName = resourceName;
                span.SetTag(Tags.HttpRequestHeadersHost, host);
                span.SetTag(Tags.HttpMethod, httpMethod);
                span.SetTag(Tags.HttpUrl, url);
                span.SetTag(Tags.AspNetRoute, route?.Url);
                span.SetTag(Tags.AspNetController, controllerName);
                span.SetTag(Tags.AspNetAction, actionName);
            }
            catch
            {
                // TODO: logging
            }
        }

        /// <summary>
        /// Wrapper method used to instrument System.Web.Mvc.Async.AsyncControllerActionInvoker.BeginInvokeAction().
        /// </summary>
        /// <param name="asyncControllerActionInvoker">The AsyncControllerActionInvoker instance.</param>
        /// <param name="controllerContext">The ControllerContext for the current request.</param>
        /// <param name="actionName">The name of the controller action.</param>
        /// <param name="callback">An <see cref="AsyncCallback"/> delegate.</param>
        /// <param name="state">An object that holds the state of the async operation.</param>
        /// <returns>Returns the <see cref="IAsyncResult "/> returned by the original BeginInvokeAction() that is later passed to <see cref="EndInvokeAction"/>.</returns>
        [InterceptMethod(
            CallerAssembly = "System.Web.Mvc",
            TargetAssembly = "System.Web.Mvc",
            TargetType = "System.Web.Mvc.Async.IAsyncActionInvoker")]
        public static object BeginInvokeAction(
            dynamic asyncControllerActionInvoker,
            dynamic controllerContext,
            dynamic actionName,
            dynamic callback,
            dynamic state)
        {
            AspNetMvcIntegration integration = null;

            try
            {
                if (HttpContext.Current != null)
                {
                    integration = new AspNetMvcIntegration((object)controllerContext);
                    HttpContext.Current.Items[HttpContextKey] = integration;
                }
            }
            catch
            {
                // TODO: log this as an instrumentation error, but continue calling instrumented method
            }

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                return asyncControllerActionInvoker.BeginInvokeAction(controllerContext, actionName, callback, state);
            }
            catch (Exception ex)
            {
                integration?.SetException(ex);
                throw;
            }
        }

        /// <summary>
        /// Wrapper method used to instrument System.Web.Mvc.Async.AsyncControllerActionInvoker.EndInvokeAction().
        /// </summary>
        /// <param name="asyncControllerActionInvoker">The AsyncControllerActionInvoker instance.</param>
        /// <param name="asyncResult">The <see cref="IAsyncResult"/> returned by <see cref="BeginInvokeAction"/>.</param>
        /// <returns>Returns the <see cref="bool"/> returned by the original EndInvokeAction().</returns>
        [InterceptMethod(
            CallerAssembly = "System.Web.Mvc",
            TargetAssembly = "System.Web.Mvc",
            TargetType = "System.Web.Mvc.Async.IAsyncActionInvoker")]
        public static bool EndInvokeAction(dynamic asyncControllerActionInvoker, dynamic asyncResult)
        {
            AspNetMvcIntegration integration = null;

            try
            {
                if (HttpContext.Current != null)
                {
                    integration = HttpContext.Current?.Items[HttpContextKey] as AspNetMvcIntegration;
                }
            }
            catch
            {
                // TODO: log this as an instrumentation error, but continue calling instrumented method
            }

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                return asyncControllerActionInvoker.EndInvokeAction(asyncResult);
            }
            catch (Exception ex)
            {
                integration?.SetException(ex);
                throw;
            }
            finally
            {
                integration?.Dispose();
            }
        }

        /// <summary>
        /// Tags the current span as an error. Called when an unhandled exception is thrown in the instrumented method.
        /// </summary>
        /// <param name="ex">The exception that was thrown and not handled in the instrumented method.</param>
        public void SetException(Exception ex)
        {
            _scope?.Span?.SetException(ex);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            try
            {
                // sometimes, if an exception was unhandled in user code, status code is set to 500 later in the pipeline,
                // so it is still 200 here. if there was an unhandled exception, always set status code to 500.
                if (_scope?.Span?.Error == true)
                {
                    _scope?.Span?.SetTag(Tags.HttpStatusCode, "500");
                }
                else if (_httpContext != null)
                {
                    _scope?.Span?.SetTag(Tags.HttpStatusCode, _httpContext.Response.StatusCode.ToString());
                }
            }
            finally
            {
                _scope?.Dispose();
            }
        }
    }
}

#endif
