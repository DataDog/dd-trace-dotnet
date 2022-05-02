// <copyright file="LambdaCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net;
using System.Text;

using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    internal class LambdaCommon
    {
        private const string PlaceholderServiceName = "placeholder-service";
        private const string PlaceholderOperationName = "placeholder-operation";
        private const string DefaultJson = "{}";
        private const double ServerlessMaxWaitingFlushTime = 3;

        internal static CallTargetState StartInvocation<TArg>(TArg payload, ILambdaExtensionRequest requestBuilder)
        {
            var json = DefaultJson;
            try
            {
                json = JsonConvert.SerializeObject(payload);
            }
            catch (Exception)
            {
                Serverless.Debug("Could not serialize input");
            }

            return new CallTargetState(NotifyExtensionStart(requestBuilder, json));
        }

        internal static CallTargetState StartInvocationWithoutEvent(ILambdaExtensionRequest requestBuilder)
        {
            return StartInvocation(DefaultJson, requestBuilder);
        }

        internal static CallTargetReturn<TReturn> EndInvocationSync<TReturn>(TReturn returnValue, Exception exception, Scope scope, ILambdaExtensionRequest requestBuilder)
        {
            scope?.Dispose();
            Flush();
            NotifyExtensionEnd(requestBuilder, scope, exception != null);
            return new CallTargetReturn<TReturn>(returnValue);
        }

        internal static TReturn EndInvocationAsync<TReturn>(TReturn returnValue, Exception exception, Scope scope, ILambdaExtensionRequest requestBuilder)
        {
            scope?.Dispose();
            Flush();
            NotifyExtensionEnd(requestBuilder, scope, exception != null);
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
            Span span = null;
            if (traceId == null || samplingPriority == null)
            {
                Serverless.Debug("samplingPriority and traceId not found, tracer will be responsible for that");
                span = tracer.StartSpan(PlaceholderOperationName, null, serviceName: PlaceholderServiceName, addToTraceContext: false);
                // If we don't add the span to the trace context, then we need to manually call the sampler
                span.Context.TraceContext.SetSamplingPriority(tracer.TracerManager.Sampler?.GetSamplingPriority(span));
            }
            else
            {
                Serverless.Debug($"creating the placeholder span with traceId = {traceId} and samplingPriority = {samplingPriority}");
                span = tracer.StartSpan(PlaceholderOperationName, null, serviceName: PlaceholderServiceName, traceId: Convert.ToUInt64(traceId), addToTraceContext: false);
                span.Context.TraceContext.SetSamplingPriority(Convert.ToInt32(samplingPriority));
            }

            string spanToString = span.ToString();
            Serverless.Debug($"span traceId = {spanToString}");
            return tracer.TracerManager.ScopeManager.Activate(span, false);
        }

        internal static Scope SendStartInvocation(ILambdaExtensionRequest requestBuilder, string data)
        {
            Serverless.Debug("send data = ");
            Serverless.Debug(data);
            var request = requestBuilder.GetStartInvocationRequest();
            var byteArray = Encoding.UTF8.GetBytes(data ?? DefaultJson);
            request.ContentLength = byteArray.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            var traceId = response.Headers.Get(HttpHeaderNames.TraceId);
            Serverless.Debug($"ok trace ID is = {traceId}");
            var samplingPriority = response.Headers.Get(HttpHeaderNames.SamplingPriority);
            Serverless.Debug($"ok samplingPriority is = {samplingPriority}");

            if (ValidateOKStatus(response))
            {
                return CreatePlaceholderScope(Tracer.Instance, traceId, samplingPriority);
            }

            Serverless.Debug("ooopsy valide OK Status is not OK");
            return null;
        }

        internal static bool SendEndInvocation(ILambdaExtensionRequest requestBuilder, Scope scope, bool isError)
        {
            var request = requestBuilder.GetEndInvocationRequest(scope, isError);
            return ValidateOKStatus((HttpWebResponse)request.GetResponse());
        }

        internal static bool ValidateOKStatus(HttpWebResponse response)
        {
            var statusCode = response.StatusCode;
            Serverless.Debug("The extension responds with statusCode = " + statusCode);
            return statusCode == HttpStatusCode.OK;
        }

        internal static Scope NotifyExtensionStart(ILambdaExtensionRequest requestBuilder, string json)
        {
            Scope scope = null;
            try
            {
                scope = SendStartInvocation(requestBuilder, json);
                if (null == scope)
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

        internal static void NotifyExtensionEnd(ILambdaExtensionRequest requestBuilder, Scope scope, bool isError)
        {
            try
            {
                if (!SendEndInvocation(requestBuilder, scope, isError))
                {
                    Serverless.Debug("Extension does not send a status 200 OK");
                }
            }
            catch (Exception ex)
            {
                Serverless.Error("Could not send payload to the extension", ex);
            }
        }

        internal static void Flush()
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
    }
}
