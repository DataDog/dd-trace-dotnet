// <copyright file="AspNetWebApi2Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.ClrProfiler.Integrations.AspNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Contains instrumentation wrappers for ASP.NET Web API 5.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AspNetWebApi2Integration
    {
        private const string OperationName = "aspnet-webapi.request";
        private const string Major5Minor1 = "5.1";
        private const string Major5MinorX = "5";

        private const string SystemWebHttpAssemblyName = "System.Web.Http";
        private const string HttpControllerTypeName = "System.Web.Http.Controllers.IHttpController";
        private const string HttpControllerContextTypeName = "System.Web.Http.Controllers.HttpControllerContext";

        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.AspNetWebApi2));
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AspNetWebApi2Integration));

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
            TargetMaximumVersion = Major5MinorX)]
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
                    Log.Warning($"{nameof(AspNetWebApi2Integration)}.{nameof(ExecuteAsync)}: Unable to find System.Net.Http.HttpResponseMessage Type from method arguments. Using fallback logic to find the Type needed for return type.");
                    var statsd = Tracer.Instance.Statsd;
                    statsd?.Warning(source: $"{nameof(AspNetWebApi2Integration)}.{nameof(ExecuteAsync)}", message: "Unable to find System.Net.Http.HttpResponseMessage Type from method arguments. Using fallback logic to find the Type needed for return type.", null);

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
                                         ClrNames.HttpResponseMessageTask,
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
        /// <param name="context">The controller context for the call</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A task with the result</returns>
        private static async Task<T> ExecuteAsyncInternal<T>(
            Func<object, object, CancellationToken, object> instrumentedMethod,
            object apiController,
            object context,
            CancellationToken cancellationToken)
        {
            var controllerContext = context.DuckCast<IHttpControllerContext>();

            Scope scope = CreateScope(controllerContext, out var tags);

            try
            {
                // call the original method, inspecting (and rethrowing) any unhandled exceptions
                var task = (Task<T>)instrumentedMethod(apiController, context, cancellationToken);
                var responseMessage = await task;

                if (scope != null)
                {
                    // some fields aren't set till after execution, so populate anything missing
                    UpdateSpan(controllerContext, scope.Span, tags, Enumerable.Empty<KeyValuePair<string, string>>());
                    HttpContextHelper.AddHeaderTagsFromHttpResponse(System.Web.HttpContext.Current, scope);
                    scope.Span.SetHttpStatusCode(responseMessage.DuckCast<HttpResponseMessageStruct>().StatusCode, isServer: true);
                    scope.Dispose();
                }

                return responseMessage;
            }
            catch (Exception ex)
            {
                if (scope != null)
                {
                    // some fields aren't set till after execution, so populate anything missing
                    UpdateSpan(controllerContext, scope.Span, tags, Enumerable.Empty<KeyValuePair<string, string>>());
                    scope.Span.SetException(ex);

                    // We don't have access to the final status code at this point
                    // Ask the HttpContext to call us back to that we can get it
                    var httpContext = System.Web.HttpContext.Current;

                    if (httpContext != null)
                    {
                        // We don't know how long it'll take for ASP.NET to invoke the callback,
                        // so we store the real finish time
                        var now = scope.Span.Context.TraceContext.UtcNow;
                        httpContext.AddOnRequestCompleted(h => OnRequestCompleted(h, scope, now));
                    }
                    else
                    {
                        // Looks like we won't be able to get the final status code
                        scope.Dispose();
                    }
                }

                throw;
            }
        }

        internal static Scope CreateScope(IHttpControllerContext controllerContext, out AspNetTags tags)
        {
            Scope scope = null;
            tags = null;

            try
            {
                if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId))
                {
                    // integration disabled, don't create a scope, skip this trace
                    return null;
                }

                var tracer = Tracer.Instance;
                var request = controllerContext.Request;
                SpanContext propagatedContext = null;
                var tagsFromHeaders = Enumerable.Empty<KeyValuePair<string, string>>();

                if (request != null && tracer.ActiveScope == null)
                {
                    try
                    {
                        // extract propagated http headers
                        var headers = request.Headers;
                        var headersCollection = new HttpHeadersCollection(headers);

                        propagatedContext = SpanContextPropagator.Instance.Extract(headersCollection);
                        tagsFromHeaders = SpanContextPropagator.Instance.ExtractHeaderTags(headersCollection, tracer.Settings.HeaderTags, SpanContextPropagator.HttpRequestHeadersTagPrefix);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting propagated HTTP headers.");
                    }
                }

                tags = new AspNetTags();
                scope = tracer.StartActiveWithTags(OperationName, propagatedContext, tags: tags);
                UpdateSpan(controllerContext, scope.Span, tags, tagsFromHeaders);

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating scope.");
            }

            return scope;
        }

        internal static void UpdateSpan(IHttpControllerContext controllerContext, Span span, AspNetTags tags, IEnumerable<KeyValuePair<string, string>> headerTags)
        {
            try
            {
                var newResourceNamesEnabled = Tracer.Instance.Settings.RouteTemplateResourceNamesEnabled;
                var request = controllerContext.Request;
                Uri requestUri = request.RequestUri;

                string host = request.Headers.Host ?? string.Empty;
                string rawUrl = requestUri?.ToString().ToLowerInvariant() ?? string.Empty;
                string method = request.Method.Method?.ToUpperInvariant() ?? "GET";
                string route = null;
                try
                {
                    route = controllerContext.RouteData.Route.RouteTemplate;
                }
                catch
                {
                }

                string resourceName;

                if (route != null)
                {
                    resourceName = $"{method} {(newResourceNamesEnabled ? "/" : string.Empty)}{route.ToLowerInvariant()}";
                }
                else if (requestUri != null)
                {
                    var cleanUri = UriHelpers.GetCleanUriPath(requestUri);
                    resourceName = $"{method} {cleanUri.ToLowerInvariant()}";
                }
                else
                {
                    resourceName = $"{method}";
                }

                string controller = string.Empty;
                string action = string.Empty;
                string area = string.Empty;
                try
                {
                    var routeValues = controllerContext.RouteData.Values;
                    if (routeValues != null)
                    {
                        controller = (routeValues.GetValueOrDefault("controller") as string)?.ToLowerInvariant();
                        action = (routeValues.GetValueOrDefault("action") as string)?.ToLowerInvariant();
                        area = (routeValues.GetValueOrDefault("area") as string)?.ToLowerInvariant();
                    }
                }
                catch
                {
                }

                // Replace well-known routing tokens
                resourceName =
                    resourceName
                       .Replace("{area}", area)
                       .Replace("{controller}", controller)
                       .Replace("{action}", action);

                span.DecorateWebServerSpan(
                    resourceName: resourceName,
                    method: method,
                    host: host,
                    httpUrl: rawUrl,
                    tags,
                    headerTags);

                tags.AspNetAction = action;
                tags.AspNetController = controller;
                tags.AspNetArea = area;
                tags.AspNetRoute = route;

                if (newResourceNamesEnabled)
                {
                    // set the resource name in the HttpContext so TracingHttpModule can update root span
                    var httpContext = System.Web.HttpContext.Current;
                    if (httpContext is not null)
                    {
                        httpContext.Items[SharedConstants.HttpContextPropagatedResourceNameKey] = resourceName;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error populating scope data.");
            }
        }

        private static void OnRequestCompleted(System.Web.HttpContext httpContext, Scope scope, DateTimeOffset finishTime)
        {
            HttpContextHelper.AddHeaderTagsFromHttpResponse(httpContext, scope);
            scope.Span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true);
            scope.Span.Finish(finishTime);
            scope.Dispose();
        }
    }
}

#endif
