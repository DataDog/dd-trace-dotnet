// <copyright file="ICodec.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.OpenTracing
{
    internal interface ICodec
    {
        void Inject(global::OpenTracing.ISpanContext spanContext, object carrier);

        global::OpenTracing.ISpanContext Extract(object carrier);
    }
}
