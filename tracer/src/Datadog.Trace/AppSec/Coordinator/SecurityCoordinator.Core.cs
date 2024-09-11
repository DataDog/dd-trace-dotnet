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
using Microsoft.AspNetCore.Routing;

namespace Datadog.Trace.AppSec.Coordinator;

internal readonly partial struct SecurityCoordinator
{
    internal SecurityCoordinator(Security security, Span span, HttpTransport? transport = null)
    {
        _security = security;
        _localRootSpan = TryGetRoot(span);
        _httpTransport = transport ?? new HttpTransport(CoreHttpContextStore.Instance.Get());
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

    internal void BlockAndReport(IResult? result)
    {
        if (result is not null)
        {
            if (result.ShouldBlock)
            {
                throw new BlockException(result, result.RedirectInfo ?? result.BlockInfo!);
            }

            TryReport(result, result.ShouldBlock);
        }
    }

    internal void ReportAndBlock(IResult? result)
    {
        if (result is not null)
        {
            TryReport(result, result.ShouldBlock);

            if (result.ShouldBlock)
            {
                throw new BlockException(result, result.RedirectInfo ?? result.BlockInfo!, true);
            }
        }
    }

    private Dictionary<string, object> GetBasicRequestArgsForWaf()
    {
        var request = _httpTransport.Context.Request;
        var headersDic = ExtractHeadersFromRequest(request.Headers);

        var cookiesDic = new Dictionary<string, List<string>>(request.Cookies.Keys.Count);
        for (var i = 0; i < request.Cookies.Count; i++)
        {
            var cookie = request.Cookies.ElementAt(i);
            var currentKey = cookie.Key ?? string.Empty;
            var keyExists = cookiesDic.TryGetValue(currentKey, out var value);
            if (!keyExists)
            {
                cookiesDic.Add(currentKey, [cookie.Value ?? string.Empty]);
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
        public HttpTransport(HttpContext context) => Context = context;

        public override HttpContext Context { get; }

        internal override bool IsBlocked
        {
            get
            {
                if (Context.Items.TryGetValue(BlockingAction.BlockDefaultActionName, out var value))
                {
                    return value is bool boolValue && boolValue;
                }

                return false;
            }
        }

        internal override int StatusCode => Context.Response.StatusCode;

        internal override IDictionary<string, object>? RouteData => Context.GetRouteData()?.Values;

        internal override bool ReportedExternalWafsRequestHeaders
        {
            get => Context.Items["ReportedExternalWafsRequestHeaders"] is true;
            set => Context.Items["ReportedExternalWafsRequestHeaders"] = value;
        }

        internal override void MarkBlocked() => Context.Items[BlockingAction.BlockDefaultActionName] = true;

        internal override IContext GetAdditiveContext() => Context.Features.Get<IContext>();

        internal override void SetAdditiveContext(IContext additiveContext) => Context.Features.Set(additiveContext);

        internal override IHeadersCollection GetRequestHeaders() => new HeadersCollectionAdapter(Context.Request.Headers);

        internal override IHeadersCollection GetResponseHeaders() => new HeadersCollectionAdapter(Context.Response.Headers);
    }
}
#endif
