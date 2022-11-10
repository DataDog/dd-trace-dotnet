// <copyright file="SecurityTransport.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;
using Datadog.Trace.Util.Http;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.AppSec.Transports;

internal partial class SecurityTransport
{
    private readonly HttpContext _context;

    internal SecurityTransport(Security security, HttpContext context, Span span)
        : this(span, new HttpTransport(context), security) => _context = context;

    private bool CanAccessHeaders => true;

    internal IResult ShouldBlockPathParams(IDictionary<string, object> pathParams)
    {
        if (_httpTransport.Blocked)
        {
            return new NullOkResult();
        }

        var args = new Dictionary<string, object> { { AddressesConstants.RequestPathParams, pathParams } };
        return RunWaf(args);
    }

    internal IResult ShouldBlockBody(object body)
    {
        if (_httpTransport.Blocked)
        {
            return new NullOkResult();
        }

        var keysAndValues = BodyExtractor.Extract(body);
        var args = new Dictionary<string, object> { { AddressesConstants.RequestBody, keysAndValues } };
        return RunWaf(args);
    }

    public void CheckAndBlock(IResult result)
    {
        if (result.ShouldBeReported)
        {
            // todo, report from the filter exception / exception middleware, after exception has been thrown, as theoretically at this point, request hasn't been blocked yet
            Report(result, result.Block);
            if (result.Block)
            {
                throw new BlockException(result);
            }
        }
    }

    public Dictionary<string, object> GetBasicRequestArgsForWaf()
    {
        var request = _context.Request;
        var headersDic = new Dictionary<string, string[]>(request.Headers.Keys.Count);
        foreach (var k in request.Headers.Keys)
        {
            var currentKey = k ?? string.Empty;
            if (!currentKey.Equals("cookie", System.StringComparison.OrdinalIgnoreCase))
            {
                currentKey = currentKey.ToLowerInvariant();
#if NETCOREAPP
                if (!headersDic.TryAdd(currentKey, request.Headers[currentKey]))
                {
#else
                if (!headersDic.ContainsKey(currentKey))
                {
                    headersDic.Add(currentKey, request.Headers[currentKey]);
                }
                else
                {
#endif
                    Log.Warning("Header {key} couldn't be added as argument to the waf", currentKey);
                }
            }
        }

        var cookiesDic = new Dictionary<string, List<string>>(request.Cookies.Keys.Count);
        for (var i = 0; i < request.Cookies.Count; i++)
        {
            var cookie = request.Cookies.ElementAt(i);
            var currentKey = cookie.Key ?? string.Empty;
            var keyExists = cookiesDic.TryGetValue(currentKey, out var value);
            if (!keyExists)
            {
                cookiesDic.Add(currentKey, new List<string> { cookie.Value ?? string.Empty });
            }
            else
            {
                value.Add(cookie.Value);
            }
        }

        var queryStringDic = new Dictionary<string, List<string>>(request.Query.Count);
        // a query string like ?test&[$slice} only fills the key part in dotnetcore and in IIS it only fills the value part, it's been decided to make it a key always
        foreach (var kvp in request.Query)
        {
            var value = kvp.Value;
            var currentKey = kvp.Key ?? string.Empty; // sometimes key can be null

            if (!queryStringDic.TryGetValue(currentKey, out var list))
            {
                queryStringDic.Add(currentKey, new List<string> { value });
            }
            else
            {
                list.Add(value);
            }
        }

        var dict = new Dictionary<string, object>
        {
            { AddressesConstants.RequestMethod, request.Method },
            { AddressesConstants.ResponseStatus, request.HttpContext.Response.StatusCode.ToString() },
            { AddressesConstants.RequestUriRaw, request.GetUrl() },
            { AddressesConstants.RequestQuery, queryStringDic },
            { AddressesConstants.RequestHeaderNoCookies, headersDic },
            { AddressesConstants.RequestCookies, cookiesDic },
            { AddressesConstants.RequestClientIp, _localRootSpan.GetTag(Tags.HttpClientIp) }
        };

        return dict;
    }

    private class HttpTransport : HttpTransportBase
    {
        private readonly HttpContext _context;

        public HttpTransport(HttpContext context) => _context = context;

        internal override bool Blocked => _context.Items["block"] is true;

        internal override void MarkBlocked() => _context.Items["block"] = true;

        internal override IContext GetAdditiveContext() => _context.Features.Get<IContext>();

        internal override void SetAdditiveContext(IContext additiveContext) => _context.Features.Set(additiveContext);

        internal override IHeadersCollection GetRequestHeaders() => new HeadersCollectionAdapter(_context.Request.Headers);

        internal override IHeadersCollection GetResponseHeaders() => new HeadersCollectionAdapter(_context.Response.Headers);
    }
}
#endif
