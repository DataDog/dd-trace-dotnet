#if NET45

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// The ASP.NET MVC 5 integration.
    /// </summary>
    internal sealed class AspNetMvc5Integration : IDisposable
    {
        private const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations.AspNetMvc5Integration";
        private static readonly Type ContollerContextType;

        private readonly HttpContextBase _httpContext;
        private readonly Scope _scope;

        static AspNetMvc5Integration()
        {
            try
            {
                Assembly assembly = Assembly.Load("System.Web.Mvc");
                ContollerContextType = assembly.GetType("System.Web.Mvc.ControllerContext", throwOnError: false);
            }
            catch
            {
                ContollerContextType = null;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetMvc5Integration"/> class.
        /// </summary>
        /// <param name="controllerContextObj">An array with all the arguments that were passed into the instrumented method. If it is an instance method, the first arguments is <c>this</c>.</param>
        public AspNetMvc5Integration(object controllerContextObj)
        {
            if (controllerContextObj == null || ContollerContextType == null)
            {
                // bail out early
                return;
            }

            try
            {
                if (controllerContextObj.GetType() != ContollerContextType)
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

                string httpMethod = _httpContext.Request.HttpMethod.ToUpperInvariant();

                IDictionary<string, object> routeValues = controllerContext.RouteData.Values;
                string controllerName = routeValues.GetValueOrDefault("controller") as string;
                string actionName = routeValues.GetValueOrDefault("action") as string;
                string resourceName = $"{controllerName}.{actionName}()";

                _scope = Tracer.Instance.StartActive(OperationNames.AspNetMvcRequest);
                Span span = _scope.Span;
                span.Type = SpanTypes.Web;
                span.ResourceName = resourceName;
                span.SetTag(Tags.HttpMethod, httpMethod);
                span.SetTag(Tags.HttpUrl, _httpContext.Request.RawUrl.ToLowerInvariant());
                span.SetTag(Tags.AspNetRoute, (string)controllerContext.RouteData.Route.Url);
                span.SetTag(Tags.AspNetController, controllerName);
                span.SetTag(Tags.AspNetAction, actionName);

                _scope = Tracer.Instance.ActivateSpan(span, finishOnClose: true);
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
        public static object BeginInvokeAction(
            dynamic asyncControllerActionInvoker,
            dynamic controllerContext,
            dynamic actionName,
            dynamic callback,
            dynamic state)
        {
            AspNetMvc5Integration integration = null;

            try
            {
                if (Instrumentation.Enabled && HttpContext.Current != null)
                {
                    integration = new AspNetMvc5Integration((object)controllerContext);
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
        public static bool EndInvokeAction(dynamic asyncControllerActionInvoker, dynamic asyncResult)
        {
            AspNetMvc5Integration integration = null;

            try
            {
                if (Instrumentation.Enabled && HttpContext.Current != null)
                {
                    integration = HttpContext.Current?.Items[HttpContextKey] as AspNetMvc5Integration;
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

#endif
