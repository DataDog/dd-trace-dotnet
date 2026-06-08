// <copyright file="DataContractJsonSerializer_WriteObject_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Web;
using System.Xml;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Runtime.Serialization.XmlObjectSerializer.WriteObject calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Serialization",
        TypeName = "System.Runtime.Serialization.XmlObjectSerializer",
        MethodName = "WriteObject",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.Stream, ClrNames.Object },
        MinimumVersion = "4",
        MaximumVersion = "4",
        IntegrationName = nameof(IntegrationId.AspNetMvc),
        InstrumentationCategory = InstrumentationCategory.AppSec,
        CallTargetIntegrationKind = CallTargetKind.Derived)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DataContractJsonSerializer_WriteObject_Integration
    {
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, Stream stream, object? graph)
        {
            try
            {
                if (!DataContractJsonSerializerWriteObjectCommon.TryGetCaptureContext(instance, graph, out var httpContext, out var response, out var scope, out var useSimpleDictionaryFormat)
                 || !DataContractJsonSerializerWriteObjectCommon.IsResponseOutputStream(stream, response))
                {
                    return CallTargetState.GetDefault();
                }

                return DataContractJsonSerializerWriteObjectCommon.CreateState(graph!, httpContext, scope, useSimpleDictionaryFormat);
            }
            catch (Exception ex)
            {
                DataContractJsonSerializerWriteObjectCommon.LogError(ex);
                return CallTargetState.GetDefault();
            }
        }

        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
            => DataContractJsonSerializerWriteObjectCommon.OnMethodEnd(exception, in state);
    }

    /// <summary>
    /// System.Runtime.Serialization.Json.DataContractJsonSerializer.WriteObject calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Serialization",
        TypeName = "System.Runtime.Serialization.Json.DataContractJsonSerializer",
        MethodName = "WriteObject",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.Xml.XmlDictionaryWriter", ClrNames.Object },
        MinimumVersion = "4",
        MaximumVersion = "4",
        IntegrationName = nameof(IntegrationId.AspNetMvc),
        InstrumentationCategory = InstrumentationCategory.AppSec,
        CallTargetIntegrationKind = CallTargetKind.Default)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable SA1402
    public sealed class DataContractJsonSerializer_WriteObject_XmlDictionaryWriter_Integration
#pragma warning restore SA1402
    {
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, XmlDictionaryWriter writer, object? graph)
        {
            try
            {
                if (!DataContractJsonSerializerWriteObjectCommon.TryGetCaptureContext(instance, graph, out var httpContext, out var response, out var scope, out var useSimpleDictionaryFormat)
                 || !DataContractJsonSerializerWriteObjectCommon.IsResponseOutputWriter(writer, response))
                {
                    return CallTargetState.GetDefault();
                }

                return DataContractJsonSerializerWriteObjectCommon.CreateState(graph!, httpContext, scope, useSimpleDictionaryFormat);
            }
            catch (Exception ex)
            {
                DataContractJsonSerializerWriteObjectCommon.LogError(ex);
                return CallTargetState.GetDefault();
            }
        }

        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
            => DataContractJsonSerializerWriteObjectCommon.OnMethodEnd(exception, in state);
    }

#pragma warning disable SA1402
    internal static class DataContractJsonSerializerWriteObjectCommon
