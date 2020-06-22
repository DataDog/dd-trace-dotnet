#if !NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Extensions;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Contains instrumentation wrappers for ASP.NET Web API 5.
    /// </summary>
    public static class AspNetWebApi2Integration
    {
        private const string IntegrationName = "AspNetWebApi2";
        private const string OperationName = "aspnet-webapi.request";
        private const string Major5Minor1 = "5.1";
        private const string Major5 = "5";

        private const string SystemWebHttpAssemblyName = "System.Web.Http";
        private const string HttpControllerTypeName = "System.Web.Http.Controllers.IHttpController";
        private const string HttpControllerContextTypeName = "System.Web.Http.Controllers.HttpControllerContext";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(AspNetWebApi2Integration));

        /// <summary>
        /// Calls the underlying ExecuteAsync and traces the request.
        /// </summary>
        /// <param name="apiController">The Api Controller</param>
        /// <param name="controllerContext">The controller context for the call</param>
        /// <param name="boxedCancellationToken">The cancellation token</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>A task with the result</returns>
        [InterceptMethod(
            TargetAssembly = SystemWebHttpAssemblyName,
            TargetType = HttpControllerTypeName,
            TargetSignatureTypes = new[] { ClrNames.HttpResponseMessageTask, HttpControllerContextTypeName, ClrNames.CancellationToken },
            TargetMinimumVersion = Major5Minor1,
            TargetMaximumVersion = Major5)]
        public static object ExecuteAsync(
            object apiController,
            object controllerContext,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (apiController == null) { throw new ArgumentNullException(nameof(apiController)); }

            var cancellationToken = (CancellationToken)boxedCancellationToken;
            var callOpCode = (OpCodeValue)opCode;
            var httpControllerType = apiController.GetInstrumentedInterface(HttpControllerTypeName);

            Type taskResultType;

            try
            {
                var request = controllerContext.GetProperty<object>("Request").GetValueOrDefault();
                var httpRequestMessageType = request.GetInstrumentedType("System.Net.Http.HttpRequestMessage");

                // The request should never be null, so get the base type found in System.Net.Http.dll
                if (httpRequestMessageType != null)
                {
                    var systemNetHttpAssembly = httpRequestMessageType.Assembly;
                    taskResultType = systemNetHttpAssembly.GetType("System.Net.Http.HttpResponseMessage", true);
                }

                // This should never happen, but put in a reasonable fallback of finding the first System.Net.Http.dll in the AppDomain
                else
                {
                    var statsd = Tracer.Instance.Statsd;
                    if (statsd != null)
                    {
                        statsd.AppendWarning(source: $"{nameof(AspNetWebApi2Integration)}.{nameof(ExecuteAsync)}", message: "Using fallback logic to find System.Net.Http.HttpResponseMessage Type needed for return type", null);
                        statsd.Send();
                    }

                    var systemNetHttpAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.GetName().Name.Equals("System.Net.Http", StringComparison.OrdinalIgnoreCase));
                    var firstSystemNetHttpAssembly = systemNetHttpAssemblies.First();
                    taskResultType = firstSystemNetHttpAssembly.GetType("System.Net.Http.HttpResponseMessage", true);
                }
            }
            catch (Exception ex)
            {
                // This shouldn't happen because the System.Net.Http assembly should have been loaded if this method was called
                // The profiled app will not continue working as expected without this method
                Log.Error(ex, "Error finding types in the user System.Net.Http assembly.");
                throw;
            }

            Func<object, object, CancellationToken, object> instrumentedMethod = null;

            try
            {
                instrumentedMethod = MethodBuilder<Func<object, object, CancellationToken, object>>
                                    .Start(moduleVersionPtr, mdToken, opCode, nameof(ExecuteAsync))
                                    .WithConcreteType(httpControllerType)
                                    .WithParameters(controllerContext, cancellationToken)
                                    .WithNamespaceAndNameFilters(
                                         ClrNames.GenericTask,
                                         HttpControllerContextTypeName,
                                         ClrNames.CancellationToken)
                                    .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: HttpControllerTypeName,
                    methodName: nameof(ExecuteAsync),
                    instanceType: apiController.GetType().AssemblyQualifiedName);
                throw;
            }

            return AsyncHelper.InvokeGenericTaskDelegate(
                owningType: apiController.GetType(),
                taskResultType: taskResultType,
                nameOfIntegrationMethod: nameof(ExecuteAsyncInternal),
                integrationType: typeof(AspNetWebApi2Integration),
                instrumentedMethod,
                apiController,
                controllerContext,
                cancellationToken);
        }

        /// <summary>
        /// Calls the underlying ExecuteAsync and traces the request.
        /// </summary>
        /// <typeparam name="T">The type of the generic Task instantiation</typeparam>
        /// <param name="instrumentedMethod">The underlying ExecuteAsync method</param>
        /// <param name="apiController">The Api Controller</param>
        /// <param name="controllerContext">The controller context for the call</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A task with the result</returns>
        private static async Task<T> ExecuteAsyncInternal<T>(
            Func<object, object, CancellationToken, object> instrumentedMethod,
            object apiController,
            object controllerContext,
            CancellationToken cancellationToken)
        {
            using (Scope scope = CreateScope(controllerContext))
            {
                try
                {
                    // call the original method, inspecting (but not catching) any unhandled exceptions
                    var task = (Task<T>)instrumentedMethod(apiController, controllerContext, cancellationToken);
                    var responseMessage = await task.ConfigureAwait(false);

                    if (scope != null)
                    {
                        // some fields aren't set till after execution, so populate anything missing
                        UpdateSpan(controllerContext, scope.Span);
                    }

                    return responseMessage;
                }
                catch (Exception ex)
                {
                    if (scope != null)
                    {
                        // some fields aren't set till after execution, so populate anything missing
                        UpdateSpan(controllerContext, scope.Span);
                    }

                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static Scope CreateScope(object controllerContext)
        {
            Scope scope = null;

            try
            {
                if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
                {
                    // integration disabled, don't create a scope, skip this trace
                    return null;
                }

                var tracer = Tracer.Instance;
                var request = controllerContext.GetProperty<object>("Request").GetValueOrDefault();
                SpanContext propagatedContext = null;

                if (request != null && tracer.ActiveScope == null)
                {
                    try
                    {
                        // extract propagated http headers
                        var headers = request.GetProperty<object>("Headers").GetValueOrDefault();
                        propagatedContext = SpanContextPropagator.Instance.ExtractHttpHeadersWithReflection(headers);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting propagated HTTP headers.");
                    }
                }

                scope = tracer.StartActive(OperationName, propagatedContext);
                UpdateSpan(controllerContext, scope.Span);

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(IntegrationName, enabledWithGlobalSetting: true);
                scope.Span.SetMetric(Tags.Analytics, analyticsSampleRate);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating scope.");
            }

            return scope;
        }

        private static void UpdateSpan(dynamic controllerContext, Span span)
        {
            try
            {
                var req = controllerContext?.Request;

                string host = req?.Headers?.Host ?? string.Empty;
                string rawUrl = req?.RequestUri?.ToString().ToLowerInvariant() ?? string.Empty;
                string absoluteUri = req?.RequestUri?.AbsoluteUri?.ToLowerInvariant() ?? string.Empty;
                string method = controllerContext?.Request?.Method?.Method?.ToUpperInvariant() ?? "GET";
                string route = null;
                try
                {
                    route = controllerContext?.RouteData?.Route?.RouteTemplate;
                }
                catch
                {
                }

                string resourceName = $"{method} {absoluteUri.ToLowerInvariant()}";

                if (route != null)
                {
                    resourceName = $"{method} {route.ToLowerInvariant()}";
                }
                else if (req?.RequestUri != null)
                {
                    var cleanUri = UriHelpers.GetRelativeUrl(req?.RequestUri, tryRemoveIds: true);
                    resourceName = $"{method} {cleanUri.ToLowerInvariant()}";
                }

                string controller = string.Empty;
                string action = string.Empty;
                try
                {
                    if (controllerContext?.RouteData?.Values is IDictionary<string, object> routeValues)
                    {
                        controller = (routeValues.GetValueOrDefault("controller") as string)?.ToLowerInvariant();
                        action = (routeValues.GetValueOrDefault("action") as string)?.ToLowerInvariant();
                    }
                }
                catch
                {
                }

                // Fail safe to catch templates in routing values
                resourceName =
                    resourceName
                       .Replace("{controller}", controller)
                       .Replace("{action}", action);

                span.DecorateWebServerSpan(
                    resourceName: resourceName,
                    method: method,
                    host: host,
                    httpUrl: rawUrl);
                span.SetTag(Tags.AspNetAction, action);
                span.SetTag(Tags.AspNetController, controller);
                span.SetTag(Tags.AspNetRoute, route);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error populating scope data.");
            }
        }
    }
}

#endif
