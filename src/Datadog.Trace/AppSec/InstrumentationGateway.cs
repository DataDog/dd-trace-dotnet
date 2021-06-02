// <copyright file="InstrumentationGateway.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Transport;

namespace Datadog.Trace.AppSec
{
    internal class InstrumentationGateway
    {
        public event EventHandler<InstrumentationGatewayEventArgs> InstrumentationGetwayEvent;

        public void RaiseEvent(IDictionary<string, object> eventData, ITransport transport)
        {
            InstrumentationGetwayEvent?.Invoke(this, new InstrumentationGatewayEventArgs(eventData, transport));
        }
    }
}
