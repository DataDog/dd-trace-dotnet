// <copyright file="Lambda.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

using System.ComponentModel;
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

            return CallTargetState.GetDefault();
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
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
