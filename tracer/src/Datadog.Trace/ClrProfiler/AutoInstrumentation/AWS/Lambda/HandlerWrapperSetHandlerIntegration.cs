// <copyright file="HandlerWrapperSetHandlerIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util.Delegates;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Lambda;

/// <summary>
/// Instrumenting the HandlerWrapper's GetHandlerWrapper method to wrap the Handler with Datadog wrapping methods
/// </summary>
[InstrumentMethod(
    AssemblyName = "Amazon.Lambda.RuntimeSupport",
    TypeName = "Amazon.Lambda.RuntimeSupport.HandlerWrapper",
    MethodName = "set_Handler",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "Amazon.Lambda.RuntimeSupport.LambdaBootstrapHandler" },
    MinimumVersion = "1.4.0",
    MaximumVersion = "1.*.*",
    IntegrationName = IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class HandlerWrapperSetHandlerIntegration
{
    private const string IntegrationName = nameof(IntegrationId.AwsLambda);
    private static readonly ILambdaExtensionRequest RequestBuilder = new LambdaRequestBuilder();
    private static readonly Async1Callbacks Callbacks = new();

    /// <summary>
    /// OnMethodBegin callback. The input Delegate handler is the customer's handler.
    /// And it's already wrapped by the Amazon.Lambda.RuntimeSupport.HandlerWrapper.
    /// Here we will try further wrap it with our DelegateWrapper class
    /// in order to make it notify the Datadog-Extension before and after the handler's each invocation.
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
    /// <param name="handler">Instance of Amazon.Lambda.RuntimeSupport.LambdaBootstrapHandler</param>
    /// <returns>CallTarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref Delegate handler)
    {
        handler = handler.Instrument(Callbacks);
        return CallTargetState.GetDefault();
    }

    private static string ConvertPayloadStream(Stream payloadStream)
    {
        var reader = new StreamReader(payloadStream, Encoding.UTF8, leaveOpen: true);
        var result = reader.ReadToEnd();
        // Reset the offset so that it can be read by the originally intended consumer, i.e. the user defined handler
        payloadStream.Seek(0, SeekOrigin.Begin);
        return result;
    }

    private readonly struct Async1Callbacks : IBegin1Callbacks, IReturnAsyncCallback, IReturnCallback
    {
        public bool PreserveAsyncContext => false;

        public object OnDelegateBegin<TArg1>(object sender, ref TArg1 arg)
        {
            LambdaCommon.Log("DelegateWrapper Running OnDelegateBegin");

            Scope scope;
            var proxyInstance = arg.DuckCast<IInvocationRequest>();
            if (proxyInstance == null)
            {
                LambdaCommon.Log("DuckCast.IInvocationRequest got null proxyInstance", debug: false);
                scope = LambdaCommon.SendStartInvocation(new LambdaRequestBuilder(), string.Empty, null);
            }
            else
            {
                var jsonString = ConvertPayloadStream(proxyInstance.InputStream);
                scope = LambdaCommon.SendStartInvocation(new LambdaRequestBuilder(), jsonString, proxyInstance.LambdaContext?.ClientContext?.Custom);
            }

            LambdaCommon.Log("DelegateWrapper FINISHED Running OnDelegateBegin");
            return new CallTargetState(scope);
        }

        public void OnException(object sender, Exception ex)
        {
            LambdaCommon.Log("OnDelegateBegin could not send payload to the extension", ex, false);
        }

        public TReturn OnDelegateEnd<TReturn>(object sender, TReturn returnValue, Exception exception, object state)
        {
            // Needed in order to make this Async1Callbacks work with Func1Wrapper, which expects IReturnCallback
            return returnValue;
        }

        /// <inheritdoc/>
        public async Task<TInnerReturn> OnDelegateEndAsync<TInnerReturn>(object sender, TInnerReturn returnValue, Exception exception, object state)
        {
            LambdaCommon.Log("DelegateWrapper Running OnDelegateEndAsync");
            try
            {
                var proxyInstance = returnValue.DuckCast<IInvocationResponse>();
                if (proxyInstance == null)
                {
                    LambdaCommon.Log("DuckCast.IInvocationResponse got null proxyInstance", debug: false);
                    await LambdaCommon.EndInvocationAsync(string.Empty, exception, ((CallTargetState)state!).Scope, RequestBuilder).ConfigureAwait(false);
                }
                else
                {
                    var jsonString = ConvertPayloadStream(proxyInstance.OutputStream);
                    await LambdaCommon.EndInvocationAsync(jsonString, exception, ((CallTargetState)state!).Scope, RequestBuilder).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LambdaCommon.Log("OnDelegateEndAsync could not send payload to the extension", ex, false);
                await LambdaCommon.EndInvocationAsync(string.Empty, ex, ((CallTargetState)state!).Scope, RequestBuilder).ConfigureAwait(false);
            }

            LambdaCommon.Log("DelegateWrapper FINISHED Running OnDelegateEndAsync");
            return returnValue;
        }
    }
}
#endif
