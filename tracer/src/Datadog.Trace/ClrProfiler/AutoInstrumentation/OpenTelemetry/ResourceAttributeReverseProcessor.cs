// <copyright file="ResourceAttributeReverseProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.OpenTelemetry
{
    /// <summary>
    /// The OpenTelemetry processor that copies the resource attributes to the spans, as a reverse duck typing proxy of
    /// OpenTelemetry.BaseProcessor`1[System.Diagnostics.Activity]. Without dynamic code (NativeAOT), the build generates
    /// that proxy instead of the type <see cref="TracerProviderBuilderIntegration"/> emits.
    /// </summary>
    [DuckReverseDelegation("OpenTelemetry.BaseProcessor`1[[System.Diagnostics.Activity, System.Diagnostics.DiagnosticSource]]", "OpenTelemetry")]
    internal sealed class ResourceAttributeReverseProcessor
    {
        private readonly DuckType.CreateTypeResult _baseProcessorProxyType;
        private object? _processor;

        private ResourceAttributeReverseProcessor(DuckType.CreateTypeResult baseProcessorProxyType)
        {
            _baseProcessorProxyType = baseProcessorProxyType;
        }

        public static object Create(Type baseProcessorType)
        {
            // The processor is read as its base type: the proxy type is the build's, not a type the duck type mappings name.
            var implementation = new ResourceAttributeReverseProcessor(DuckType.GetOrCreateProxyType(typeof(BaseProcessorStruct), baseProcessorType));
            var processor = DuckType.CreateReverse(baseProcessorType, implementation);
            implementation._processor = processor;
            return processor;
        }

        [DuckReverseMethod(ParameterTypeNames = new[] { "System.Diagnostics.Activity, System.Diagnostics.DiagnosticSource" })]
        public void OnStart(object data)
        {
            if (_processor is not null && _baseProcessorProxyType.CanCreate())
            {
                ResourceAttributeProcessorHelper.AddResourceAttributes(_baseProcessorProxyType.CreateInstance<BaseProcessorStruct>(_processor), data);
            }
        }
    }
}
