using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal class ApiWebRequestFactory : IApiRequestFactory
    {
        public IApiRequest Create(Uri endpoint)
        {
            return new ApiWebRequest((HttpWebRequest)WebRequest.Create(endpoint));
        }
    }
}