#pragma warning restore SA1402
    {
        private const string MethodName = "System.Runtime.Serialization.Json.DataContractJsonSerializer.WriteObject()";

        // Marks that the response body has already been captured for API Security on this request,
        // so we only extract and run the WAF once per response.
        private const string ResponseBodyCapturedKey = "__Datadog.ApiSecurity.DataContractResponseCaptured";

        private static readonly Type? HttpResponseStreamType = typeof(HttpResponse).Assembly.GetType("System.Web.HttpResponseStream");
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DataContractJsonSerializerWriteObjectCommon));
        private static readonly object CapturedMarker = new();

        internal static CallTargetState CreateState(object graph, HttpContext httpContext, Scope scope, bool useSimpleDictionaryFormat)
        {
            // Both overloads (Stream and XmlDictionaryWriter) fire for a single WriteObject(Stream, ...)
            // call because the Stream overload internally writes through an XmlJsonWriter. Mark the request
            // so the nested/subsequent hook is skipped instead of re-extracting and re-running the WAF.
            httpContext.Items[ResponseBodyCapturedKey] = CapturedMarker;
            return new(scope, new DataContractJsonSerializerState(graph, httpContext, useSimpleDictionaryFormat));
        }

        internal static CallTargetReturn OnMethodEnd(Exception? exception, in CallTargetState state)
        {
            if (exception is null && state.State is DataContractJsonSerializerState serializerState && state.Scope is { } scope)
            {
                try
                {
                    var security = Security.Instance;
                    var securityTransport = SecurityCoordinator.Get(security, scope.Span, serializerState.HttpContext);
                    if (!securityTransport.IsBlocked)
                    {
                        var extractedObject = ObjectExtractor.ExtractDataContract(serializerState.Graph, serializerState.UseSimpleDictionaryFormat);
                        if (extractedObject is not null)
                        {
                            var inputData = new Dictionary<string, object> { { AddressesConstants.ResponseBody, extractedObject } };
                            securityTransport.BlockAndReport(inputData);
                        }
                    }
                }
                catch (Exception ex) when (BlockException.GetBlockException(ex) is null)
                {
                    LogError(ex);
                }
            }

            return CallTargetReturn.GetDefault();
        }

        internal static void LogError(Exception ex)
            => Log.Error(ex, "Error instrumenting method {MethodName}", MethodName);

        internal static bool TryGetCaptureContext<TTarget>(
            TTarget instance,
            object? graph,
            [NotNullWhen(true)] out HttpContext? httpContext,
            [NotNullWhen(true)] out HttpResponse? response,
            [NotNullWhen(true)] out Scope? scope,
            out bool useSimpleDictionaryFormat)
        {
            httpContext = null;
            response = null;
            scope = null;
            useSimpleDictionaryFormat = false;

            if (instance is not DataContractJsonSerializer dcjs)
            {
                return false;
            }

            useSimpleDictionaryFormat = dcjs.UseSimpleDictionaryFormat;

            var security = Security.Instance;
            if (!security.AppsecEnabled || !security.Settings.ApiSecurityParseResponseBody || graph is null)
            {
                return false;
            }

            httpContext = HttpContext.Current;
            if (httpContext is null)
            {
                return false;
            }

            if (httpContext.Items.Contains(ResponseBodyCapturedKey))
            {
                // Response body already captured for API Security on this request.
                return false;
            }

            response = httpContext.Response;
            scope = SharedItems.TryPeekScope(httpContext, AspNetMvcIntegration.HttpContextKey)
                 ?? SharedItems.TryPeekScope(httpContext, AspNetWebApi2Integration.HttpContextKey);
            return scope is not null;
        }

        internal static bool IsResponseOutputStream(Stream stream, HttpResponse response)
        {
            if (ReferenceEquals(stream, response.OutputStream))
            {
                return true;
            }

            if (HttpResponseStreamType is null || !HttpResponseStreamType.IsAssignableFrom(stream.GetType()))
            {
                return false;
            }

            if (!stream.TryDuckCast<HttpResponseStreamStruct>(out var responseStream)
             || responseStream.Writer is not { } writer)
            {
                return false;
            }

            return ReferenceEquals(writer.Response, response);
        }

        internal static bool IsResponseOutputWriter(XmlDictionaryWriter writer, HttpResponse response)
        {
            if (writer.GetType().FullName != "System.Runtime.Serialization.Json.XmlJsonWriter")
            {
                return false;
            }

            if (!writer.TryDuckCast<XmlJsonWriterStruct>(out var jsonWriter)
             || jsonWriter.NodeWriter is not { } nodeWriter)
            {
                return false;
            }

            if (nodeWriter.GetType().FullName != "System.Runtime.Serialization.Json.XmlJsonWriter+JsonNodeWriter")
            {
                return false;
            }

            if (!nodeWriter.TryDuckCast<XmlStreamNodeWriterStruct>(out var streamNodeWriter)
             || streamNodeWriter.Stream is not { } stream)
            {
                return false;
            }

            return IsResponseOutputStream(stream, response);
        }

        [DuckCopy]
        internal struct XmlJsonWriterStruct
        {
            [DuckField(Name = "nodeWriter", FallbackToBaseTypes = true)]
            public object? NodeWriter;
        }

        [DuckCopy]
        internal struct XmlStreamNodeWriterStruct
        {
            [DuckField(Name = "stream", FallbackToBaseTypes = true)]
            public Stream? Stream;
        }

        [DuckCopy]
        internal struct HttpResponseStreamStruct
        {
            [DuckField(Name = "_writer", FallbackToBaseTypes = true)]
            public HttpWriterStruct? Writer;
        }

        [DuckCopy]
        internal struct HttpWriterStruct
        {
            [DuckField(Name = "_response", FallbackToBaseTypes = true)]
            public HttpResponse? Response;
        }

        private sealed class DataContractJsonSerializerState(object graph, HttpContext httpContext, bool useSimpleDictionaryFormat)
        {
            public object Graph { get; } = graph;

            public HttpContext HttpContext { get; } = httpContext;

            public bool UseSimpleDictionaryFormat { get; } = useSimpleDictionaryFormat;
        }
    }
}
#endif
