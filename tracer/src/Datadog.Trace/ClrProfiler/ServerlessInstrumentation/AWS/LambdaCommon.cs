// <copyright file="LambdaCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Datadog.Trace.Agent.Transports;
using Datadog.Trace.ClrProfiler.AutoInstrumentation;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    internal class LambdaCommon
    {
        private const string EndInvocationPath = "/lambda/end-invocation";
        private const string StartInvocationPath = "/lambda/start-invocation";
        private const string TraceContextPath = "/trace-context";
        private const string TraceContextUri = "http://127.0.0.1:8124";
        private const string TraceContextUriEnvName = "_DD_TRACE_CONTEXT_ENDPOINT";
        private const string PlaceholderServiceName = "placeholder-service";
        private const string PlaceholderOperationName = "placeholder-operation";
        private static readonly string DefaultJson = "{}";

        internal static Scope CreatePlaceholderScope(Tracer tracer)
        {
            Scope scope = null;
            try
            {
                var uri = EnvironmentHelpers.GetEnvironmentVariable(TraceContextUriEnvName) ?? TraceContextUri;
                WebRequest request = WebRequest.Create(uri + TraceContextPath);
                request.Credentials = CredentialCache.DefaultCredentials;
                request.Headers.Set(HttpHeaderNames.TracingEnabled, "false");
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    string traceId = response.Headers.Get(HttpHeaderNames.TraceId);
                    // need to set the exact same spanId so nested spans (auto-instrumentation or manual) will have the correct parent-id
                    string spanId = response.Headers.Get(HttpHeaderNames.SpanId);
                    Serverless.Debug("received traceId = " + traceId + " and span id = " + spanId);
                    var span = tracer.StartSpan(PlaceholderOperationName, null, serviceName: PlaceholderServiceName, traceId: Convert.ToUInt64(traceId), spanId: Convert.ToUInt64(spanId));
                    scope = tracer.TracerManager.ScopeManager.Activate(span, false);
                }
            }
            catch (Exception ex)
            {
                Serverless.Error("Error creating the placeholder scope." + ex);
            }

            return scope;
        }

        internal static CallTargetState StartInvocation<TArg>(TArg payload)
        {
            var uri = EnvironmentHelpers.GetEnvironmentVariable(TraceContextUriEnvName) ?? TraceContextUri;
            try
            {
                string json = null;
                try
                {
                    json = JsonConvert.SerializeObject(payload);
                }
                catch (Exception ex)
                {
                    Serverless.Debug("Could not serialize input : " + ex.Message);
                }

                if (!Post(uri + StartInvocationPath, json))
                {
                    Serverless.Debug("Extension does not send a status 200 OK");
                }
            }
            catch (Exception ex)
            {
                Serverless.Error("Could not send payload to the extension : " + ex.Message);
            }

            return new CallTargetState(CreatePlaceholderScope(Tracer.Instance));
        }

        internal static CallTargetState StartInvocationWithoutEvent()
        {
            var uri = EnvironmentHelpers.GetEnvironmentVariable(TraceContextUriEnvName) ?? TraceContextUri;
            if (!Post(uri + StartInvocationPath))
            {
                Serverless.Debug("Extension does not send a status 200 OK");
            }

            return new CallTargetState(CreatePlaceholderScope(Tracer.Instance));
        }

        internal static CallTargetReturn<TReturn> EndInvocation<TReturn>(TReturn returnValue, Exception exception, Scope scope)
        {
            bool isError;
            if (returnValue != null && typeof(TReturn).IsSubclassOf(typeof(Task)))
            {
                Serverless.Debug("End invocation, task detected, wait for completion");
                var task = (Task)Convert.ChangeType(returnValue, typeof(Task));
                while (!task.IsCompleted) { }

                Serverless.Debug("Completed");
                isError = task.Status == TaskStatus.Faulted;
            }
            else
            {
                Serverless.Debug("End invocation, no task detected");
                Serverless.Debug(typeof(TReturn).ToString());
                Serverless.Debug(typeof(TReturn).FullName);
                isError = exception != null;
            }

            Serverless.Debug("Marking as " + isError);
            var uri = EnvironmentHelpers.GetEnvironmentVariable(TraceContextUriEnvName) ?? TraceContextUri;
            try
            {
                if (!Post(uri + EndInvocationPath, isError: isError))
                {
                    Serverless.Error("Extension does not send a 200 OK status");
                }
            }
            catch (Exception ex)
            {
                Serverless.Error("Could not send payload to the extension : " + ex.Message);
            }

            scope?.ServerlessDispose();
            return new CallTargetReturn<TReturn>(returnValue);
        }

        private static bool Post(string url, string data = null, bool isError = false)
        {
            Serverless.Debug("Sending POST request to " + url);
            Serverless.Debug("With data = " + data);
            WebRequest request = WebRequest.Create(url);
            request.Method = "POST";
            request.Headers.Set(HttpHeaderNames.TracingEnabled, "false");
            byte[] byteArray = Encoding.UTF8.GetBytes(data ?? DefaultJson);
            request.ContentType = MimeTypes.Json;
            request.ContentLength = byteArray.Length;
            if (isError)
            {
                request.Headers.Set(HttpHeaderNames.InvocationError, "true");
            }

            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
            WebResponse response = request.GetResponse();
            var statusCode = ((HttpWebResponse)response).StatusCode;
            Serverless.Debug("The extension responds with statusCode = " + statusCode);
            return statusCode == HttpStatusCode.OK;
        }
    }
}
