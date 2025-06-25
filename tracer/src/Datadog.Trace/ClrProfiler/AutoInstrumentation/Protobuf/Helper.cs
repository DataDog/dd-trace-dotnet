// <copyright file="Helper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Protobuf;

internal static class Helper
{
    /// <summary>
    /// Disable the instrumentation when we detect a protobuf message that is 100% not defined by the customer
    /// (currently we disable it only for Google protobuf itself and Microsoft types)
    /// </summary>
    /// <typeparam name="TMessage">needs to be the raw type (not a DuckType)</typeparam>
    /// <returns>true if the instrumentation has been disabled</returns>
    public static bool DisableInstrumentationIfInternalProtobufType<TMessage>()
    {
        var typeName = typeof(TMessage).FullName;
        if (typeName != null &&
            // Google uses protobuf internally in the protobuf library, we don't want to capture those.
            (typeName.StartsWith("Google.Protobuf.", StringComparison.OrdinalIgnoreCase)
             // Microsoft has some protobuf definitions in https://github.com/microsoft/durabletask-protobuf for instance
          || typeName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)))
        {
            // We disable the integrations once and for all here.
            IntegrationOptions<MessageWriteToIntegration, TMessage>.DisableIntegration();
            IntegrationOptions<MessageMergeFromIntegration, TMessage>.DisableIntegration();
            IntegrationOptions<BufferMessageInternalWriteToIntegration, TMessage>.DisableIntegration();
            IntegrationOptions<BufferMessageInternalMergeFromIntegration, TMessage>.DisableIntegration();
            return true;
        }

        return false;
    }
}
