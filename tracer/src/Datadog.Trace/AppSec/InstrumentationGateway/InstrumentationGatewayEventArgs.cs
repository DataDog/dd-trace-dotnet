// <copyright file="InstrumentationGatewayEventArgs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.AppSec.Transports;

namespace Datadog.Trace.AppSec
{
     internal class InstrumentationGatewayEventArgs : EventArgs
    {
        public InstrumentationGatewayEventArgs(ITransport transport, Span relatedSpan)
        {
            Transport = transport;
            RelatedSpan = relatedSpan;
        }

        public ITransport Transport { get; }

        public Span RelatedSpan { get; }
    }
}
