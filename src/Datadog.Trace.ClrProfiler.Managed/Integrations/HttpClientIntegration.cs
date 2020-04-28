using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracer integration for HttpClient.
    /// </summary>
    public static class HttpClientIntegration
    {
        private const string IntegrationName = "HttpClient";
        private const string SystemNetHttp = "System.Net.Http";
        private const string Major4 = "4";
        private const string HttpClientTarget = "System.Net.Http.HttpClient";
        private const string DeleteAsync = "DeleteAsync";
        private const string SendAsync = "SendAsync";
        private const string GetAsync = "GetAsync";
        private const string GetByteArrayAsync = "GetByteArrayAsync";
        private const string GetStreamAsync = "GetStreamAsync";
        private const string GetStringAsync = "GetStringAsync";
        private const string PostAsync = "PostAsync";
        private const string PutAsync = "PutAsync";
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(HttpClientIntegration));

        /// <summary>
        /// Instrumentation wrapper for HttpClient.GetStringAsync.
        /// </summary>
        /// <param name="handler">The HttpClient instance to instrument.</param>
        /// <param name="uri">The <see cref="Uri"/> that represents the current request uri.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        TargetAssembly = SystemNetHttp,
        TargetType = HttpClientTarget,
        TargetMethod = GetStringAsync,
        TargetSignatureTypes = new[] { ClrNames.StringTask, ClrNames.Uri },
        TargetMinimumVersion = Major4,
        TargetMaximumVersion = Major4)]
        public static object HttpClient_GetStringAsync(
            object handler,
            object uri,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var callOpCode = (OpCodeValue)opCode;
            var httpClient = handler.GetInstrumentedType(HttpClientTarget);

            Func<HttpClient, Uri, Task<string>> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<HttpClient, Uri, Task<string>>>
                       .Start(moduleVersionPtr, mdToken, opCode, GetStringAsync)
                       .WithConcreteType(httpClient)
                       .WithParameters(uri)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, ClrNames.Uri)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: HttpClientTarget,
                    methodName: GetStringAsync,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            return GetStringAsyncInternal(
                instrumentedMethod,
                reportedType: callOpCode == OpCodeValue.Call ? httpClient : handler.GetType(),
                (HttpClient)handler,
                (Uri)uri);
        }

        private static async Task<string> GetStringAsyncInternal(
            Func<HttpClient, Uri, Task<string>> getStringAsync,
            Type reportedType,
            HttpClient handler,
            Uri uri)
        {
            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, "GET", uri, IntegrationName))
            {
                try
                {
                    string response = await getStringAsync(handler, uri).ConfigureAwait(false);
                    return response;
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Instrumentation wrapper for HttpClient.GetAsync.
        /// </summary>
        /// <param name="handler">The HttpClient.GetAsync instance to instrument.</param>
        /// <param name="uri">The <see cref="Uri"/> that represents the current request uri.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        TargetAssembly = SystemNetHttp,
        TargetType = HttpClientTarget,
        TargetMethod = GetStreamAsync,
        TargetSignatureTypes = new[] { ClrNames.StreamTask, ClrNames.Uri },
        TargetMinimumVersion = Major4,
        TargetMaximumVersion = Major4)]
        public static object HttpClient_GetStreamAsync(
            object handler,
            object uri,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var callOpCode = (OpCodeValue)opCode;
            var httpClient = handler.GetInstrumentedType(HttpClientTarget);

            Func<HttpClient, Uri, Task<Stream>> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<HttpClient, Uri, Task<Stream>>>
                       .Start(moduleVersionPtr, mdToken, opCode, GetStreamAsync)
                       .WithConcreteType(httpClient)
                       .WithParameters(uri)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, ClrNames.Uri)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: HttpClientTarget,
                    methodName: GetStreamAsync,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            return GetStreamAsyncInternal(
                instrumentedMethod,
                reportedType: callOpCode == OpCodeValue.Call ? httpClient : handler.GetType(),
                (HttpClient)handler,
                (Uri)uri);
        }

        private static async Task<Stream> GetStreamAsyncInternal(
            Func<HttpClient, Uri, Task<Stream>> getStreamAsync,
            Type reportedType,
            HttpClient handler,
            Uri uri)
        {
            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, "GET", uri, IntegrationName))
            {
                try
                {
                    Stream response = await getStreamAsync(handler, uri).ConfigureAwait(false);
                    return response;
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Instrumentation wrapper for HttpClient.GetAsync.
        /// </summary>
        /// <param name="handler">The HttpClient.GetAsync instance to instrument.</param>
        /// <param name="uri">The <see cref="Uri"/> that represents the current request uri.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        TargetAssembly = SystemNetHttp,
        TargetType = HttpClientTarget,
        TargetMethod = GetByteArrayAsync,
        TargetSignatureTypes = new[] { ClrNames.ByteArrayTask, ClrNames.Uri },
        TargetMinimumVersion = Major4,
        TargetMaximumVersion = Major4)]
        public static object HttpClient_GetByteArrayAsync(
            object handler,
            object uri,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var callOpCode = (OpCodeValue)opCode;
            var httpClient = handler.GetInstrumentedType(HttpClientTarget);

            Func<HttpClient, Uri, Task<byte[]>> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<HttpClient, Uri, Task<byte[]>>>
                       .Start(moduleVersionPtr, mdToken, opCode, GetByteArrayAsync)
                       .WithConcreteType(httpClient)
                       .WithParameters(uri)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, ClrNames.Uri)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: HttpClientTarget,
                    methodName: GetByteArrayAsync,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            return GetByteArrayAsyncInternal(
                instrumentedMethod,
                reportedType: callOpCode == OpCodeValue.Call ? httpClient : handler.GetType(),
                (HttpClient)handler,
                (Uri)uri);
        }

        private static async Task<byte[]> GetByteArrayAsyncInternal(
            Func<HttpClient, Uri, Task<byte[]>> getByteArrayAsync,
            Type reportedType,
            HttpClient handler,
            Uri uri)
        {
            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, "GET", uri, IntegrationName))
            {
                try
                {
                    byte[] response = await getByteArrayAsync(handler, uri).ConfigureAwait(false);
                    return response;
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Instrumentation wrapper for HttpClient.GetAsync.
        /// </summary>
        /// <param name="handler">The HttpClient.GetAsync instance to instrument.</param>
        /// <param name="uri">The <see cref="Uri"/> that represents the current request uri.</param>
        /// <param name="completionOption">The <see cref="HttpCompletionOption"/> that is passed in the current request.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        TargetAssembly = SystemNetHttp,
        TargetType = HttpClientTarget,
        TargetMethod = GetAsync,
        TargetSignatureTypes = new[] { ClrNames.HttpResponseMessageTask, ClrNames.Uri, ClrNames.HttpCompletionOption, ClrNames.CancellationToken },
        TargetMinimumVersion = Major4,
        TargetMaximumVersion = Major4)]
        public static object HttpClient_GetAsync(
            object handler,
            object uri,
            int completionOption,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var callOpCode = (OpCodeValue)opCode;
            var httpClient = handler.GetInstrumentedType(HttpClientTarget);
            var cancellationToken = (CancellationToken)boxedCancellationToken;
            var compOption = (HttpCompletionOption)completionOption;

            Func<HttpClient, Uri, HttpCompletionOption, CancellationToken, Task<HttpResponseMessage>> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<HttpClient, Uri, HttpCompletionOption, CancellationToken, Task<HttpResponseMessage>>>
                       .Start(moduleVersionPtr, mdToken, opCode, GetAsync)
                       .WithConcreteType(httpClient)
                       .WithParameters(uri, compOption, cancellationToken)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, ClrNames.Uri, ClrNames.HttpCompletionOption, ClrNames.CancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: HttpClientTarget,
                    methodName: GetAsync,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            return GetAsyncInternal(
                instrumentedMethod,
                reportedType: callOpCode == OpCodeValue.Call ? httpClient : handler.GetType(),
                (HttpClient)handler,
                (Uri)uri,
                compOption,
                cancellationToken);
        }

        private static async Task<HttpResponseMessage> GetAsyncInternal(
            Func<HttpClient, Uri, HttpCompletionOption, CancellationToken, Task<HttpResponseMessage>> getAsync,
            Type reportedType,
            HttpClient handler,
            Uri uri,
            HttpCompletionOption completionOption,
            CancellationToken cancellationToken)
        {
            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, "GET", uri, IntegrationName))
            {
                try
                {
                    HttpResponseMessage response = await getAsync(handler, uri, completionOption, cancellationToken).ConfigureAwait(false);

                    // this tag can only be set after the response is returned
                    scope?.Span.SetTag(Tags.HttpStatusCode, ((int)response.StatusCode).ToString());

                    return response;
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Instrumentation wrapper for HttpClient.DeleteAsync.
        /// </summary>
        /// <param name="handler">The <see cref="HttpClient"/> instance to instrument.</param>
        /// <param name="uri">The <see cref="Uri"/> that represents the current request uri.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        TargetAssembly = SystemNetHttp,
        TargetType = HttpClientTarget,
        TargetMethod = DeleteAsync,
        TargetSignatureTypes = new[] { ClrNames.HttpResponseMessageTask, ClrNames.Uri, ClrNames.CancellationToken },
        TargetMinimumVersion = Major4,
        TargetMaximumVersion = Major4)]
        public static object HttpClient_DeleteAsync(
            object handler,
            object uri,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var callOpCode = (OpCodeValue)opCode;
            var httpClient = handler.GetInstrumentedType(HttpClientTarget);
            var cancellationToken = (CancellationToken)boxedCancellationToken;

            Func<HttpClient, Uri, CancellationToken, Task<HttpResponseMessage>> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<HttpClient, Uri, CancellationToken, Task<HttpResponseMessage>>>
                       .Start(moduleVersionPtr, mdToken, opCode, DeleteAsync)
                       .WithConcreteType(httpClient)
                       .WithParameters(uri, cancellationToken)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, ClrNames.Uri, ClrNames.CancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: HttpClientTarget,
                    methodName: DeleteAsync,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            return DeleteAsyncInternal(
                instrumentedMethod,
                reportedType: callOpCode == OpCodeValue.Call ? httpClient : handler.GetType(),
                (HttpClient)handler,
                (Uri)uri,
                cancellationToken);
        }

        private static async Task<HttpResponseMessage> DeleteAsyncInternal(
            Func<HttpClient, Uri, CancellationToken, Task<HttpResponseMessage>> deleteAsync,
            Type reportedType,
            HttpClient handler,
            Uri uri,
            CancellationToken cancellationToken)
        {
            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, "DELETE", uri, IntegrationName))
            {
                try
                {
                    HttpResponseMessage response = await deleteAsync(handler, uri, cancellationToken).ConfigureAwait(false);

                    // this tag can only be set after the response is returned
                    scope?.Span.SetTag(Tags.HttpStatusCode, ((int)response.StatusCode).ToString());

                    return response;
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Instrumentation wrapper for <see cref="HttpClient.PostAsync"/>.
        /// </summary>
        /// <param name="handler">The <see cref="HttpClient"/> instance to instrument.</param>
        /// <param name="uri">The <see cref="Uri"/> that represents the current request uri.</param>
        /// <param name="content">An <see cref="HttpContent"> object.</see>/></param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        TargetAssembly = SystemNetHttp,
        TargetType = HttpClientTarget,
        TargetMethod = PostAsync,
        TargetSignatureTypes = new[] { ClrNames.HttpResponseMessageTask, ClrNames.Uri, ClrNames.HttpContent, ClrNames.CancellationToken },
        TargetMinimumVersion = Major4,
        TargetMaximumVersion = Major4)]
        public static object HttpClient_PostAsync(
            object handler,
            object uri,
            object content,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var cancellationToken = (CancellationToken)boxedCancellationToken;
            var callOpCode = (OpCodeValue)opCode;
            var httpClient = handler.GetInstrumentedType(HttpClientTarget);

            Func<HttpClient, Uri, HttpContent, CancellationToken, Task<HttpResponseMessage>> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<HttpClient, Uri, HttpContent, CancellationToken, Task<HttpResponseMessage>>>
                       .Start(moduleVersionPtr, mdToken, opCode, PostAsync)
                       .WithConcreteType(httpClient)
                       .WithParameters(uri, content, cancellationToken)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, ClrNames.Uri, ClrNames.HttpContent, ClrNames.CancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: HttpClientTarget,
                    methodName: PostAsync,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            return AsyncInternal_PostOrPush(
                instrumentedMethod,
                "POST",
                (HttpClient)handler,
                (Uri)uri,
                (HttpContent)content,
                cancellationToken);
        }

