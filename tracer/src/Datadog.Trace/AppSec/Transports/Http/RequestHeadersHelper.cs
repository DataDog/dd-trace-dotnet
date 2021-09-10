// <copyright file="RequestHeadersHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.AppSec.Transports.Http
{
    internal class RequestHeadersHelper
    {
        internal static readonly string[] HeadersToSend = new string[] { "user-agent", "referer", "x-forwarded-for", "x-real-ip", "client-ip", "x-forwarded", "x-cluster-client-ip", "forwarded-for", "forwarded", "via" };

        internal static IDictionary<string, string> Get(Func<string, string> getHeader)
        {
            var headersDic = new Dictionary<string, string>();
            foreach (var headerToSend in HeadersToSend)
            {
                var result = getHeader(headerToSend);
                if (!string.IsNullOrEmpty(result))
                {
                    headersDic.Add(headerToSend, result);
                }
            }

            return headersDic;
        }
    }
}
