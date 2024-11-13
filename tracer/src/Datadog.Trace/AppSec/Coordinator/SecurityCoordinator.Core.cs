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
using Microsoft.Extensions.Primitives;

namespace Datadog.Trace.AppSec.Coordinator;

internal readonly partial struct SecurityCoordinator
{
    private SecurityCoordinator(Security security, Span span, HttpTransport transport)
    {
        _security = security;
        _localRootSpan = TryGetRoot(span);
        _httpTransport = transport;
    }

    private static bool CanAccessHeaders => true;

    internal static SecurityCoordinator? TryGet(Security security, Span span)
    {
        var context = CoreHttpContextStore.Instance.Get();
        if (context is null)
        {
            Log.Warning("Can't instantiate SecurityCoordinator.Core as no transport has been provided and CoreHttpContextStore.Instance.Get() returned null, make sure HttpContext is available");
            return null;
        }

        return new SecurityCoordinator(security, span, new(context));
    }

    internal static SecurityCoordinator Get(Security security, Span span, HttpContext context) => new(security, span, new HttpTransport(context));

    internal static SecurityCoordinator Get(Security security, Span span, HttpTransport transport) => new(security, span, transport);

    private static object GetHeaderValueForWaf(StringValues value)
    {
        return (value.Count == 1 ? value[0] : value);
    }

    private static object GetHeaderValueForWaf(IHeaderDictionary headers, string currentKey)
    {
        return GetHeaderValueForWaf(headers[currentKey]);
    }

    private static void GetCookieKeyValueFromIndex(IRequestCookieCollection cookies, int i, out string key, out string value)
    {
        var cookie = cookies.ElementAt(i);
        key = cookie.Key;
        value = cookie.Value;
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
        var cookiesDic = ExtractCookiesFromRequest(request);
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

        if (cookiesDic is not null)
        {
            AddAddressIfDictionaryHasElements(AddressesConstants.RequestCookies, cookiesDic);
        }

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
                    return value is true;
                }

                return false;
            }
        }

        internal override int StatusCode => Context.Response.StatusCode;

        internal override IDictionary<string, object>? RouteData => Context.GetRouteData()?.Values;

        internal override bool ReportedExternalWafsRequestHeaders
        {
            get
            {
                if (Context.Items.TryGetValue(ReportedExternalWafsRequestHeadersStr, out var value))
                {
                    return value is bool boolValue && boolValue;
                }

                return false;
            }
            set => Context.Items[ReportedExternalWafsRequestHeadersStr] = value;
        }

        internal override void MarkBlocked() => Context.Items[BlockingAction.BlockDefaultActionName] = true;

        internal override IContext GetAdditiveContext() => Context.Features.Get<IContext>();

        internal override void SetAdditiveContext(IContext additiveContext) => Context.Features.Set(additiveContext);

        internal override IHeadersCollection GetRequestHeaders() => new HeadersCollectionAdapter(Context.Request.Headers);

        internal override IHeadersCollection GetResponseHeaders() => new HeadersCollectionAdapter(Context.Response.Headers);
    }
}
#endif
