// <copyright file="DataContractJsonSerializer_WriteObject_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Runtime.Serialization.Json.DataContractJsonSerializer.WriteObject calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Serialization",
        TypeName = "System.Runtime.Serialization.Json.DataContractJsonSerializer",
        MethodName = "WriteObject",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.Stream, ClrNames.Object },
        MinimumVersion = "4",
        MaximumVersion = "4",
        IntegrationName = nameof(IntegrationId.AspNetMvc),
        InstrumentationCategory = InstrumentationCategory.AppSec)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DataContractJsonSerializer_WriteObject_Integration
    {
        private const string MethodName = "System.Runtime.Serialization.Json.DataContractJsonSerializer.WriteObject()";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DataContractJsonSerializer_WriteObject_Integration));

        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, Stream stream, object? graph)
        {
            try
            {
                var security = Security.Instance;
                if (!security.AppsecEnabled || !security.Settings.ApiSecurityParseResponseBody || graph is null)
                {
                    return CallTargetState.GetDefault();
                }

                var httpContext = HttpContext.Current;
                var response = httpContext?.Response;
                if (response is null || !ReferenceEquals(stream, response.OutputStream))
                {
                    return CallTargetState.GetDefault();
                }

                var scope = SharedItems.TryPeekScope(httpContext, AspNetMvcIntegration.HttpContextKey)
                         ?? SharedItems.TryPeekScope(httpContext, AspNetWebApi2Integration.HttpContextKey);

                return scope is null ? CallTargetState.GetDefault() : new CallTargetState(scope, graph);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error instrumenting method {MethodName}", MethodName);
                return CallTargetState.GetDefault();
            }
        }

        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        {
            if (exception is null && state.State is { } graph && state.Scope is { } scope)
            {
                try
                {
                    var security = Security.Instance;
                    var securityTransport = SecurityCoordinator.Get(security, scope.Span, HttpContext.Current);
                    if (!securityTransport.IsBlocked)
                    {
                        var extractedObject = ObjectExtractor.ExtractDataContract(graph);
                        if (extractedObject is not null)
                        {
                            var inputData = new Dictionary<string, object> { { AddressesConstants.ResponseBody, extractedObject } };
                            securityTransport.BlockAndReport(inputData);
                        }
                    }
                }
                catch (Exception ex) when (BlockException.GetBlockException(ex) is null)
                {
                    Log.Error(ex, "Error instrumenting method {MethodName}", MethodName);
                }
            }

            return CallTargetReturn.GetDefault();
        }
    }
}
#endif
