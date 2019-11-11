using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

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
        private const string MinimumVersion = "1";
        private const string MaximumVersion = "6";

        /// <summary>
        /// Type for unobtrusive hooking into Microsoft.AspNetCore.Mvc pipeline.
        /// </summary>
        private const string DiagnosticListenerTypeName = "Microsoft.AspNetCore.Mvc.MvcCoreDiagnosticListenerExtensions";

        /// <summary>
        /// Base type used for traversing the pipeline in Microsoft.AspNetCore.Mvc.Core.
        /// </summary>
        private const string ResourceInvokerTypeName = "Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker";

        private const string ControllerActionDescriptorTypeName = "Microsoft.AspNetCore.Http.DefaultHttpContext";

        private const string DefaultHttpContextTypeName = "Microsoft.AspNetCore.Http.DefaultHttpContext";

        private const string RouteDataTypeName = "Microsoft.AspNetCore.Routing.RouteData";

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
            TargetType = DiagnosticListenerTypeName,
            TargetSignatureTypes = new[] { ClrNames.Void, ClrNames.Ignore, ControllerActionDescriptorTypeName, DefaultHttpContextTypeName, RouteDataTypeName },
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
                concreteType = module.GetType(DiagnosticListenerTypeName);

                instrumentedMethod =
                    MethodBuilder<Action<object, object, object, object>>
                       .Start(module, mdToken, opCode, nameof(BeforeAction))
                       .WithConcreteType(concreteType)
                       .WithParameters(diagnosticListener, controllerActionDescriptor, httpContext, routeData)
                       .WithNamespaceAndNameFilters(
                            ClrNames.Void,
                            ClrNames.Ignore,
                            ClrNames.Ignore,
                            ClrNames.Ignore,
                            ClrNames.Ignore)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: DiagnosticListenerTypeName,
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
                context?.Scope?.Span?.SetException(ex);
                throw;
            }
            finally
            {
                context?.Scope?.Dispose();
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
            TargetType = DiagnosticListenerTypeName,
            TargetSignatureTypes = new[] { ClrNames.Void, ClrNames.Ignore, ControllerActionDescriptorTypeName, DefaultHttpContextTypeName, RouteDataTypeName },
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
                concreteType = module.GetType(DiagnosticListenerTypeName);

                instrumentedMethod =
                    MethodBuilder<Action<object, object, object, object>>
                       .Start(module, mdToken, opCode, nameof(AfterAction))
                       .WithConcreteType(concreteType)
                       .WithParameters(diagnosticListener, controllerActionDescriptor, httpContext, routeData)
                       .WithNamespaceAndNameFilters(
                            ClrNames.Void,
                            ClrNames.Ignore,
                            ClrNames.Ignore,
                            ClrNames.Ignore,
                            ClrNames.Ignore)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: DiagnosticListenerTypeName,
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
