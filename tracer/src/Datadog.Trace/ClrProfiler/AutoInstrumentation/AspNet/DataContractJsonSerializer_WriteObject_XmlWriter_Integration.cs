// <copyright file="DataContractJsonSerializer_WriteObject_XmlWriter_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

using System;
using System.ComponentModel;
using System.Xml;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Runtime.Serialization.Json.DataContractJsonSerializer.WriteObject(XmlWriter, object) calltarget instrumentation.
    /// Handles callers that store the writer in an XmlWriter variable (e.g. JsonReaderWriterFactory.CreateJsonWriter)
    /// and therefore bind to this overload at compile time rather than the XmlDictionaryWriter one.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Serialization",
        TypeName = "System.Runtime.Serialization.Json.DataContractJsonSerializer",
        MethodName = "WriteObject",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.Xml.XmlWriter", ClrNames.Object },
        MinimumVersion = "4",
        MaximumVersion = "4",
        IntegrationName = nameof(IntegrationId.AspNet),
        InstrumentationCategory = InstrumentationCategory.AppSec,
        CallTargetIntegrationKind = CallTargetKind.Default)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DataContractJsonSerializer_WriteObject_XmlWriter_Integration
    {
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, XmlWriter writer, object? graph)
        {
            try
            {
                if (!DataContractJsonSerializerWriteObjectCommon.TryGetCaptureContext(instance, graph, out var httpContext, out var response, out var scope, out var useSimpleDictionaryFormat))
                {
                    return CallTargetState.GetDefault();
                }

                // The writer is a plain XmlWriter at compile-time, but at runtime it may be an
                // XmlDictionaryWriter (e.g. XmlJsonWriter from JsonReaderWriterFactory.CreateJsonWriter).
                // Only capture if the writer resolves to the response output stream.
                if (writer is not XmlDictionaryWriter dictionaryWriter
                 || !DataContractJsonSerializerWriteObjectCommon.IsResponseOutputWriter(dictionaryWriter, response))
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
}
#endif
