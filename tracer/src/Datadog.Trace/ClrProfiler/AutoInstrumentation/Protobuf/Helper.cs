// <copyright file="Helper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Protobuf;

internal class Helper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Helper>();

    private static readonly Lazy<IDescriptorReflectionProxy?> DescriptorReflectionProxy = new(
        () =>
        {
            // prepare incantations to access the static property DescriptorReflection.Descriptor
            var staticType = Type.GetType("Google.Protobuf.Reflection.DescriptorReflection,Google.Protobuf");
            if (staticType == null)
            {
                return null;
            }

            var proxyType = typeof(IDescriptorReflectionProxy);

            var proxyResult = DuckType.GetOrCreateProxyType(proxyType, staticType);
            if (!proxyResult.Success)
            {
                Log.Warning("Cannot create proxy for type Google.Protobuf.Reflection.DescriptorReflection, protobuf instrumentation may malfunction.");
                return null;
            }

            return (IDescriptorReflectionProxy)proxyResult.CreateInstance(null!);
        });

    public interface IDescriptorReflectionProxy
    {
        object? Descriptor { get; } // this is actually a static property
    }

    public static bool TryGetDescriptor(IMessageProxy messageProxy, out MessageDescriptorProxy? descriptor)
    {
        descriptor = null;
        if (messageProxy.Instance is null)
        {
            return false;
        }

        // some public functions that we are instrumenting are also called internally by protobuf,
        // and there is one case where trying to access the descriptor at that point results in a nullref
        // because it relies on this property. We check it here to make sure we're not going to generate an exception by accessing it.
        if (DescriptorReflectionProxy.Value?.Descriptor == null)
        {
            return false;
        }

        // now we know it's safe to access the Descriptor property on the message
        descriptor = messageProxy.Descriptor;
        return true;
    }
}
