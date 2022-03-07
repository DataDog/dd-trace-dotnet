// <copyright file="IContextExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Propagators
{
    internal interface IContextExtractor
    {
        bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out SpanContext? spanContext)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>;
    }

    internal interface ICarrierGetter<in TCarrier>
    {
        IEnumerable<string?> Get(TCarrier carrier, string key);
    }
}
