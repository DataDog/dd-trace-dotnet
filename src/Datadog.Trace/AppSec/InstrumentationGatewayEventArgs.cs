using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Transport;

namespace Datadog.Trace.AppSec
{
    internal class InstrumentationGatewayEventArgs : EventArgs
    {
        public InstrumentationGatewayEventArgs(IReadOnlyDictionary<string, object> eventData, ITransport transport)
        {
            EventData = eventData;
            Transport = transport;
        }

        public IReadOnlyDictionary<string, object> EventData { get; }

        public ITransport Transport { get; }
    }
}
