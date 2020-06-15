using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Extensions;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracer integration for HttpClientHandler.
    /// </summary>
    public static class HttpMessageHandlerIntegration
    {
        private const string IntegrationName = "HttpMessageHandler";
        private const string SystemNetHttp = "System.Net.Http";
        private const string Major4 = "4";

        private const string HttpMessageHandler = "System.Net.Http.HttpMessageHandler";
        private const string HttpClientHandler = "System.Net.Http.HttpClientHandler";
        private const string SendAsync = "SendAsync";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(HttpMessageHandlerIntegration));

        /// <summary>
        /// Instrumentation wrapper for HttpMessageHandler.SendAsync/>.
        /// </summary>
        /// <param name="handler">The HttpMessageHandler instance to instrument.</param>
        /// <param name="request">The HttpRequestMessage that represents the current HTTP request.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssembly = SystemNetHttp,
            TargetType = HttpMessageHandler,
            TargetMethod = SendAsync,
            TargetSignatureTypes = new[] { ClrNames.HttpResponseMessageTask, ClrNames.HttpRequestMessage, ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object HttpMessageHandler_SendAsync(
            object handler,
            object request,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            // original signature:
            // Task<HttpResponseMessage> HttpMessageHandler.SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            var cancellationToken = (CancellationToken)boxedCancellationToken;
            var callOpCode = (OpCodeValue)opCode;
            var httpMessageHandler = handler.GetInstrumentedType(HttpMessageHandler);
            Type taskResultType;

            try
            {
                var currentHttpAssembly = handler.GetType().Assembly;
                taskResultType = currentHttpAssembly.GetType("System.Net.Http.HttpResponseMessage", true);
            }
            catch (Exception ex)
            {
                // This shouldn't happen because the System.Net.Http assembly should have been loaded if this method was called
                // profiled app will not continue working as expected without this method
                Log.Error(ex, "Error finding types in the user System.Net.Http assembly.");
                throw;
            }

            Func<object, object, CancellationToken, object> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, CancellationToken, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, SendAsync)
                       .WithConcreteType(httpMessageHandler)
                       .WithParameters(request, cancellationToken)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, ClrNames.HttpRequestMessage, ClrNames.CancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: HttpMessageHandler,
                    methodName: SendAsync,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            return AsyncHelper.InvokeGenericTaskDelegate(
                owningType: handler.GetType(),
                taskResultType: taskResultType,
                nameOfIntegrationMethod: nameof(SendAsyncInternal),
                integrationType: typeof(HttpMessageHandlerIntegration),
                instrumentedMethod,
                callOpCode == OpCodeValue.Call ? httpMessageHandler : handler.GetType(),
                handler,
                request,
                cancellationToken);
        }

        /// <summary>
        /// Instrumentation wrapper for HttpClientHandler.SendAsync.
        /// </summary>
        /// <param name="handler">The HttpClientHandler instance to instrument.</param>
        /// <param name="request">The HttpRequestMessage that represents the current HTTP request.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssembly = SystemNetHttp,
            TargetType = HttpClientHandler,
            TargetMethod = SendAsync,
            TargetSignatureTypes = new[] { ClrNames.HttpResponseMessageTask, ClrNames.HttpRequestMessage, ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object HttpClientHandler_SendAsync(
            object handler,
            object request,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            // original signature:
            // Task<HttpResponseMessage> HttpClientHandler.SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            var cancellationToken = (CancellationToken)boxedCancellationToken;
            var callOpCode = (OpCodeValue)opCode;
            var httpClientHandler = handler.GetInstrumentedType(HttpClientHandler);

            Type taskResultType;

            try
            {
                var currentHttpAssembly = handler.GetType().Assembly;
                taskResultType = currentHttpAssembly.GetType("System.Net.Http.HttpResponseMessage", true);
            }
            catch (Exception ex)
            {
                // This shouldn't happen because the System.Net.Http assembly should have been loaded if this method was called
                // profiled app will not continue working as expected without this method
                Log.Error(ex, "Error finding types in the user System.Net.Http assembly.");
                throw;
            }

            Func<object, object, CancellationToken, object> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, CancellationToken, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, SendAsync)
                       .WithConcreteType(httpClientHandler)
                       .WithParameters(request, cancellationToken)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, ClrNames.HttpRequestMessage, ClrNames.CancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: HttpClientHandler,
                    methodName: SendAsync,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            return AsyncHelper.InvokeGenericTaskDelegate(
                owningType: handler.GetType(),
                taskResultType: taskResultType,
                nameOfIntegrationMethod: nameof(SendAsyncInternal),
                integrationType: typeof(HttpMessageHandlerIntegration),
                instrumentedMethod,
                callOpCode == OpCodeValue.Call ? httpClientHandler : handler.GetType(),
                handler,
                request,
                cancellationToken);
        }

        private static async Task<T> SendAsyncInternal<T>(
            Func<object, object, CancellationToken, object> sendAsync,
            Type reportedType,
            object handler,
            object request,
            CancellationToken cancellationToken)
        {
            var headers = request.GetProperty<object>("Headers").GetValueOrDefault();
            if (!(reportedType.FullName.Equals(HttpClientHandler, StringComparison.OrdinalIgnoreCase) || IsSocketsHttpHandlerEnabled(reportedType)) ||
                !IsTracingEnabled(headers))
            {
                // skip instrumentation
                var task = (Task<T>)sendAsync(handler, request, cancellationToken);
                return await task.ConfigureAwait(false);
            }

            var httpMethod = request.GetProperty("Method").GetProperty<string>("Method").GetValueOrDefault();
            var requestUri = request.GetProperty<Uri>("RequestUri").GetValueOrDefault();

            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, httpMethod, requestUri, IntegrationName))
            {
                try
                {
                    if (scope != null)
                    {
                        scope.Span.SetTag("http-client-handler-type", reportedType.FullName);

                        // add distributed tracing headers to the HTTP request
                        SpanContextPropagator.Instance.InjectWithReflection(scope.Span.Context, headers);
                    }

                    var task = (Task<T>)sendAsync(handler, request, cancellationToken);
                    var response = await task.ConfigureAwait(false);

                    // this tag can only be set after the response is returned
                    int statusCode = response.GetProperty<int>("StatusCode").GetValueOrDefault();
                    scope?.Span.SetTag(Tags.HttpStatusCode, (statusCode).ToString());

                    return response;
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static bool IsSocketsHttpHandlerEnabled(Type reportedType)
        {
            return Tracer.Instance.Settings.IsOptInIntegrationEnabled("HttpSocketsHandler") && reportedType.FullName.Equals("System.Net.Http.SocketsHttpHandler", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTracingEnabled(object headers)
        {
            if (headers.CallMethod<string, bool>("Contains", HttpHeaderNames.TracingEnabled).Value)
            {
                var headerValues = headers.CallMethod<string, IEnumerable<string>>("GetValues", HttpHeaderNames.TracingEnabled).Value;
                if (headerValues != null && headerValues.Any(s => string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)))
                {
                    // tracing is disabled for this request via http header
                    return false;
                }
            }

            return true;
        }
    }
}
