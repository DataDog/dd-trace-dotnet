// <copyright file="HeadersConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.AppSec.Transports.Http
{
    internal static class HeadersConstants
    {
        internal static readonly string[] IpHeaders = new string[] { "x-forwarded-for", "x-real-ip", "client-ip", "x-forwarded", "x-cluster-client-ip", "forwarded-for", "forwarded", "via" };
        internal static readonly IEnumerable<string> OtherHeaders = new string[] { "user-agent", "referer" };
    }
}
