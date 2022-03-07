// <copyright file="IContextInjector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Propagators
{
    internal interface IContextInjector
    {
        void Inject<TCarrier, TCarrierSetter>(SpanContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>;
    }

    internal interface ICarrierSetter<in TCarrier>
    {
        void Set(TCarrier carrier, string key, string value);
    }
}
