// <copyright file="ApiWebRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;
using static Datadog.Trace.HttpOverStreams.DatadogHttpValues;

namespace Datadog.Trace.Agent.Transports
{
    internal sealed class ApiWebRequest : IApiRequest
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ApiWebRequest>();
        private readonly HttpWebRequest _request;

        public ApiWebRequest(HttpWebRequest request)
        {
            _request = request;
        }

        public void AddHeader(string name, string value)
        {
            _request.Headers.Add(name, value);
        }

        public Task<IApiResponse> GetAsync()
            => SendAsync(method: "GET", contentType: null, contentEncoding: null, state: this, writeBody: null, new CancellationTokenSource(_request.Timeout));

        public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType)
            => PostAsync(bytes, contentType, null);

        public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string? contentEncoding)
            => SendAsync(
                method: "POST",
                contentType,
                contentEncoding,
                state: bytes,
                writeBody: static (requestStream, body) => requestStream.WriteAsync(body.Array!, body.Offset, body.Count));

        public Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression)
            => PostAsJsonAsync(payload, compression, SerializationHelpers.DefaultJsonSettings);

        public Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression, JsonSerializerSettings settings)
        {
            var contentEncoding = compression == MultipartCompression.GZip ? "gzip" : null;
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Sending {Type} data as JSON with compression '{Compression}'", typeof(T).FullName, contentEncoding ?? "none");
            }

            return SendAsync(
                method: "POST",
                contentType: MimeTypes.Json,
                contentEncoding,
                state: new JsonState<T>(payload, settings, compression),
                writeBody: static (reqStream, state) => SerializationHelpers.WriteAsJson(reqStream, state.Payload, state.Settings, state.Compression));
        }

        public Task<IApiResponse> PostAsync(Func<Stream, Task> writeToRequestStream, string contentType, string? contentEncoding, string multipartBoundary)
            => SendAsync(
                method: "POST",
                ContentTypeHelper.GetContentType(contentType, multipartBoundary),
                contentEncoding,
                state: writeToRequestStream,
                writeBody: static (stream, wb) => wb(stream));

        /// <summary>
        /// Send a Post request using multipart form data.
        /// WARNING: Name and FileName of each MultipartFormItem instance must be ASCII encoding compatible.
        /// </summary>
        /// <param name="items">Multipart form data items</param>
        /// <param name="multipartCompression">Multipart compression</param>
        /// <returns>Task with the response</returns>
        public Task<IApiResponse> PostAsync(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None)
        {
            if (items is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(items));
            }

            Log.Debug<int>("Sending multipart form request with {Count} items.", items.Length);

            return SendAsync(
                method: "POST",
                contentType: "multipart/form-data; boundary=" + Boundary,
                contentEncoding: multipartCompression == MultipartCompression.GZip ? "gzip" : null,
                state: new MultipartState(items, multipartCompression),
                writeBody: static (requestStream, state) => WriteMultipartAsync(state.Items, requestStream, state.Compression));
        }

        private static async Task WriteMultipartAsync(MultipartFormItem[] items, Stream reqStream, MultipartCompression multipartCompression)
        {
            if (multipartCompression == MultipartCompression.GZip)
            {
                Log.Debug("Using MultipartCompression.GZip");
                using var gzip = new GZipStream(reqStream, CompressionMode.Compress, leaveOpen: true);
                await WriteToStreamAsync(items, gzip).ConfigureAwait(false);
                await gzip.FlushAsync().ConfigureAwait(false);
                Log.Debug("Compressing multipart payload...");
            }
            else
            {
                await WriteToStreamAsync(items, reqStream).ConfigureAwait(false);
            }
        }

        private static async Task WriteToStreamAsync(MultipartFormItem[] multipartItems, Stream requestStream)
        {
            // Write form request using the boundary
            var boundaryBytes = MultipartBytes.BoundarySeparator;
            var trailerBytes = MultipartBytes.BoundaryTrailer;

            // Write each MultipartFormItem
            var itemsWritten = 0;
            foreach (var item in multipartItems)
            {
                if (!item.IsValid(Log))
                {
                    continue;
                }

                var headerBytes = Encoding.ASCII.GetBytes(
                    item.FileName is not null
                        ? $"Content-Type: {item.ContentType}\r\nContent-Disposition: form-data; name=\"{item.Name}\"; filename=\"{item.FileName}\"\r\n\r\n"
                        : $"Content-Type: {item.ContentType}\r\nContent-Disposition: form-data; name=\"{item.Name}\"\r\n\r\n");

                if (itemsWritten == 0)
                {
                    // If we are writing the first item, we skip the initial `\r\n` in the array
                    await requestStream.WriteAsync(boundaryBytes, 2, boundaryBytes.Length - 2).ConfigureAwait(false);
                }
                else
                {
                    await requestStream.WriteAsync(boundaryBytes, 0, boundaryBytes.Length).ConfigureAwait(false);
                }

                await requestStream.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
                if (item.ContentInBytes is { } arraySegment)
                {
                    Log.Debug("Adding to Multipart Byte Array | Name: {Name} | FileName: {FileName} | ContentType: {ContentType}", item.Name, item.FileName, item.ContentType);
                    await requestStream.WriteAsync(arraySegment.Array!, arraySegment.Offset, arraySegment.Count).ConfigureAwait(false);
                }
                else if (item.ContentInStream is { } stream)
                {
                    Log.Debug("Adding to Multipart Stream | Name: {Name} | FileName: {FileName} | ContentType: {ContentType}", item.Name, item.FileName, item.ContentType);
                    await stream.CopyToAsync(requestStream).ConfigureAwait(false);
                }

                itemsWritten++;
            }

            if (itemsWritten == 0)
            {
                await requestStream.WriteAsync(boundaryBytes, 2, boundaryBytes.Length - 2).ConfigureAwait(false);
            }

            await requestStream.WriteAsync(trailerBytes, 0, trailerBytes.Length).ConfigureAwait(false);
        }

        private void ResetRequest(string method, string? contentType, string? contentEncoding)
        {
            _request.Method = method;
            _request.ContentType = string.IsNullOrEmpty(contentType) ? null : contentType;
            if (string.IsNullOrEmpty(contentEncoding))
            {
                _request.Headers.Remove(HttpRequestHeader.ContentEncoding);
            }
            else
            {
                _request.Headers.Set(HttpRequestHeader.ContentEncoding, contentEncoding);
            }
        }

        private Task<IApiResponse> SendAsync<TState>(string method, string? contentType, string? contentEncoding, TState state, Func<Stream, TState, Task> writeBody)
            => SendAsync(method, contentType, contentEncoding, state, writeBody, new CancellationTokenSource(_request.Timeout));

        [TestingOnly]
        internal Task<IApiResponse> SendAsync(string method, string? contentType, string? contentEncoding, Func<Stream, Task>? writeBody, CancellationTokenSource cts)
            => SendAsync(method, contentType, contentEncoding, state: writeBody, writeBody is null ? null : static (stream, writeFunc) => writeFunc!(stream), cts);

        private async Task<IApiResponse> SendAsync<TState>(string method, string? contentType, string? contentEncoding, TState state, Func<Stream, TState, Task>? writeBody, CancellationTokenSource cts)
        {
            CancellationTokenRegistration registration = default;
            try
            {
                ResetRequest(method, contentType, contentEncoding);

                // The callback runs on a CancellationTokenSource timer thread (or
                // synchronously if the token is already cancelled), so it must swallow
                // exceptions -- nothing observes them there.

                // Note that this deadline is currently _only_ scoped to the "send": connect,
                // writing the request body, and waiting for response headers - ending when
                // SendAsync returns. Reading the response body afterwards is not covered.
                //
                // This diverges slightly from HttpClient, where the timeout covers the _whole_ read
                // currently, however, this _may_ change if we switch to using HttpCompletionMode.
                // ResponseHeadersRead to avoid the full buffering into memory as we do today.
                var deadlineExpired = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var tcsRegistration = cts.Token.Register(x => ((TaskCompletionSource<object?>)x!).TrySetResult(true), deadlineExpired, useSynchronizationContext: false);
                registration = cts.Token.Register(static s => { try { ((HttpWebRequest)s!).Abort(); } catch { } }, _request);

                if (writeBody is not null)
                {
                    using (var requestStream = await _request.GetRequestStreamAsync().ConfigureAwait(false))
                    {
                        var writeBodyTask = writeBody(requestStream, state);
                        var result = await Task.WhenAny(writeBodyTask, deadlineExpired.Task).ConfigureAwait(false);
                        if (result == deadlineExpired.Task)
                        {
                            // The deadline fired before the body producer finished. Don't wait for it any longer - observe
                            // its eventual fault here so it can't surface as an unobserved task exception later.
                            _ = writeBodyTask.ContinueWith(
                                static t => _ = t.Exception,
                                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                            ThrowCancelledException(_request, cts.Token, null);
                        }
                        else
                        {
                            // This has  finished, but not necessarily succesfully, so await it
                            await writeBodyTask.ConfigureAwait(false);
                        }
                    }
                }

                // Call GetResponseAsync(), but make sure we handle cancellation in the mean time
                HttpWebResponse httpWebResponse;
                var getResponseTask = _request.GetResponseAsync();
                var getResponseResult = await Task.WhenAny(getResponseTask, deadlineExpired.Task).ConfigureAwait(false);
                if (getResponseResult == deadlineExpired.Task)
                {
                    // The deadline fired before GetResponseAsync() finished. Don't wait for it any longer:
                    // if it eventually faults, observe the exception so it can't surface as an unobserved
                    // task exception; if it eventually succeeds _despite_ Abort(), dispose the orphaned
                    // response instead of leaking the underlying connection.
                    _ = getResponseTask.ContinueWith(static t => ObserveOrphanedResponse(t), TaskContinuationOptions.ExecuteSynchronously);
                    ThrowCancelledException(_request, cts.Token, null);
                    httpWebResponse = default; // Not reachable, but easiest way to keep the compiler happy
                }
                else
                {
                    httpWebResponse = (HttpWebResponse)await getResponseTask.ConfigureAwait(false);
                }

                // Make sure we don't have a concurrent Abort() before returning - calling Dispose() here waits
                // for in-flight cancellation, and it's safe to call Dispose() more than once.
                registration.Dispose();
                if (cts.IsCancellationRequested)
                {
                    httpWebResponse.Dispose();
                    ThrowCancelledException(_request, cts.Token, null);
                }

                return new ApiWebResponse(httpWebResponse);
            }
            catch (WebException exception)
                when (exception.Status == WebExceptionStatus.ProtocolError && exception.Response != null)
            {
                // Same race as above, and we don't want Abort() to happen after we return response to caller
                registration.Dispose();
                if (cts.IsCancellationRequested)
                {
                    exception.Response.Dispose();
                    ThrowCancelledException(_request, cts.Token, exception);
                }

                // If the exception is caused by an error status code, ignore it and let the caller handle the result
                return new ApiWebResponse((HttpWebResponse)exception.Response);
            }
            catch (Exception ex) when (cts.IsCancellationRequested && ex is not OperationCanceledException)
            {
                ThrowCancelledException(_request, cts.Token, ex);
                return default; // never hit, compiler is stupid
            }
            finally
            {
                registration.Dispose();
                cts.Dispose();
            }

            [DoesNotReturn]
            static void ThrowCancelledException(HttpWebRequest request, CancellationToken token, Exception? innerException)
                => throw new OperationCanceledException($"The request to {request.RequestUri} timed out after {request.Timeout}ms.", innerException, token);

            static void ObserveOrphanedResponse(Task<WebResponse> task)
            {
                if (task.IsFaulted)
                {
                    _ = task.Exception;
                }
                else if (task.Status == TaskStatus.RanToCompletion)
                {
                    task.Result.Dispose();
                }
            }
        }

        private readonly struct JsonState<T>(T payload, JsonSerializerSettings settings, MultipartCompression compression)
        {
            public readonly T Payload = payload;
            public readonly JsonSerializerSettings Settings = settings;
            public readonly MultipartCompression Compression = compression;
        }

        private readonly struct MultipartState(MultipartFormItem[] items, MultipartCompression compression)
        {
            public readonly MultipartFormItem[] Items = items;
            public readonly MultipartCompression Compression = compression;
        }
    }
}