#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Instrumentation wrapper for <see cref="HttpClient.PutAsync"/>.
        /// </summary>
        /// <param name="handler">The <see cref="HttpClient"/> instance to instrument.</param>
        /// <param name="uri">The <see cref="Uri"/> that represents the current request uri.</param>
        /// <param name="content">An <see cref="HttpContent"> object.</see>/></param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        TargetAssembly = SystemNetHttp,
        TargetType = HttpClientTarget,
        TargetMethod = PutAsync,
        TargetSignatureTypes = new[] { ClrNames.HttpResponseMessageTask, ClrNames.Uri, ClrNames.HttpContent, ClrNames.CancellationToken },
        TargetMinimumVersion = Major4,
        TargetMaximumVersion = Major4)]
        public static object HttpClient_PutAsync(
            object handler,
            object uri,
            object content,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var cancellationToken = (CancellationToken)boxedCancellationToken;
            var callOpCode = (OpCodeValue)opCode;
            var httpClient = handler.GetInstrumentedType(HttpClientTarget);

            Func<HttpClient, Uri, HttpContent, CancellationToken, Task<HttpResponseMessage>> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<HttpClient, Uri, HttpContent, CancellationToken, Task<HttpResponseMessage>>>
                       .Start(moduleVersionPtr, mdToken, opCode, PutAsync)
                       .WithConcreteType(httpClient)
                       .WithParameters(uri, content, cancellationToken)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, ClrNames.Uri, ClrNames.HttpContent, ClrNames.CancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: HttpClientTarget,
                    methodName: PutAsync,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            return AsyncInternal_PostOrPush(
                instrumentedMethod,
                "PUT",
                (HttpClient)handler,
                (Uri)uri,
                (HttpContent)content,
                cancellationToken);
        }

