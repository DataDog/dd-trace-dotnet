using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal class ApiWebResponse : IApiResponse
    {
        private HttpWebResponse _response;

        public ApiWebResponse(HttpWebResponse response)
        {
            _response = response;
        }

        public int StatusCode => (int)_response.StatusCode;

        public long ContentLength => _response.ContentLength;

        public async Task<string> ReadAsStringAsync()
        {
            using (var responseStream = _response.GetResponseStream())
            {
                var reader = new StreamReader(responseStream);
                return await reader.ReadToEndAsync();
            }
        }
    }
}
