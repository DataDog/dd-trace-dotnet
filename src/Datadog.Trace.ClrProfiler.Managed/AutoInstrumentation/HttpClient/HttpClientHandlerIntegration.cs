using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Tagging;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements must be documented
#pragma warning disable SA1201 // Elements must appear in the correct order

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.HttpClient
{
    [InstrumentMethod(
        Assembly = "System.Net.Http",
        Type = "System.Net.Http.HttpClientHandler",
        Method = "SendAsync",
        ReturnTypeName = ClrNames.HttpResponseMessageTask,
        ParametersTypesNames = new[] { ClrNames.HttpRequestMessage, ClrNames.CancellationToken },
        MinimumVersion = "4.0.0",
        MaximumVersion = "5.*.*")]
    public class HttpClientHandlerIntegration
    {
        private const string IntegrationName = "HttpMessageHandler";

        public static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest requestMessage, CancellationToken cancellationToken)
            where TRequest : IHttpRequestMessage
        {
            Scope scope = null;
            HttpTags tags = null;

            if (IsTracingEnabled(requestMessage.Headers))
            {
                scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, requestMessage.Method.Method, requestMessage.RequestUri, IntegrationName, out tags);
                if (scope != null)
                {
                    tags.HttpClientHandlerType = instance.GetType().FullName;

                    // add distributed tracing headers to the HTTP request
                    SpanContextPropagator.Instance.Inject(scope.Span.Context, new HttpHeadersCollection(requestMessage.Headers));
                }
            }

            return new CallTargetState(new IntegrationState(scope, tags));
        }

        public static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse responseMessage, Exception exception, CallTargetState state)
            where TResponse : IHttpResponseMessage
        {
            IntegrationState integrationState = (IntegrationState)state.State;
            if (integrationState.Scope is null)
            {
                return responseMessage;
            }

            try
            {
                if (exception is null)
                {
                    if (integrationState.Tags != null)
                    {
                        integrationState.Tags.HttpStatusCode = HttpTags.ConvertStatusCodeToString(responseMessage.StatusCode);
                    }
                }
                else
                {
                    integrationState.Scope.Span.SetException(exception);
                }
            }
            finally
            {
                integrationState.Scope.Dispose();
            }

            return responseMessage;
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

        private readonly struct IntegrationState
        {
            public readonly Scope Scope;
            public readonly HttpTags Tags;

            public IntegrationState(Scope scope, HttpTags tags)
            {
                Scope = scope;
                Tags = tags;
            }
        }

        /*
         * Duck Typing interfaces
         */
        public interface IHttpRequestMessage
        {
            HttpMethodStruct Method { get; }

            Uri RequestUri { get; }

            IRequestHeaders Headers { get; }
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

        public interface IHttpResponseMessage
        {
            int StatusCode { get; }
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
