using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.AppSec.Transport
{
    internal interface ITransport
    {
        void Block();
    }
}
