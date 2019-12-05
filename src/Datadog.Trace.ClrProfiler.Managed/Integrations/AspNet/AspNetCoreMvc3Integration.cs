using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// The ASP.NET Core MVC 3 integration.
    /// </summary>
    public static class AspNetCoreMvc3Integration
    {
        private const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations." + nameof(AspNetCoreMvc3Integration);
        private const string IntegrationName = "AspNetCoreMvc3";
        private const string OperationName = "aspnet-coremvc.request";
        private const string AspnetMvcCore = "Microsoft.AspNetCore.Mvc.Core";
        private const string MinimumVersion = "3";
        private const string MaximumVersion = "3";

        /// <summary>
        /// Type for unobtrusive hooking into Microsoft.AspNetCore.Mvc pipeline.
        /// </summary>
        private const string DiagnosticListenerExtensionsTypeName = "Microsoft.AspNetCore.Mvc.MvcCoreDiagnosticListenerExtensions";

        /// <summary>
        /// Base type used for traversing the pipeline in Microsoft.AspNetCore.Mvc.Core.
        /// </summary>
        private const string ResourceInvokerTypeName = "Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker";

        private const string ControllerActionDescriptorTypeName = "Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor";

        private const string DefaultHttpContextTypeName = "Microsoft.AspNetCore.Http.DefaultHttpContext";
        private const string HttpContextTypeName = "Microsoft.AspNetCore.Http.HttpContext";

        private const string RouteDataTypeName = "Microsoft.AspNetCore.Routing.RouteData";

        private const string DiagnosticListenerTypeName = "System.Diagnostics.DiagnosticListener";
        private const string ResourceExecutedContextSealedTypeName = "Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker+ResourceExecutedContextSealed";
        private const string ExceptionContextSealedTypeName = "Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker+ExceptionContextSealed";
        private const string ResultExecutedContextSealedTypeName = "Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker+ResultExecutedContextSealed";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(AspNetCoreMvc3Integration));

        private static AspNetCoreMvcContext CreateContext(object actionDescriptor, object httpContext)
        {
            var context = new AspNetCoreMvcContext();

            try
            {
                var request = httpContext.GetProperty("Request").GetValueOrDefault();

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

                var scope = tracer.StartActive(OperationName, propagatedContext);
                context.Scope = scope;
                var span = scope.Span;

                span.DecorateWebServerSpan(
                    resourceName: resourceName,
                    method: httpMethod,
                    host: host,
                    httpUrl: url);

                span.SetTag(Tags.AspNetController, controllerName);
                span.SetTag(Tags.AspNetAction, actionName);

                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(IntegrationName, enabledWithGlobalSetting: true);
                span.SetMetric(Tags.Analytics, analyticsSampleRate);
            }
            catch (Exception ex)
            {
                context.ShouldInstrument = false;
                context.Scope.Dispose();
                Log.Error(
                    ex,
                    "An exception occurred when trying to initialize a Scope for {0}",
                    nameof(AspNetCoreMvc3Integration));
                throw;
            }

            return context;
        }

        /// <summary>
        /// Wrapper method used to instrument Microsoft.AspNetCore.Mvc.MvcCoreDiagnosticSourceExtensions.BeforeAction(...)
        /// </summary>
        /// <param name="diagnosticListener">The diagnostic listener that this extension method was called on.</param>
        /// <param name="controllerActionDescriptor">An descriptor with information about the current controller and action.</param>
        /// <param name="httpContext">The HttpContext for the current request.</param>
        /// <param name="routeData">Any relevant data from routing.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        [InterceptMethod(
            TargetAssembly = AspnetMvcCore,
            TargetType = DiagnosticListenerExtensionsTypeName,
            TargetSignatureTypes = new[] { ClrNames.Void, DiagnosticListenerTypeName, ControllerActionDescriptorTypeName, DefaultHttpContextTypeName, RouteDataTypeName },
            TargetMinimumVersion = MinimumVersion,
            TargetMaximumVersion = MaximumVersion)]
        [InterceptMethod(
            TargetAssembly = AspnetMvcCore,
            TargetType = DiagnosticListenerExtensionsTypeName,
            TargetSignatureTypes = new[] { ClrNames.Void, DiagnosticListenerTypeName, ControllerActionDescriptorTypeName, HttpContextTypeName, RouteDataTypeName },
            TargetMinimumVersion = MinimumVersion,
            TargetMaximumVersion = MaximumVersion)]
        public static void BeforeAction(
            object diagnosticListener,
            object controllerActionDescriptor,
            object httpContext,
            object routeData,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            AspNetCoreMvcContext context = null;

            try
            {
                context = CreateContext(controllerActionDescriptor, httpContext);

                if (context.ShouldInstrument
                 && httpContext.TryGetPropertyValue("Items", out IDictionary<object, object> contextItems))
                {
                    contextItems[HttpContextKey] = context;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error creating {nameof(AspNetCoreMvcContext)}.");
            }

            Action<object, object, object, object> instrumentedMethod = null;
            Type concreteType = null;

            try
            {
                var module = ModuleLookup.GetByPointer(moduleVersionPtr);
                concreteType = module.GetType(DiagnosticListenerExtensionsTypeName);

                instrumentedMethod =
                    MethodBuilder<Action<object, object, object, object>>
                       .Start(module, mdToken, opCode, nameof(BeforeAction))
                       .WithConcreteType(concreteType)
                       .WithParameters(diagnosticListener, controllerActionDescriptor, httpContext, routeData)
                       .WithNamespaceAndNameFilters(
                            ClrNames.Void,
                            DiagnosticListenerTypeName,
                            ControllerActionDescriptorTypeName,
                            ClrNames.Ignore,
                            RouteDataTypeName)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: DiagnosticListenerExtensionsTypeName,
                    methodName: nameof(BeforeAction),
                    instanceType: null,
                    relevantArguments: new[] { concreteType?.AssemblyQualifiedName });
                throw;
            }

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                instrumentedMethod.Invoke(diagnosticListener, controllerActionDescriptor, httpContext, routeData);
            }
            catch (Exception ex)
            {
                // BeforeAction should never throw exceptions as it is diagnostic only.
                // If we are here, it means we've likely messed up.
                // Set an exception and close our span
                if (context?.Scope != null)
                {
                    context.Scope?.Span?.SetException(ex);
                    context.Scope?.Dispose();
                    context.ShouldInstrument = false;
                    context.Scope = null;
                }

                throw;
            }
        }

        /// <summary>
        /// Wrapper method used to instrument Microsoft.AspNetCore.Mvc.MvcCoreDiagnosticSourceExtensions.AfterAction(...)
        /// </summary>
        /// <param name="diagnosticListener">The diagnostic listener that this extension method was called on.</param>
        /// <param name="controllerActionDescriptor">An descriptor with information about the current controller and action.</param>
        /// <param name="httpContext">The HttpContext for the current request.</param>
        /// <param name="routeData">Any relevant data from routing.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        [InterceptMethod(
            TargetAssembly = AspnetMvcCore,
            TargetType = DiagnosticListenerExtensionsTypeName,
            TargetSignatureTypes = new[] { ClrNames.Void, DiagnosticListenerTypeName, ControllerActionDescriptorTypeName, DefaultHttpContextTypeName, RouteDataTypeName },
            TargetMinimumVersion = MinimumVersion,
            TargetMaximumVersion = MaximumVersion)]
        [InterceptMethod(
            TargetAssembly = AspnetMvcCore,
            TargetType = DiagnosticListenerExtensionsTypeName,
            TargetSignatureTypes = new[] { ClrNames.Void, DiagnosticListenerTypeName, ControllerActionDescriptorTypeName, HttpContextTypeName, RouteDataTypeName },
            TargetMinimumVersion = MinimumVersion,
            TargetMaximumVersion = MaximumVersion)]
        public static void AfterAction(
            object diagnosticListener,
            object controllerActionDescriptor,
            object httpContext,
            object routeData,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            AspNetCoreMvcContext context = null;

            try
            {
                if (httpContext.TryGetPropertyValue("Items", out IDictionary<object, object> contextItems))
                {
                    context = contextItems?[HttpContextKey] as AspNetCoreMvcContext;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error accessing {nameof(AspNetCoreMvcContext)}.");
            }

            Action<object, object, object, object> instrumentedMethod = null;
            Type concreteType = null;

            try
            {
                var module = ModuleLookup.GetByPointer(moduleVersionPtr);
                concreteType = module.GetType(DiagnosticListenerExtensionsTypeName);

                instrumentedMethod =
                    MethodBuilder<Action<object, object, object, object>>
                       .Start(module, mdToken, opCode, nameof(AfterAction))
                       .WithConcreteType(concreteType)
                       .WithParameters(diagnosticListener, controllerActionDescriptor, httpContext, routeData)
                       .WithNamespaceAndNameFilters(
                            ClrNames.Void,
                            DiagnosticListenerTypeName,
                            ControllerActionDescriptorTypeName,
                            ClrNames.Ignore,
                            RouteDataTypeName)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: DiagnosticListenerExtensionsTypeName,
                    methodName: nameof(AfterAction),
                    instanceType: null,
                    relevantArguments: new[] { concreteType?.AssemblyQualifiedName });
                throw;
            }

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                instrumentedMethod.Invoke(diagnosticListener, controllerActionDescriptor, httpContext, routeData);
            }
            catch (Exception ex)
            {
                context?.Scope?.Span?.SetException(ex);
                throw;
            }
            finally
            {
                context?.Scope?.Dispose();
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
            TargetAssembly = AspnetMvcCore,
            TargetType = ResourceInvokerTypeName,
            TargetSignatureTypes = new[] { ClrNames.Void, ResourceExecutedContextSealedTypeName },
            TargetMethod = nameof(Rethrow),
            TargetMinimumVersion = MinimumVersion,
            TargetMaximumVersion = MaximumVersion)]
        public static void Rethrow_ResourceExecutedContextSealed(object context, int opCode, int mdToken, long moduleVersionPtr)
        {
            Rethrow(context, opCode, mdToken, moduleVersionPtr, ResourceExecutedContextSealedTypeName);
        }

        /// <summary>
        /// Wrapper method used to catch unhandled exceptions in the incoming request pipeline for Microsoft.AspNetCore.Mvc.Core
        /// </summary>
        /// <param name="context">The DiagnosticSource that this extension method was called on.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        [InterceptMethod(
            TargetAssembly = AspnetMvcCore,
            TargetType = ResourceInvokerTypeName,
            TargetSignatureTypes = new[] { ClrNames.Void, ExceptionContextSealedTypeName },
            TargetMethod = nameof(Rethrow),
            TargetMinimumVersion = MinimumVersion,
            TargetMaximumVersion = MaximumVersion)]
        public static void Rethrow_ExceptionContextSealed(object context, int opCode, int mdToken, long moduleVersionPtr)
        {
            Rethrow(context, opCode, mdToken, moduleVersionPtr, ExceptionContextSealedTypeName);
        }

        /// <summary>
        /// Wrapper method used to catch unhandled exceptions in the incoming request pipeline for Microsoft.AspNetCore.Mvc.Core
        /// </summary>
        /// <param name="context">The DiagnosticSource that this extension method was called on.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        [InterceptMethod(
            TargetAssembly = AspnetMvcCore,
            TargetType = ResourceInvokerTypeName,
            TargetSignatureTypes = new[] { ClrNames.Void, ResultExecutedContextSealedTypeName },
            TargetMethod = nameof(Rethrow),
            TargetMinimumVersion = MinimumVersion,
            TargetMaximumVersion = MaximumVersion)]
        public static void Rethrow_ResultExecutedContextSealed(object context, int opCode, int mdToken, long moduleVersionPtr)
        {
            Rethrow(context, opCode, mdToken, moduleVersionPtr, ResultExecutedContextSealedTypeName);
        }

        private static void Rethrow(object context, int opCode, int mdToken, long moduleVersionPtr, string contextType)
        {
            Action<object> instrumentedMethod;

            try
            {
                var module = ModuleLookup.GetByPointer(moduleVersionPtr);
                var concreteType = module.GetType(contextType);

                instrumentedMethod =
                    MethodBuilder<Action<object>>
                       .Start(moduleVersionPtr, mdToken, opCode, nameof(Rethrow))
                       .WithConcreteType(concreteType)
                       .WithParameters(context)
                       .WithNamespaceAndNameFilters(ClrNames.Void, contextType)
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

            AspNetCoreMvcContext integration = null;

            try
            {
                if (context.TryGetPropertyValue("HttpContext", out object httpContext))
                {
                    if (httpContext.TryGetPropertyValue("Items", out IDictionary<object, object> contextItems))
                    {
                        integration = contextItems?[HttpContextKey] as AspNetCoreMvcContext;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error accessing {nameof(AspNetCoreMvc3Integration)}.");
            }

            try
            {
                // call the original method, observing any unhandled exceptions
                instrumentedMethod.Invoke(context);
            }
            catch (Exception ex)
            {
                integration?.Scope?.Span?.SetException(ex);
                throw;
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

        private class AspNetCoreMvcContext
        {
            public AspNetCoreMvcContext()
            {
                if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
                {
                    // integration disabled
                    ShouldInstrument = false;
                }
            }

            public bool ShouldInstrument { get; set; } = true;

            public Scope Scope { get; set; }
        }
    }
}
