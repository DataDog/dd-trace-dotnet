using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracer integration for HttpClientHandler.
    /// </summary>
    public static class HttpMessageHandlerIntegration
    {
        private const string SystemNetHttp = "System.Net.Http";
        private const string Major4 = "4";
        private const string Major5 = "5";

        private const string HttpMessageHandlerTypeName = "HttpMessageHandler";
        private const string HttpClientHandlerTypeName = "HttpClientHandler";

        private const string HttpMessageHandler = SystemNetHttp + "." + HttpMessageHandlerTypeName;
        private const string HttpClientHandler = SystemNetHttp + "." + HttpClientHandlerTypeName;
        private const string SendAsync = "SendAsync";
        private const string Send = "Send";

        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.HttpMessageHandler));
        private static readonly IntegrationInfo SocketHandlerIntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.HttpSocketsHandler));

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(HttpMessageHandlerIntegration));
        private static readonly string[] NamespaceAndNameFilters = { ClrNames.GenericTask, ClrNames.HttpRequestMessage, ClrNames.CancellationToken };

        private static Type _httpMessageHandlerResultType;
        private static Type _httpClientHandlerResultType;

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
            TargetMaximumVersion = Major5)]
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
            var httpMessageHandler = handler.GetInstrumentedType(SystemNetHttp, HttpMessageHandlerTypeName);

            Func<object, object, CancellationToken, object> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, CancellationToken, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, SendAsync)
                       .WithConcreteType(httpMessageHandler)
                       .WithParameters(request, cancellationToken)
                       .WithNamespaceAndNameFilters(NamespaceAndNameFilters)
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

            var reportedType = callOpCode == OpCodeValue.Call ? httpMessageHandler : handler.GetType();
            var requestValue = request.As<HttpRequestMessageStruct>();

            var isHttpClientHandler = handler.GetInstrumentedType(SystemNetHttp, HttpClientHandlerTypeName) != null;

            if (!(isHttpClientHandler || IsSocketsHttpHandlerEnabled(reportedType)) ||
                !IsTracingEnabled(requestValue.Headers))
            {
                // skip instrumentation
                return instrumentedMethod(handler, request, cancellationToken);
            }

            Type taskResultType = _httpMessageHandlerResultType;

            if (taskResultType == null || taskResultType.Assembly != httpMessageHandler.Assembly)
            {
                try
                {
                    var currentHttpAssembly = httpMessageHandler.Assembly;
                    taskResultType = currentHttpAssembly.GetType("System.Net.Http.HttpResponseMessage", true);
                    _httpMessageHandlerResultType = taskResultType;
                }
                catch (Exception ex)
                {
                    // This shouldn't happen because the System.Net.Http assembly should have been loaded if this method was called
                    // profiled app will not continue working as expected without this method
                    Log.Error(ex, "Error finding types in the user System.Net.Http assembly.");
                    throw;
                }
            }

            return SendAsyncInternal(
                    instrumentedMethod,
                    reportedType,
                    requestValue,
                    handler,
                    request,
                    cancellationToken)
                .Cast(taskResultType);
        }

        /// <summary>
        /// Instrumentation wrapper for HttpMessageHandler.Send/>.
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
            TargetMethod = Send,
            TargetSignatureTypes = new[] { ClrNames.HttpResponseMessage, ClrNames.HttpRequestMessage, ClrNames.CancellationToken },
            TargetMinimumVersion = Major5,
            TargetMaximumVersion = Major5)]

        public static object HttpMessageHandler_Send(
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
            // HttpResponseMessage HttpMessageHandler.Send(HttpRequestMessage request, CancellationToken cancellationToken)
            var cancellationToken = (CancellationToken)boxedCancellationToken;
            var callOpCode = (OpCodeValue)opCode;
            var httpMessageHandler = handler.GetInstrumentedType(SystemNetHttp, HttpMessageHandlerTypeName);

            Func<object, object, CancellationToken, object> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, CancellationToken, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, Send)
                       .WithConcreteType(httpMessageHandler)
                       .WithParameters(request, cancellationToken)
                       .WithNamespaceAndNameFilters(NamespaceAndNameFilters)
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
                    methodName: Send,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            var reportedType = callOpCode == OpCodeValue.Call ? httpMessageHandler : handler.GetType();
            var requestValue = request.As<HttpRequestMessageStruct>();

            var isHttpClientHandler = handler.GetInstrumentedType(SystemNetHttp, HttpClientHandlerTypeName) != null;

            if (!(isHttpClientHandler || IsSocketsHttpHandlerEnabled(reportedType)) ||
                !IsTracingEnabled(requestValue.Headers))
            {
                // skip instrumentation
                return instrumentedMethod(handler, request, cancellationToken);
            }

            return SendInternal(
                    instrumentedMethod,
                    reportedType,
                    requestValue,
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
            TargetMaximumVersion = Major5)]
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
            var httpClientHandler = handler.GetInstrumentedType(SystemNetHttp, HttpClientHandlerTypeName);

            Func<object, object, CancellationToken, object> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, CancellationToken, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, SendAsync)
                       .WithConcreteType(httpClientHandler)
                       .WithParameters(request, cancellationToken)
                       .WithNamespaceAndNameFilters(NamespaceAndNameFilters)
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

            var requestValue = request.As<HttpRequestMessageStruct>();
            var reportedType = callOpCode == OpCodeValue.Call ? httpClientHandler : handler.GetType();

            if (!IsTracingEnabled(requestValue.Headers))
            {
                // skip instrumentation
                return instrumentedMethod(handler, request, cancellationToken);
            }

            Type taskResultType = _httpClientHandlerResultType;

            if (taskResultType == null || taskResultType.Assembly != httpClientHandler.Assembly)
            {
                try
                {
                    var currentHttpAssembly = httpClientHandler.Assembly;
                    taskResultType = currentHttpAssembly.GetType("System.Net.Http.HttpResponseMessage", true);
                    _httpClientHandlerResultType = taskResultType;
                }
                catch (Exception ex)
                {
                    // This shouldn't happen because the System.Net.Http assembly should have been loaded if this method was called
                    // profiled app will not continue working as expected without this method
                    Log.Error(ex, "Error finding types in the user System.Net.Http assembly.");
                    throw;
                }
            }

            return SendAsyncInternal(
                    instrumentedMethod,
                    reportedType,
                    requestValue,
                    handler,
                    request,
                    cancellationToken)
                .Cast(taskResultType);
        }

        /// <summary>
        /// Instrumentation wrapper for HttpClientHandler.Send.
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
            TargetMethod = Send,
            TargetSignatureTypes = new[] { ClrNames.HttpResponseMessage, ClrNames.HttpRequestMessage, ClrNames.CancellationToken },
            TargetMinimumVersion = Major5,
            TargetMaximumVersion = Major5)]
        public static object HttpClientHandler_Send(
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
            // HttpResponseMessage HttpClientHandler.Send(HttpRequestMessage request, CancellationToken cancellationToken)
            var cancellationToken = (CancellationToken)boxedCancellationToken;
            var callOpCode = (OpCodeValue)opCode;
            var httpClientHandler = handler.GetInstrumentedType(SystemNetHttp, HttpClientHandlerTypeName);

            Func<object, object, CancellationToken, object> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, CancellationToken, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, Send)
                       .WithConcreteType(httpClientHandler)
                       .WithParameters(request, cancellationToken)
                       .WithNamespaceAndNameFilters(NamespaceAndNameFilters)
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
                    methodName: Send,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            var requestValue = request.As<HttpRequestMessageStruct>();
            var reportedType = callOpCode == OpCodeValue.Call ? httpClientHandler : handler.GetType();

            if (!IsTracingEnabled(requestValue.Headers))
            {
                // skip instrumentation
                return instrumentedMethod(handler, request, cancellationToken);
            }

            return SendInternal(
                    instrumentedMethod,
                    reportedType,
                    requestValue,
                    handler,
                    request,
                    cancellationToken);
        }

        private static async Task<object> SendAsyncInternal(
            Func<object, object, CancellationToken, object> sendAsync,
            Type reportedType,
            HttpRequestMessageStruct requestValue,
            object handler,
            object request,
            CancellationToken cancellationToken)
        {
            var httpMethod = requestValue.Method.Method;
            var requestUri = requestValue.RequestUri;

            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, httpMethod, requestUri, IntegrationId, out var tags))
            {
                try
                {
                    if (scope != null)
                    {
                        tags.HttpClientHandlerType = reportedType.FullName;

                        // add distributed tracing headers to the HTTP request
                        SpanContextPropagator.Instance.Inject(scope.Span.Context, new HttpHeadersCollection(requestValue.Headers));
                    }

                    var task = (Task)sendAsync(handler, request, cancellationToken);
                    await task.ConfigureAwait(false);

                    var response = task.As<TaskObjectStruct>().Result;

                    // this tag can only be set after the response is returned
                    int statusCode = response.As<HttpResponseMessageStruct>().StatusCode;

                    if (scope != null)
                    {
                        scope.Span.SetHttpStatusCode(statusCode, isServer: false);
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static object SendInternal(
            Func<object, object, CancellationToken, object> send,
            Type reportedType,
            HttpRequestMessageStruct requestValue,
            object handler,
            object request,
            CancellationToken cancellationToken)
        {
            var httpMethod = requestValue.Method.Method;
            var requestUri = requestValue.RequestUri;

            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, httpMethod, requestUri, IntegrationId, out var tags))
            {
                try
                {
                    if (scope != null)
                    {
                        tags.HttpClientHandlerType = reportedType.FullName;

                        // add distributed tracing headers to the HTTP request
                        SpanContextPropagator.Instance.Inject(scope.Span.Context, new HttpHeadersCollection(requestValue.Headers));
                    }

                    var response = send(handler, request, cancellationToken);

                    // this tag can only be set after the response is returned
                    int statusCode = response.As<HttpResponseMessageStruct>().StatusCode;

                    if (scope != null)
                    {
                        scope.Span.SetHttpStatusCode(statusCode, isServer: false);
                    }

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
            return Tracer.Instance.Settings.IsIntegrationEnabled(SocketHandlerIntegrationId, defaultValue: false)
                && reportedType.FullName.Equals("System.Net.Http.SocketsHttpHandler", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTracingEnabled(IRequestHeaders headers)
        {
            if (headers.TryGetValues(HttpHeaderNames.TracingEnabled, out var headerValues))
            {
                if (headerValues is string[] arrayValues)
                {
                    for (var i = 0; i < arrayValues.Length; i++)
                    {
                        if (string.Equals(arrayValues[i], "false", StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                if (headerValues != null && headerValues.Any(s => string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)))
                {
                    // tracing is disabled for this request via http header
                    return false;
                }
            }

            return true;
        }

        /********************
         * Duck Typing Types
         */
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1600 // Elements must be documented

        [DuckCopy]
        public struct HttpRequestMessageStruct
        {
            public HttpMethodStruct Method;

            public Uri RequestUri;

            public IRequestHeaders Headers;
        }

        [DuckCopy]
        public struct HttpMethodStruct
        {
            public string Method;
        }

        public interface IRequestHeaders
        {
            bool TryGetValues(string name, out IEnumerable<string> values);

            bool Remove(string name);

            void Add(string name, string value);
        }

        [DuckCopy]
        public struct HttpResponseMessageStruct
        {
            public int StatusCode;
        }

        [DuckCopy]
        public struct TaskObjectStruct
        {
            public object Result;
        }

        internal readonly struct HttpHeadersCollection : IHeadersCollection
        {
            private readonly IRequestHeaders _headers;

            public HttpHeadersCollection(IRequestHeaders headers)
            {
                _headers = headers ?? throw new ArgumentNullException(nameof(headers));
            }

            public IEnumerable<string> GetValues(string name)
            {
                if (_headers.TryGetValues(name, out IEnumerable<string> values))
                {
                    return values;
                }

                return Enumerable.Empty<string>();
            }

            public void Set(string name, string value)
            {
                _headers.Remove(name);
                _headers.Add(name, value);
            }

            public void Add(string name, string value)
            {
                _headers.Add(name, value);
            }

            public void Remove(string name)
            {
                _headers.Remove(name);
            }
        }
    }
}
