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

        public void RaiseEvent(IReadOnlyDictionary<string, object> eventData, ITransport transport)
        {
            InstrumentationGetwayEvent?.Invoke(this, new InstrumentationGatewayEventArgs(eventData, transport));
        }
    }
}
