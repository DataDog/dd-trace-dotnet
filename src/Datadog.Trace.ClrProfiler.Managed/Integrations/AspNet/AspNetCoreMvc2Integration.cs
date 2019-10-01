using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.ExtensionMethods;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// The ASP.NET Core MVC 2 integration.
    /// </summary>
    public sealed class AspNetCoreMvc2Integration : IDisposable
    {
        private const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations." + nameof(AspNetCoreMvc2Integration);
        private const string IntegrationName = "AspNetCoreMvc2";
        private const string OperationName = "aspnet-coremvc.request";
        private const string AspnetMvcCore = "Microsoft.AspNetCore.Mvc.Core";
        private const string Major2 = "2";

        /// <summary>
        /// Type for unobtrusive hooking into Microsoft.AspNetCore.Mvc.Core pipeline.
        /// </summary>
        private const string DiagnosticSourceTypeName = "Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions";

        /// <summary>
        /// Base type used for traversing the pipeline in Microsoft.AspNetCore.Mvc.Core.
        /// </summary>
        private const string ResourceInvokerTypeName = "Microsoft.AspNetCore.Mvc.Internal.ResourceInvoker";

        private static readonly Type DiagnosticSourceType = Type.GetType($"{DiagnosticSourceTypeName}, {AspnetMvcCore}");
        private static readonly Type ResourceInvokerType = Type.GetType($"{ResourceInvokerTypeName}, {AspnetMvcCore}");
        private static readonly Vendoring.Serilog.ILogger Log = Vendoring.DatadogLogging.GetLogger(typeof(AspNetCoreMvc2Integration));

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
                var request = _httpContext.GetProperty("Request").GetValueOrDefault();

                GetTagValues(
                    actionDescriptor,
                    request,
                    out string httpMethod,
                    out string host,
                    out string resourceName,
                    out string url,
                    out string controllerName,
                    out string actionName);

                SpanContext propagatedContext = null;
                var tracer = Tracer.Instance;

                if (tracer.ActiveScope == null)
                {
                    try
                    {
                        // extract propagated http headers
                        var requestHeaders = request.GetProperty<IEnumerable>("Headers").GetValueOrDefault();

                        if (requestHeaders != null)
                        {
                            var headersCollection = new DictionaryHeadersCollection();

                            foreach (object header in requestHeaders)
                            {
                                var key = header.GetProperty<string>("Key").GetValueOrDefault();
                                var values = header.GetProperty<IList<string>>("Value").GetValueOrDefault();

                                if (key != null && values != null)
                                {
                                    headersCollection.Add(key, values);
                                }
                            }

                            propagatedContext = SpanContextPropagator.Instance.Extract(headersCollection);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting propagated HTTP headers.");
                    }
                }

                _scope = tracer.StartActive(OperationName, propagatedContext);
                var span = _scope.Span;

                span.DecorateWebServerSpan(
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
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        [InterceptMethod(
            CallerAssembly = AspnetMvcCore,
            TargetAssembly = AspnetMvcCore,
            TargetType = DiagnosticSourceTypeName,
            TargetSignatureTypes = new[] { ClrNames.Void, ClrNames.Ignore, "Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor", "Microsoft.AspNetCore.Http.HttpContext", "Microsoft.AspNetCore.Routing.RouteData" },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
        public static void BeforeAction(
            object diagnosticSource,
            object actionDescriptor,
            object httpContext,
            object routeData,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
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
                Log.Error(ex, $"Error creating {nameof(AspNetCoreMvc2Integration)}.");
            }

            Action<object, object, object, object> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Action<object, object, object, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, nameof(BeforeAction))
                       .WithConcreteType(DiagnosticSourceType)
                       .WithParameters(diagnosticSource, actionDescriptor, httpContext, routeData)
                       .WithNamespaceAndNameFilters(
                            ClrNames.Void,
                            ClrNames.Ignore,
                            "Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor",
                            "Microsoft.AspNetCore.Http.HttpContext",
                            "Microsoft.AspNetCore.Routing.RouteData")
                       .Build();
            }
            catch (Exception ex)
            {
                // profiled app will continue working as expected without this method
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: DiagnosticSourceTypeName,
                    methodName: nameof(BeforeAction),
                    instanceType: null,
                    relevantArguments: new[] { diagnosticSource?.GetType().AssemblyQualifiedName });
            }

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                instrumentedMethod?.Invoke(diagnosticSource, actionDescriptor, httpContext, routeData);
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
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        [InterceptMethod(
            CallerAssembly = AspnetMvcCore,
            TargetAssembly = AspnetMvcCore,
            TargetType = DiagnosticSourceTypeName,
            TargetSignatureTypes = new[] { ClrNames.Void, ClrNames.Ignore, "Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor", "Microsoft.AspNetCore.Http.HttpContext", "Microsoft.AspNetCore.Routing.RouteData" },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
        public static void AfterAction(
            object diagnosticSource,
            object actionDescriptor,
            object httpContext,
            object routeData,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
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
                Log.Error(ex, $"Error accessing {nameof(AspNetCoreMvc2Integration)}.");
            }

            Action<object, object, object, object> instrumentedMethod = null;

            string methodDef = $"{DiagnosticSourceTypeName}.{nameof(AfterAction)}(...)";

            try
            {
                instrumentedMethod =
                    MethodBuilder<Action<object, object, object, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, nameof(AfterAction))
                       .WithConcreteType(DiagnosticSourceType)
                       .WithParameters(diagnosticSource, actionDescriptor, httpContext, routeData)
                       .WithNamespaceAndNameFilters(
                            ClrNames.Void,
                            ClrNames.Ignore,
                            "Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor",
                            "Microsoft.AspNetCore.Http.HttpContext",
                            "Microsoft.AspNetCore.Routing.RouteData")
                       .Build();
            }
            catch (Exception ex)
            {
                // profiled app will continue working as expected without this method
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: DiagnosticSourceTypeName,
                    methodName: nameof(AfterAction),
                    instanceType: null,
                    relevantArguments: new[] { diagnosticSource?.GetType().AssemblyQualifiedName });
            }

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                instrumentedMethod?.Invoke(diagnosticSource, actionDescriptor, httpContext, routeData);
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
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        [InterceptMethod(
            CallerAssembly = AspnetMvcCore,
            TargetAssembly = AspnetMvcCore,
            TargetType = ResourceInvokerTypeName,
            TargetSignatureTypes = new[] { ClrNames.Void, ClrNames.Ignore },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
        public static void Rethrow(object context, int opCode, int mdToken, long moduleVersionPtr)
        {
            if (context == null)
            {
                // Every rethrow method in every v2.x returns when the context is null
                // We need the type of context to call the correct method as there are 3
                // Remove this when we introduce the type arrays within the profiler
                return;
            }

            var shouldTrace = Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName);

            Action<object> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Action<object>>
                       .Start(moduleVersionPtr, mdToken, opCode, nameof(Rethrow))
                       .WithConcreteType(ResourceInvokerType)
                       .WithParameters(context)
                       .WithNamespaceAndNameFilters(ClrNames.Void, ClrNames.Ignore)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: ResourceInvokerTypeName,
                    methodName: nameof(Rethrow),
                    instanceType: null,
                    relevantArguments: new[] { context.GetType().AssemblyQualifiedName });
                throw;
            }

            AspNetCoreMvc2Integration integration = null;
            if (shouldTrace)
            {
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
                    Log.Error(ex, $"Error accessing {nameof(AspNetCoreMvc2Integration)}.");
                }
            }

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                instrumentedMethod.Invoke(context);
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

        private static void GetTagValues(
            object actionDescriptor,
            object request,
            out string httpMethod,
            out string host,
            out string resourceName,
            out string url,
            out string controllerName,
            out string actionName)
        {
            controllerName = actionDescriptor.GetProperty<string>("ControllerName").GetValueOrDefault()?.ToLowerInvariant();

            actionName = actionDescriptor.GetProperty<string>("ActionName").GetValueOrDefault()?.ToLowerInvariant();

            host = request.GetProperty("Host").GetProperty<string>("Value").GetValueOrDefault();

            httpMethod = request.GetProperty<string>("Method").GetValueOrDefault()?.ToUpperInvariant() ?? "UNKNOWN";

            string pathBase = request.GetProperty("PathBase").GetProperty<string>("Value").GetValueOrDefault();

            string path = request.GetProperty("Path").GetProperty<string>("Value").GetValueOrDefault();

            string queryString = request.GetProperty("QueryString").GetProperty<string>("Value").GetValueOrDefault();

            url = $"{pathBase}{path}{queryString}";

            string resourceUrl = actionDescriptor.GetProperty("AttributeRouteInfo").GetProperty<string>("Template").GetValueOrDefault() ??
                                 UriHelpers.GetRelativeUrl(new Uri($"https://{host}{url}"), tryRemoveIds: true).ToLowerInvariant();

            resourceName = $"{httpMethod} {resourceUrl}";
        }

        private bool DisposeObject(IDisposable disposable)
        {
            disposable?.Dispose();
            return false;
        }
    }
}
