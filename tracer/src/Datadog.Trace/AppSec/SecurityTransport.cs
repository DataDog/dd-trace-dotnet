// <copyright file="SecurityTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.AppSec.Transports.Http;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec
{
    internal partial class SecurityTransport
    {
        private readonly Security _security;
        private readonly Span _span;
        private readonly HttpTransport _transport;

        internal IResult ShouldBlock()
        {
            var args = _transport.PrepareArgsForWaf(_span);
            return RunWaf(args);
        }

        internal IResult RunWaf(Dictionary<string, object> args)
        {
            LogAddressIfDebugEnabled(args);
            return _security.RunWaf(_transport, _span, args);
        }

        internal void Report(IResult result, bool blocked) => _security.Report(_transport, _span, result, blocked);

        internal void AddResponseHeaderTags() => _security.AddResponseHeaderTags(_transport, _span);

        private static void LogAddressIfDebugEnabled(IDictionary<string, object> args)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                foreach (var key in args.Keys)
                {
                    Log.Debug("DDAS-0008-00: Pushing address {Key} to the Instrumentation Gateway.", key);
                }
            }
        }
    }
}
