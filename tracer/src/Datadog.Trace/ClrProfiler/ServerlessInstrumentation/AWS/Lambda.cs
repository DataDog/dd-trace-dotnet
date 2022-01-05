// <copyright file="Lambda.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Net;

using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    /// <summary>
    /// CallTarget integration for AWS Handler
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "xxx",
        TypeName = "yyy",
        MethodName = "zzz",
        ReturnTypeName = "System.Object",
        ParameterTypeNames = new[] { "System.Object", "System.Object" },
        MinimumVersion = "1",
        MaximumVersion = "5",
        IntegrationName = "AWSLambda")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class Lambda
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="incommingEvent">IncommingEvent value, toto.</param>
        /// <param name="context">Context value, tutu.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, object incommingEvent, object context)
        {
            Console.WriteLine("[from autoinstrumentation] OnMethodBegin");
            Console.WriteLine("[from autoinstrumentation] IncomingEvent dump");
            try
            {
                string jsonString = JsonConvert.SerializeObject(incommingEvent);
                Console.WriteLine(jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Console.WriteLine("[from autoinstrumentation] Context dump");
            try
            {
                string jsonString = JsonConvert.SerializeObject(context);
                Console.WriteLine(jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return new CallTargetState(CreateDummyScope(Tracer.Instance));
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Console.WriteLine("[from autoinstrumentation] OnMethodEnd");
            if (state.Scope != null)
            {
                    state.Scope.Dispose();
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }

        internal static Scope CreateDummyScope(Tracer tracer)
        {
            Scope scope = null;
            try
            {
                string url = "http://127.0.0.1:8124/lambda/traceContext";
                WebRequest request = WebRequest.Create(url);
                request.Credentials = CredentialCache.DefaultCredentials;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Console.WriteLine(response.StatusDescription);

                for (int i = 0; i < response.Headers.Count; ++i)
                {
                    Console.WriteLine("\nHeader Name:{0}, Value :{1}", response.Headers.Keys[i], response.Headers[i]);
                }

                string serviceName = "dummy-service";
                string traceIdFromEnv = response.Headers.Get("X-Datadog-Maxday-TraceID");
                Console.WriteLine("[from autoinstrumentation] traceId = " + traceIdFromEnv);
                string spanIdFromEnv = response.Headers.Get("X-Datadog-Maxday-SpanID");
                Console.WriteLine("[from autoinstrumentation] spanId = " + spanIdFromEnv);
                string requestId = response.Headers.Get("X-Datadog-Maxday-RequestID");
                Console.WriteLine("[from autoinstrumentation] requestId = " + requestId);
                SpanContext context = tracer.CreateSpanContext(null, null, false, Convert.ToUInt64(traceIdFromEnv), null);
                scope = tracer.StartActiveInternal(requestId, parent: context, serviceName: serviceName, tags: null, spanId: Convert.ToUInt64(spanIdFromEnv));
                scope.Span.Type = SpanTypes.Custom;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error creating or populating scope." + ex);
            }

            return scope;
        }
    }
}