#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Instrumentation wrapper for <see cref="HttpClient.SendAsync"/>.
        /// </summary>
        /// <param name="handler">The <see cref="HttpClient"/> instance to instrument.</param>
        /// <param name="request">The <see cref="HttpRequestMessage"/> that represents the current HTTP request.</param>
        /// <param name="completionOption">The <see cref="HttpCompletionOption"/> that is passed in the current request.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
        TargetAssembly = SystemNetHttp,
        TargetType = HttpClientTarget,
        TargetMethod = SendAsync,
        TargetSignatureTypes = new[] { ClrNames.HttpResponseMessageTask, ClrNames.HttpRequestMessage, ClrNames.HttpCompletionOption, ClrNames.CancellationToken },
        TargetMinimumVersion = Major4,
        TargetMaximumVersion = Major4)]
        public static object HttpClient_SendAsync(
            object handler,
            object request,
            int completionOption,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var compOption = (HttpCompletionOption)completionOption;
            var cancellationToken = (CancellationToken)boxedCancellationToken;
            var callOpCode = (OpCodeValue)opCode;
            var httpClient = handler.GetInstrumentedType(HttpClientTarget);

            Func<HttpClient, HttpRequestMessage, HttpCompletionOption, CancellationToken, Task<HttpResponseMessage>> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<HttpClient, HttpRequestMessage, HttpCompletionOption, CancellationToken, Task<HttpResponseMessage>>>
                       .Start(moduleVersionPtr, mdToken, opCode, SendAsync)
                       .WithConcreteType(httpClient)
                       .WithParameters(request, compOption, cancellationToken)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, ClrNames.HttpRequestMessage, ClrNames.HttpCompletionOption, ClrNames.CancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: HttpClientTarget,
                    methodName: SendAsync,
                    instanceType: handler.GetType().AssemblyQualifiedName);
                throw;
            }

            return SendAsyncInternal(
                instrumentedMethod,
                reportedType: callOpCode == OpCodeValue.Call ? httpClient : handler.GetType(),
                (HttpClient)handler,
                (HttpRequestMessage)request,
                compOption,
                cancellationToken);
        }

        private static async Task<HttpResponseMessage> SendAsyncInternal(
            Func<HttpClient, HttpRequestMessage, HttpCompletionOption, CancellationToken, Task<HttpResponseMessage>> sendAsync,
            Type reportedType,
            HttpClient handler,
            HttpRequestMessage request,
            HttpCompletionOption completionOption,
            CancellationToken cancellationToken)
        {
            if (!IsTracingEnabled(request))
            {
                // skip instrumentation
                return await sendAsync(handler, request, completionOption, cancellationToken).ConfigureAwait(false);
            }

            string httpMethod = request.Method?.Method;

            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, httpMethod, request.RequestUri, IntegrationName))
            {
                try
                {
                    HttpResponseMessage response = await sendAsync(handler, request, completionOption, cancellationToken).ConfigureAwait(false);

                    // this tag can only be set after the response is returned
                    scope?.Span.SetTag(Tags.HttpStatusCode, ((int)response.StatusCode).ToString());

                    return response;
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static async Task<HttpResponseMessage> AsyncInternal_PostOrPush(
            Func<HttpClient, Uri, HttpContent, CancellationToken, Task<HttpResponseMessage>> fncAsync,
            string httpVerb,
            HttpClient handler,
            Uri uri,
            HttpContent content,
            CancellationToken cancellationToken)
        {
            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, httpVerb, uri, IntegrationName))
            {
                try
                {
                    HttpResponseMessage response = await fncAsync(handler, uri, content, cancellationToken).ConfigureAwait(false);

                    // this tag can only be set after the response is returned
                    scope?.Span.SetTag(Tags.HttpStatusCode, ((int)response.StatusCode).ToString());

                    return response;
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static bool IsTracingEnabled(HttpRequestMessage request)
        {
            if (request.Headers.TryGetValues(HttpHeaderNames.TracingEnabled, out var headerValues))
            {
                if (headerValues.Any(s => string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)))
                {
                    // tracing is disabled for this request via http header
                    return false;
                }
            }

            return true;
        }
    }
}
