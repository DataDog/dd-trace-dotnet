using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Datadog.Trace.ClrProfiler.Emit;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// The ASP.NET Core MVC 2 integration.
    /// </summary>
    public sealed class AspNetCoreMvc2Integration : IDisposable
    {
        internal const string OperationName = "aspnet-core-mvc.request";
        private const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations." + nameof(AspNetCoreMvc2Integration);

        private static Action<object, object, object, object> _beforeAction;
        private static Action<object, object, object, object> _afterAction;

        private readonly dynamic _httpContext;
        private readonly Scope _scope;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetCoreMvc2Integration"/> class.
        /// </summary>
        /// <param name="actionDescriptorObj">An ActionDescriptor with information about the current action.</param>
        /// <param name="httpContextObj">The HttpContext for the current request.</param>
        public AspNetCoreMvc2Integration(object actionDescriptorObj, object httpContextObj)
        {
            try
            {
                dynamic actionDescriptor = actionDescriptorObj;
                string controllerName = (actionDescriptor.ControllerName as string)?.ToLowerInvariant();
                string actionName = (actionDescriptor.ActionName as string)?.ToLowerInvariant();

                _httpContext = httpContextObj;
                string httpMethod = _httpContext.Request.Method.ToUpperInvariant();
                string url = GetDisplayUrl(_httpContext.Request).ToLowerInvariant();

                _scope = Tracer.Instance.StartActive(OperationName);
                Span span = _scope.Span;
                span.Type = SpanTypes.Web;
                span.ResourceName = $"{httpMethod} {controllerName}.{actionName}";
                span.SetTag(Tags.HttpMethod, httpMethod);
                span.SetTag(Tags.HttpUrl, url);
                span.SetTag(Tags.AspNetController, controllerName);
                span.SetTag(Tags.AspNetAction, actionName);
            }
            catch
            {
                // TODO: logging
            }
        }

        /// <summary>
        /// Wrapper method used to instrument Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions.BeforeAction()
        /// </summary>
        /// <param name="diagnosticSource">The DiagnosticSource that this extension method was called on.</param>
        /// <param name="actionDescriptor">An ActionDescriptor with information about the current action.</param>
        /// <param name="httpContext">The HttpContext for the current request.</param>
        /// <param name="routeData">A RouteData with information about the current route.</param>
        [InterceptMethod(
            CallerAssembly = "Microsoft.AspNetCore.Mvc.Core",
            TargetAssembly = "Microsoft.AspNetCore.Mvc.Core",
            TargetType = "Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions")]
        public static void BeforeAction(
            object diagnosticSource,
            object actionDescriptor,
            dynamic httpContext,
            object routeData)
        {
            AspNetCoreMvc2Integration integration = null;

            try
            {
                integration = new AspNetCoreMvc2Integration(actionDescriptor, httpContext);
                IDictionary<object, object> contextItems = httpContext.Items;
                contextItems[HttpContextKey] = integration;
            }
            catch
            {
                // TODO: log this as an instrumentation error, but continue calling instrumented method
            }

            try
            {
                if (_beforeAction == null)
                {
                    Assembly assembly = actionDescriptor.GetType().GetTypeInfo().Assembly;
                    Type type = assembly.GetType("Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions");

                    _beforeAction = DynamicMethodBuilder<Action<object, object, object, object>>.CreateMethodCallDelegate(
                        type,
                        "BeforeAction");
                }
            }
            catch
            {
                // TODO: log this as an instrumentation error, we cannot call instrumented method,
                // profiled app will continue working without DiagnosticSource
            }

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                _beforeAction?.Invoke(diagnosticSource, actionDescriptor, httpContext, routeData);
            }
            catch (Exception ex)
            {
                integration?.SetException(ex);
                throw;
            }
        }

        /// <summary>
        /// Wrapper method used to instrument Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions.AfterAction()
        /// </summary>
        /// <param name="diagnosticSource">The DiagnosticSource that this extension method was called on.</param>
        /// <param name="actionDescriptor">An ActionDescriptor with information about the current action.</param>
        /// <param name="httpContext">The HttpContext for the current request.</param>
        /// <param name="routeData">A RouteData with information about the current route.</param>
        [InterceptMethod(
            CallerAssembly = "Microsoft.AspNetCore.Mvc.Core",
            TargetAssembly = "Microsoft.AspNetCore.Mvc.Core",
            TargetType = "Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions")]
        public static void AfterAction(
            object diagnosticSource,
            object actionDescriptor,
            dynamic httpContext,
            object routeData)
        {
            AspNetCoreMvc2Integration integration = null;

            try
            {
                IDictionary<object, object> contextItems = httpContext?.Items;
                integration = contextItems?[HttpContextKey] as AspNetCoreMvc2Integration;
            }
            catch
            {
                // TODO: log this as an instrumentation error, but continue calling instrumented method
            }

            try
            {
                if (_afterAction == null)
                {
                    Type type = actionDescriptor.GetType().Assembly.GetType("Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions");

                    _afterAction = DynamicMethodBuilder<Action<object, object, object, object>>.CreateMethodCallDelegate(
                        type,
                        "AfterAction");
                }
            }
            catch
            {
                // TODO: log this as an instrumentation error, we cannot call instrumented method,
                // profiled app will continue working without DiagnosticSource
            }

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                _afterAction?.Invoke(diagnosticSource, actionDescriptor, httpContext, routeData);
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

        private static string GetDisplayUrl(dynamic request)
        {
            string host = request.Host.Value;
            string pathBase = request.PathBase.Value;
            string path = request.Path.Value;
            string queryString = request.QueryString.Value;

            return new StringBuilder(request.Scheme.Length + "://".Length + host.Length + pathBase.Length + path.Length + queryString.Length).Append(request.Scheme)
                                                                                                                                             .Append("://")
                                                                                                                                             .Append(host)
                                                                                                                                             .Append(pathBase)
                                                                                                                                             .Append(path)
                                                                                                                                             .Append(queryString)
                                                                                                                                             .ToString();
        }
    }
}
