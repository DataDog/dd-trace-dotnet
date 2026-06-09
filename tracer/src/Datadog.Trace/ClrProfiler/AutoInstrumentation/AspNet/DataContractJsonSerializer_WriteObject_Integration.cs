// <copyright file="DataContractJsonSerializer_WriteObject_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

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
        IntegrationName = nameof(IntegrationId.AspNet),
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
}
#endif
