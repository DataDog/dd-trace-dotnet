// <copyright file="InstrumentationGatewaySecurityEventArgs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.AppSec.Transports;
#if NETFRAMEWORK

#else
using Microsoft.AspNetCore.Http;
#endif

namespace Datadog.Trace.AppSec
{
    internal class InstrumentationGatewaySecurityEventArgs : InstrumentationGatewayEventArgs
    {
        private readonly IDictionary<string, object> _eventData;
        private readonly bool _overrideExistingAddress;

        public InstrumentationGatewaySecurityEventArgs(IDictionary<string, object> eventData, ITransport transport, Span relatedSpan, bool overrideExistingAddress = true)
            : base(transport, relatedSpan)
        {
            _eventData = eventData;
            _overrideExistingAddress = overrideExistingAddress;
        }

        public IDictionary<string, object> EventData => _eventData;

        public bool OverrideExistingAddress => _overrideExistingAddress;
    }
}
