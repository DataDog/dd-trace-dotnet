using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.ClrProfiler.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// The ASP.NET Core MVC 2 integration.
    /// </summary>
    public sealed class AspNetCoreMvc2Integration : IDisposable
    {
        private const string IntegrationName = "AspNetCoreMvc2";
        private const string OperationName = "aspnet-coremvc.request";
        private const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations." + nameof(AspNetCoreMvc2Integration);
        private const string Major2 = "2";

        /// <summary>
        /// Base type used for traversing the pipeline in Microsoft.AspNetCore.Mvc.Core.
        /// </summary>
        private const string ResourceInvoker = "Microsoft.AspNetCore.Mvc.Internal.ResourceInvoker";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(AspNetCoreMvc2Integration));

        private static readonly InterceptedMethodAccess<Action<object>> RethrowAccess = new InterceptedMethodAccess<Action<object>>();

        private static Action<object, object, object, object> _beforeAction;
        private static Action<object, object, object, object> _afterAction;

        private readonly object _httpContext;
        private readonly Scope _scope;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetCoreMvc2Integration"/> class.
        /// </summary>
        /// <param name="actionDescriptor">An ActionDescriptor with information about the current action.</param>
        /// <param name="httpContext">The HttpContext for the current request.</param>
        public AspNetCoreMvc2Integration(object actionDescriptor, object httpContext)
        {
            try
            {
                _httpContext = httpContext;
                string httpMethod = null;
                string resourceName = null;
                string host = null;
                string url = null;

                if (actionDescriptor.TryGetPropertyValue("ControllerName", out string controllerName))
                {
                    controllerName = controllerName?.ToLowerInvariant();
                }

                if (actionDescriptor.TryGetPropertyValue("ActionName", out string actionName))
                {
                    actionName = actionName?.ToLowerInvariant();
                }

                if (_httpContext.TryGetPropertyValue("Request", out object request) &&
                    request.TryGetPropertyValue("Method", out httpMethod))
                {
                    httpMethod = httpMethod?.ToUpperInvariant();
                }

                if (httpMethod == null)
                {
                    httpMethod = "UNKNOWN";
                }

                GetTagValuesFromRequest(request, out host, out resourceName, out url);
                SpanContext propagatedContext = null;
                var tracer = Tracer.Instance;

                if (tracer.ActiveScope == null)
                {
                    try
                    {
                        // extract propagated http headers
                        if (request.TryGetPropertyValue("Headers", out IEnumerable requestHeaders))
                        {
                            var headersCollection = new DictionaryHeadersCollection();

                            foreach (object header in requestHeaders)
                            {
                                if (header.TryGetPropertyValue("Key", out string key) &&
                                    header.TryGetPropertyValue("Value", out IList<string> values))
                                {
                                    headersCollection.Add(key, values);
                                }
                            }

                            propagatedContext = SpanContextPropagator.Instance.Extract(headersCollection);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorException("Error extracting propagated HTTP headers.", ex);
                    }
                }

                _scope = tracer.StartActive(OperationName, propagatedContext);
                var span = _scope.Span;

                if (string.IsNullOrEmpty(resourceName))
                {
                    // a legacy fail safe to be removed
                    resourceName = $"{httpMethod} {controllerName}.{actionName}";
                }

                span.DecorateWebSpan(
                    resourceName: resourceName,
                    method: httpMethod,
                    host: host,
                    httpUrl: url);

                span.SetTag(Tags.AspNetController, controllerName);
                span.SetTag(Tags.AspNetAction, actionName);

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(IntegrationName, enabledWithGlobalSetting: true);
                span.SetMetric(Tags.Analytics, analyticsSampleRate);
            }
            catch (Exception) when (DisposeObject(_scope))
            {
                // unreachable code
                throw;
            }
        }

        /// <summary>
        /// Wrapper method used to instrument Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions.BeforeAction()
        /// </summary>
        /// <param name="diagnosticSource">The DiagnosticSource that this extension method was called on.</param>
        /// <param name="actionDescriptor">An ActionDescriptor with information about the current action.</param>
        /// <param name="httpContext">The HttpContext for the current request.</param>
        /// <param name="routeData">A RouteData with information about the current route.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        [InterceptMethod(
            CallerAssembly = "Microsoft.AspNetCore.Mvc.Core",
            TargetAssembly = "Microsoft.AspNetCore.Mvc.Core",
            TargetType = "Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions",
            TargetSignatureTypes = new[] { TypeNames.Ignore, TypeNames.Ignore, TypeNames.Ignore, TypeNames.Ignore, TypeNames.Ignore },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
        public static void BeforeAction(
            object diagnosticSource,
            object actionDescriptor,
            object httpContext,
            object routeData,
            int opCode)
        {
            AspNetCoreMvc2Integration integration = null;

            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                // integration disabled
                return;
            }

            try
            {
                integration = new AspNetCoreMvc2Integration(actionDescriptor, httpContext);

                if (httpContext.TryGetPropertyValue("Items", out IDictionary<object, object> contextItems))
                {
                    contextItems[HttpContextKey] = integration;
                }
            }
            catch (Exception ex)
            {
                Log.ErrorExceptionForFilter($"Error creating {nameof(AspNetCoreMvc2Integration)}.", ex);
            }

            try
            {
                if (_beforeAction == null)
                {
                    var assembly = actionDescriptor.GetType().GetTypeInfo().Assembly;
                    var type = assembly.GetType("Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions");

                    _beforeAction = Emit.DynamicMethodBuilder<Action<object, object, object, object>>.CreateMethodCallDelegate(
                        type,
                        "BeforeAction");
                }
            }
            catch (Exception ex)
            {
                // profiled app will continue working without DiagnosticSource
                Log.ErrorException("Error calling Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions.BeforeAction()", ex);
            }

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                _beforeAction?.Invoke(diagnosticSource, actionDescriptor, httpContext, routeData);
            }
            catch (Exception ex) when (integration?.SetException(ex) ?? false)
            {
                // unreachable code
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
        /// <param name="opCode">The OpCode used in the original method call.</param>
        [InterceptMethod(
            CallerAssembly = "Microsoft.AspNetCore.Mvc.Core",
            TargetAssembly = "Microsoft.AspNetCore.Mvc.Core",
            TargetType = "Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions",
            TargetSignatureTypes = new[] { TypeNames.Ignore, TypeNames.Ignore, TypeNames.Ignore, TypeNames.Ignore, TypeNames.Ignore },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
        public static void AfterAction(
            object diagnosticSource,
            object actionDescriptor,
            object httpContext,
            object routeData,
            int opCode)
        {
            AspNetCoreMvc2Integration integration = null;

            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                // integration disabled
                return;
            }

            try
            {
                if (httpContext.TryGetPropertyValue("Items", out IDictionary<object, object> contextItems))
                {
                    integration = contextItems?[HttpContextKey] as AspNetCoreMvc2Integration;
                }
            }
            catch (Exception ex)
            {
                Log.ErrorExceptionForFilter($"Error accessing {nameof(AspNetCoreMvc2Integration)}.", ex);
            }

            try
            {
                if (_afterAction == null)
                {
                    var type = actionDescriptor.GetType().Assembly.GetType("Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions");

                    _afterAction = Emit.DynamicMethodBuilder<Action<object, object, object, object>>.CreateMethodCallDelegate(
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
        /// Wrapper method used to catch unhandled exceptions in the incoming request pipeline for Microsoft.AspNetCore.Mvc.Core
        /// </summary>
        /// <param name="context">The DiagnosticSource that this extension method was called on.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        [InterceptMethod(
            CallerAssembly = "Microsoft.AspNetCore.Mvc.Core",
            TargetAssembly = "Microsoft.AspNetCore.Mvc.Core",
            TargetType = ResourceInvoker,
            TargetSignatureTypes = new[] { "System.Void", TypeNames.Ignore },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
        public static void Rethrow(object context, int opCode)
        {
            AspNetCoreMvc2Integration integration = null;
            const string methodName = nameof(Rethrow);

            if (context == null)
            {
                // Every rethrow method in every v2.x returns when the context is null
                // We need the type of context to call the correct method as there are 3
                // Remove this when we introduce the type arrays within the profiler
                return;
            }

            try
            {
                if (context.TryGetPropertyValue("HttpContext", out object httpContext))
                {
                    if (httpContext.TryGetPropertyValue("Items", out IDictionary<object, object> contextItems))
                    {
                        integration = contextItems?[HttpContextKey] as AspNetCoreMvc2Integration;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorExceptionForFilter($"Error accessing {nameof(AspNetCoreMvc2Integration)}.", ex);
            }

            Action<object> rethrow;

            try
            {
                rethrow = RethrowAccess.GetInterceptedMethod(
                    assembly: Assembly.GetCallingAssembly(),
                    owningType: ResourceInvoker,
                    returnType: Interception.VoidType,
                    methodName: methodName,
                    generics: Interception.NullTypeArray,
                    parameters: Interception.ParamsToTypes(context));
            }
            catch (Exception ex)
            {
                // profiled app will not continue working as expected without this rethrow method
                Log.ErrorException($"Error calling {ResourceInvoker}.{methodName}(object context)", ex);
                throw;
            }

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                rethrow.Invoke(context);
            }
            catch (Exception ex) when (integration?.SetException(ex) ?? false)
            {
                // unreachable code
                throw;
            }
        }

        /// <summary>
        /// Tags the current span as an error. Called when an unhandled exception is thrown in the instrumented method.
        /// </summary>
        /// <param name="ex">The exception that was thrown and not handled in the instrumented method.</param>
        /// <returns>Always <c>false</c>.</returns>
        public bool SetException(Exception ex)
        {
            _scope?.Span?.SetException(ex);
            return false;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_httpContext != null &&
                    _httpContext.TryGetPropertyValue("Response", out object response) &&
                    response.TryGetPropertyValue("StatusCode", out object statusCode))
                {
                    _scope?.Span?.SetTag("http.status_code", statusCode.ToString());
                }
            }
            finally
            {
                _scope?.Dispose();
            }
        }

        private static void GetTagValuesFromRequest(
            object request,
            out string host,
            out string resourceName,
            out string fullUrl)
        {
            if (!request.TryGetPropertyValue("Host", out object hostObject) ||
                !hostObject.TryGetPropertyValue("Value", out host))
            {
                host = string.Empty;
            }

            if (!request.TryGetPropertyValue("PathBase", out object pathBaseObject) ||
                !pathBaseObject.TryGetPropertyValue("Value", out string pathBase))
            {
                pathBase = string.Empty;
            }

            if (!request.TryGetPropertyValue("Path", out object pathObject) ||
                !pathObject.TryGetPropertyValue("Value", out string path))
            {
                path = string.Empty;
            }

            if (!request.TryGetPropertyValue("QueryString", out object queryStringObject) ||
                !queryStringObject.TryGetPropertyValue("Value", out string queryString))
            {
                queryString = string.Empty;
            }

            if (!request.TryGetPropertyValue("Scheme", out string scheme))
            {
                scheme = string.Empty;
            }

            resourceName = $"{UriHelpers.CleanUriSegment(pathBase)}{UriHelpers.CleanUriSegment(path)}".ToLowerInvariant();
            fullUrl = $"{scheme}://{host}{pathBase}{path}{queryString}".ToLowerInvariant();
        }

        private bool DisposeObject(IDisposable disposable)
        {
            disposable?.Dispose();
            return false;
        }
    }
}
