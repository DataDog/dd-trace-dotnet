// <copyright file="LambdaCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    internal class LambdaCommon
    {
        private const string PlaceholderServiceName = "placeholder-service";
        private const string PlaceholderOperationName = "placeholder-operation";
        private const string DefaultJson = "{}";
        private const double ServerlessMaxWaitingFlushTime = 3;

        internal static CallTargetState StartInvocation<TArg>(ILambdaExtensionRequest requestBuilder, TArg payload, IDictionary<string, string> context)
        {
            var json = SerializeObject(payload);
            NotifyExtensionStart(requestBuilder, json, context);
            return new CallTargetState(CreatePlaceholderScope(Tracer.Instance, requestBuilder));
        }

        internal static CallTargetState StartInvocationWithoutEvent(ILambdaExtensionRequest requestBuilder)
        {
            return StartInvocation(requestBuilder, DefaultJson, null);
        }

        internal static CallTargetState StartInvocationOneParameter<TArg>(ILambdaExtensionRequest requestBuilder, TArg eventOrContext)
        {
            var dict = GetTraceContext(eventOrContext);
            if (dict != null)
            {
                return StartInvocation(requestBuilder, DefaultJson, dict);
            }

            return StartInvocation(requestBuilder, eventOrContext, null);
        }

        internal static CallTargetState StartInvocationTwoParameters<TArg1, TArg2>(ILambdaExtensionRequest requestBuilder, TArg1 payload, TArg2 context)
        {
            var dict = GetTraceContext(context);
            return StartInvocation(requestBuilder, payload, dict);
        }

        internal static CallTargetReturn<TReturn> EndInvocationSync<TReturn>(TReturn returnValue, Exception exception, Scope scope, ILambdaExtensionRequest requestBuilder)
        {
            var json = SerializeObject(returnValue);
            scope?.Dispose();
            Flush();
            NotifyExtensionEnd(requestBuilder, exception != null, json);
            return new CallTargetReturn<TReturn>(returnValue);
        }

        internal static TReturn EndInvocationAsync<TReturn>(TReturn returnValue, Exception exception, Scope scope, ILambdaExtensionRequest requestBuilder)
        {
            var json = SerializeObject(returnValue);
            scope?.Dispose();
            Flush();
            NotifyExtensionEnd(requestBuilder, exception != null, json);
            return returnValue;
        }

        internal static CallTargetReturn EndInvocationVoid(Exception exception, Scope scope, ILambdaExtensionRequest requestBuilder)
        {
            scope?.Dispose();
            Flush();
            NotifyExtensionEnd(requestBuilder, exception != null);
            return CallTargetReturn.GetDefault();
        }

        internal static Scope CreatePlaceholderScope(Tracer tracer, ILambdaExtensionRequest requestBuilder)
        {
            Scope scope = null;
            try
            {
                var request = requestBuilder.GetTraceContextRequest();
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    var traceId = response.Headers.Get(HttpHeaderNames.TraceId);
                    // need to set the exact same spanId so nested spans (auto-instrumentation or manual) will have the correct parent-id
                    var spanId = response.Headers.Get(HttpHeaderNames.SpanId);
                    Serverless.Debug($"received traceId = {traceId} and spanId = {spanId}");
                    var span = tracer.StartSpan(PlaceholderOperationName, null, serviceName: PlaceholderServiceName, traceId: Convert.ToUInt64(traceId), spanId: Convert.ToUInt64(spanId), addToTraceContext: false);
                    scope = tracer.TracerManager.ScopeManager.Activate(span, false);
                }
            }
            catch (Exception ex)
            {
                Serverless.Error("Error creating the placeholder scope", ex);
            }

            return scope;
        }

        internal static bool SendStartInvocation(ILambdaExtensionRequest requestBuilder, string data, IDictionary<string, string> context)
        {
            var request = requestBuilder.GetStartInvocationRequest();
            WriteRequestPayload(request, data);
            WriteRequestHeaders(request, context);
            return ValidateOKStatus((HttpWebResponse)request.GetResponse());
        }

        internal static bool SendEndInvocation(ILambdaExtensionRequest requestBuilder, bool isError, string data)
        {
            var request = requestBuilder.GetEndInvocationRequest(isError);
            WriteRequestPayload(request, data);
            return ValidateOKStatus((HttpWebResponse)request.GetResponse());
        }

        private static bool ValidateOKStatus(HttpWebResponse response)
        {
            var statusCode = response.StatusCode;
            Serverless.Debug("The extension responds with statusCode = " + statusCode);
            return statusCode == HttpStatusCode.OK;
        }

        private static void NotifyExtensionStart(ILambdaExtensionRequest requestBuilder, string json, IDictionary<string, string> context)
        {
            try
            {
                if (!SendStartInvocation(requestBuilder, json, context))
                {
                    Serverless.Debug("Extension does not send a status 200 OK");
                }
            }
            catch (Exception ex)
            {
                Serverless.Error("Could not send payload to the extension", ex);
            }
        }

        private static void NotifyExtensionEnd(ILambdaExtensionRequest requestBuilder, bool isError, string json = DefaultJson)
        {
            try
            {
                if (!SendEndInvocation(requestBuilder, isError, json))
                {
                    Serverless.Debug("Extension does not send a status 200 OK");
                }
            }
            catch (Exception ex)
            {
                Serverless.Error("Could not send payload to the extension", ex);
            }
        }

        private static void Flush()
        {
            try
            {
                // here we need a sync flush, since the lambda environment can be destroy after each invocation
                // 3 seconds is enough to send payload to the extension (via localhost)
                Tracer.Instance.TracerManager.AgentWriter.FlushTracesAsync().Wait(TimeSpan.FromSeconds(ServerlessMaxWaitingFlushTime));
            }
            catch (Exception ex)
            {
                Serverless.Error("Could not flush to the extension", ex);
            }
        }

        private static string SerializeObject<T>(T obj)
        {
            try
            {
                return JsonConvert.SerializeObject(obj);
            }
            catch (Exception ex)
            {
                Serverless.Debug("Failed to serialize object with the following error: " + ex.ToString());
                return DefaultJson;
            }
        }

        private static void WriteRequestPayload(WebRequest request, string data)
        {
            var byteArray = Encoding.UTF8.GetBytes(data);
            request.ContentLength = byteArray.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
        }

        private static void WriteRequestHeaders(WebRequest request, IDictionary<string, string> context)
        {
            if (context != null)
            {
                foreach (KeyValuePair<string, string> kv in context)
                {
                    request.Headers.Add(kv.Key, kv.Value);
                }
            }
        }

        private static IDictionary<string, string> GetTraceContext(object obj)
        {
            // Datadog duck typing library
            var proxyInstance = obj.DuckAs<ILambdaContext>();
            return proxyInstance?.ClientContext?.Custom;
        }
    }
}
