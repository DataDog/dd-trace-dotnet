// <copyright file="LambdaCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net;
using System.Text;

using Datadog.Trace.ClrProfiler.AutoInstrumentation;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    internal class LambdaCommon
    {
        private const string PlaceholderServiceName = "placeholder-service";
        private const string PlaceholderOperationName = "placeholder-operation";
        private static readonly string DefaultJson = "{}";

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

            NotifyExtensionStart(requestBuilder, json);
            return new CallTargetState(CreatePlaceholderScope(Tracer.Instance, requestBuilder));
        }

        internal static CallTargetState StartInvocationWithoutEvent(ILambdaExtensionRequest requestBuilder)
        {
            return StartInvocation(DefaultJson, requestBuilder);
        }

        internal static CallTargetReturn<TReturn> EndInvocationSync<TReturn>(TReturn returnValue, Exception exception, Scope scope, ILambdaExtensionRequest requestBuilder)
        {
            NotifyExtensionEnd(requestBuilder, exception != null);
            scope?.ServerlessDispose();
            return new CallTargetReturn<TReturn>(returnValue);
        }

        internal static TReturn EndInvocationAsync<TReturn>(TReturn returnValue, Exception exception, Scope scope, ILambdaExtensionRequest requestBuilder)
        {
            NotifyExtensionEnd(requestBuilder, exception != null);
            scope?.ServerlessDispose();
            return returnValue;
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
                    var span = tracer.StartSpan(PlaceholderOperationName, null, serviceName: PlaceholderServiceName, traceId: Convert.ToUInt64(traceId), spanId: Convert.ToUInt64(spanId));
                    scope = tracer.TracerManager.ScopeManager.Activate(span, true);
                }
            }
            catch (Exception ex)
            {
                Serverless.Error("Error creating the placeholder scope", ex);
            }

            return scope;
        }

        internal static bool SendStartInvocation(ILambdaExtensionRequest requestBuilder, string data)
        {
            var request = requestBuilder.GetStartInvocationRequest();
            var byteArray = Encoding.UTF8.GetBytes(data ?? DefaultJson);
            request.ContentLength = byteArray.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
            return ValidateOKStatus((HttpWebResponse)request.GetResponse());
        }

        internal static bool SendEndInvocation(ILambdaExtensionRequest requestBuilder, bool isError)
        {
            var request = requestBuilder.GetEndInvocationRequest(isError);
            return ValidateOKStatus((HttpWebResponse)request.GetResponse());
        }

        internal static bool ValidateOKStatus(HttpWebResponse response)
        {
            var statusCode = response.StatusCode;
            Serverless.Debug("The extension responds with statusCode = " + statusCode);
            return statusCode == HttpStatusCode.OK;
        }

        internal static void NotifyExtensionStart(ILambdaExtensionRequest requestBuilder, string json)
        {
            try
            {
                if (!SendStartInvocation(requestBuilder, json))
                {
                    Serverless.Debug("Extension does not send a status 200 OK");
                }
            }
            catch (Exception ex)
            {
                Serverless.Error("Could not send payload to the extension", ex);
            }
        }

        internal static void NotifyExtensionEnd(ILambdaExtensionRequest requestBuilder, bool isError)
        {
            try
            {
                if (!SendEndInvocation(requestBuilder, isError))
                {
                    Serverless.Debug("Extension does not send a status 200 OK");
                }
            }
            catch (Exception ex)
            {
                Serverless.Error("Could not send payload to the extension", ex);
            }
        }
    }
}
