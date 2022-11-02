// <copyright file="SecurityTransport.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Transports.Http;
#if !NETFRAMEWORK
using Datadog.Trace.AppSec.Waf;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Datadog.Trace.AppSec
{
    internal partial class SecurityTransport
    {
        private readonly HttpContext _context;

        public SecurityTransport(Security security, HttpContext context, Span span)
        {
            _context = context;
            _security = security;
            _transport = new HttpTransport(context);
            _span = span;
        }

        internal IResult ShouldBlockPathParams(RouteValueDictionary pathParams)
        {
            var args = new Dictionary<string, object> { { AddressesConstants.RequestPathParams, pathParams } };
            return RunWaf(args);
        }

        internal IResult ShouldBlockBody(object body)
        {
            var keysAndValues = BodyExtractor.Extract(body);
            var args = new Dictionary<string, object> { { AddressesConstants.RequestBody, keysAndValues } };
            return RunWaf(args);
        }
    }
}
#endif
