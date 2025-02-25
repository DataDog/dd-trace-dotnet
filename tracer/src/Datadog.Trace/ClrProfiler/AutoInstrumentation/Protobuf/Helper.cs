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
    /// <typeparam name="TMessage">needs to be the raw type (not a DuckType)</typeparam>
    public static void DisableInstrumentationIfInternalProtobufType<TMessage>()
    {
        var typeName = typeof(TMessage).FullName;
        if (typeName != null && typeName.StartsWith("Google.Protobuf.", StringComparison.OrdinalIgnoreCase))
        {
            // Google uses protobuf internally in the protobuf library, we don't want to capture those.
            // We disable the integrations once and for all here.
            IntegrationOptions<MessageWriteToIntegration, TMessage>.DisableIntegration();
            IntegrationOptions<MessageMergeFromIntegration, TMessage>.DisableIntegration();
            IntegrationOptions<BufferMessageInternalWriteToIntegration, TMessage>.DisableIntegration();
            IntegrationOptions<BufferMessageInternalMergeFromIntegration, TMessage>.DisableIntegration();
        }
    }
}
