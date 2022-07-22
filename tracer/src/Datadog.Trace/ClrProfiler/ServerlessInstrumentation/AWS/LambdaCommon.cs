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
using Datadog.Trace.Sampling;
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
            return new CallTargetState(NotifyExtensionStart(requestBuilder, json, context));
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
            Flush();
            NotifyExtensionEnd(requestBuilder, scope, exception != null, json);
            scope?.Dispose();
            return new CallTargetReturn<TReturn>(returnValue);
        }

        internal static TReturn EndInvocationAsync<TReturn>(TReturn returnValue, Exception exception, Scope scope, ILambdaExtensionRequest requestBuilder)
        {
            var json = SerializeObject(returnValue);
            Flush();
            NotifyExtensionEnd(requestBuilder, scope, exception != null, json);
            scope?.Dispose();
            return returnValue;
        }

        internal static CallTargetReturn EndInvocationVoid(Exception exception, Scope scope, ILambdaExtensionRequest requestBuilder)
        {
            scope?.Dispose();
            Flush();
            NotifyExtensionEnd(requestBuilder, scope, exception != null);
            return CallTargetReturn.GetDefault();
        }

        internal static Scope CreatePlaceholderScope(Tracer tracer, string traceId, string samplingPriority)
        {
            Span span;

            if (traceId == null)
            {
                Serverless.Debug("traceId not found");
                span = tracer.StartSpan(PlaceholderOperationName, tags: null, serviceName: PlaceholderServiceName, addToTraceContext: false);
            }
            else
            {
                Serverless.Debug($"creating the placeholder traceId = {traceId}");
                span = tracer.StartSpan(PlaceholderOperationName, tags: null, serviceName: PlaceholderServiceName, traceId: Convert.ToUInt64(traceId), addToTraceContext: false);
            }

            if (samplingPriority == null)
            {
                Serverless.Debug("samplingPriority not found");
                span.Context.TraceContext?.SetSamplingDecision(tracer.TracerManager.Sampler?.MakeSamplingDecision(span));
            }
            else
            {
                Serverless.Debug($"setting the placeholder sampling priority to = {samplingPriority}");

                // TODO: figure out SamplingMechanism, do we need to propagate it here?
                span.Context.TraceContext?.SetSamplingDecision(Convert.ToInt32(samplingPriority), SamplingMechanism.Unknown, rate: null, notifyDistributedTracer: false);
            }

            return tracer.TracerManager.ScopeManager.Activate(span, false);
        }

        internal static Scope SendStartInvocation(ILambdaExtensionRequest requestBuilder, string data, IDictionary<string, string> context)
        {
            var request = requestBuilder.GetStartInvocationRequest();
            WriteRequestPayload(request, data);
            WriteRequestHeaders(request, context);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            var traceId = response.Headers.Get(HttpHeaderNames.TraceId);
            var samplingPriority = response.Headers.Get(HttpHeaderNames.SamplingPriority);
            if (ValidateOKStatus(response))
            {
                return CreatePlaceholderScope(Tracer.Instance, traceId, samplingPriority);
            }

            return null;
        }

        internal static bool SendEndInvocation(ILambdaExtensionRequest requestBuilder, Scope scope, bool isError, string data)
        {
            var request = requestBuilder.GetEndInvocationRequest(scope, isError);
            WriteRequestPayload(request, data);
            return ValidateOKStatus((HttpWebResponse)request.GetResponse());
        }

        private static bool ValidateOKStatus(HttpWebResponse response)
        {
            var statusCode = response.StatusCode;
            Serverless.Debug("The extension responds with statusCode = " + statusCode);
            return statusCode == HttpStatusCode.OK;
        }

        private static Scope NotifyExtensionStart(ILambdaExtensionRequest requestBuilder, string json, IDictionary<string, string> context)
        {
            Scope scope = null;
            try
            {
                scope = SendStartInvocation(requestBuilder, json, context);
                if (scope == null)
                {
                    Serverless.Debug("Error while creating the scope");
                }
            }
            catch (Exception ex)
            {
                Serverless.Error("Could not send payload to the extension", ex);
            }

            return scope;
        }

        private static void NotifyExtensionEnd(ILambdaExtensionRequest requestBuilder, Scope scope, bool isError, string json = DefaultJson)
        {
            try
            {
                if (!SendEndInvocation(requestBuilder, scope, isError, json))
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
