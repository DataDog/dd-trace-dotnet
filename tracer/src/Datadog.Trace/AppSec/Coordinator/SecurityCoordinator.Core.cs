// <copyright file="SecurityCoordinator.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#pragma warning disable CS0282
#if !NETFRAMEWORK
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;
using Datadog.Trace.Util.Http;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.AppSec.Coordinator;

internal readonly partial struct SecurityCoordinator
{
    private readonly HttpContext _context;

    internal SecurityCoordinator(Security security, HttpContext context, Span span, HttpTransport? transport = null)
    {
        _context = context;
        _security = security;
        _localRootSpan = TryGetRoot(span);
        _httpTransport = transport ?? new HttpTransport(context);
    }

    private static bool CanAccessHeaders => true;

    public static Dictionary<string, string[]> ExtractHeadersFromRequest(IHeaderDictionary headers)
    {
        var headersDic = new Dictionary<string, string[]>(headers.Keys.Count);
        foreach (var k in headers.Keys)
        {
            var currentKey = k ?? string.Empty;
            if (!currentKey.Equals("cookie", System.StringComparison.OrdinalIgnoreCase))
            {
                currentKey = currentKey.ToLowerInvariant();
#if NETCOREAPP
                if (!headersDic.TryAdd(currentKey, headers[currentKey]))
                {
#else
                if (!headersDic.ContainsKey(currentKey))
                {
                    headersDic.Add(currentKey, headers[currentKey]);
                }
                else
                {
#endif
                    Log.Warning("Header {Key} couldn't be added as argument to the waf", currentKey);
                }
            }
        }

        return headersDic;
    }

    internal void CheckAndBlock(IResult? result)
    {
        if (result is not null)
        {
            if (result!.ShouldBlock)
            {
                throw new BlockException(result);
            }

            TryReport(result, result.ShouldBlock);
        }
    }

    private Dictionary<string, object> GetBasicRequestArgsForWaf()
    {
        var request = _context.Request;
        var headersDic = ExtractHeadersFromRequest(request.Headers);

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
                value?.Add(cookie.Value);
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

        var addressesDictionary = new Dictionary<string, object> { { AddressesConstants.RequestMethod, request.Method }, { AddressesConstants.ResponseStatus, request.HttpContext.Response.StatusCode.ToString() }, { AddressesConstants.RequestUriRaw, request.GetUrlForWaf() }, { AddressesConstants.RequestClientIp, _localRootSpan.GetTag(Tags.HttpClientIp) } };

        var userId = _localRootSpan.Context?.TraceContext?.Tags.GetTag(Tags.User.Id);
        if (!string.IsNullOrEmpty(userId))
        {
            addressesDictionary.Add(AddressesConstants.UserId, userId!);
        }

        AddAddressIfDictionaryHasElements(AddressesConstants.RequestQuery, queryStringDic);
        AddAddressIfDictionaryHasElements(AddressesConstants.RequestHeaderNoCookies, headersDic);
        AddAddressIfDictionaryHasElements(AddressesConstants.RequestCookies, cookiesDic);

        return addressesDictionary;

        void AddAddressIfDictionaryHasElements(string address, IDictionary dic)
        {
            if (dic.Count > 0)
            {
                addressesDictionary.Add(address, dic);
            }
        }
    }

    internal class HttpTransport : HttpTransportBase
    {
        private readonly HttpContext _context;

        public HttpTransport(HttpContext context) => _context = context;

        internal override bool IsBlocked => _context.Items["block"] is true;

        internal override void MarkBlocked() => _context.Items["block"] = true;

        internal override IContext GetAdditiveContext() => _context.Features.Get<IContext>();

        internal override void SetAdditiveContext(IContext additiveContext) => _context.Features.Set(additiveContext);

        internal override IHeadersCollection GetRequestHeaders() => new HeadersCollectionAdapter(_context.Request.Headers);

        internal override IHeadersCollection GetResponseHeaders() => new HeadersCollectionAdapter(_context.Response.Headers);
    }
}
#endif
