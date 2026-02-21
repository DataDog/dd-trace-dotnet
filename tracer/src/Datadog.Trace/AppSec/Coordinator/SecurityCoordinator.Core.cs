// <copyright file="SecurityCoordinator.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#pragma warning disable CS0282
#if !NETFRAMEWORK
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;
using Datadog.Trace.Util.Http;
using Datadog.Trace.Vendors.Serilog.Events;
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
        _appsecRequestContext = _localRootSpan.Context.TraceContext.AppSecRequestContext;
        _httpTransport = transport;
        Reporter = new SecurityReporter(_localRootSpan, transport, true);
    }

    internal static SecurityCoordinator? TryGet(Security security, Span span)
    {
        var context = CoreHttpContextStore.Instance.Get();
        if (context is null)
        {
            if (!_nullContextReported)
            {
                Log.Warning("Can't instantiate SecurityCoordinator.Core as no transport has been provided and CoreHttpContextStore.Instance.Get() returned null, make sure HttpContext is available");
                _nullContextReported = true;
            }
            else
            {
                Log.Debug("Can't instantiate SecurityCoordinator.Core as no transport has been provided and CoreHttpContextStore.Instance.Get() returned null, make sure HttpContext is available");
            }

            return null;
        }

        return new SecurityCoordinator(security, span, new(context));
    }

    internal static SecurityCoordinator? TryGetSafe(Security security, Span span)
    {
        if (AspNetCoreAvailabilityChecker.IsAspNetCoreAvailable())
        {
            var secCoord = GetSecurityCoordinatorImpl(security, span);
            return secCoord;
        }

        return null;

        [MethodImpl(MethodImplOptions.NoInlining)]
        SecurityCoordinator? GetSecurityCoordinatorImpl(Security securityImpl, Span spanImpl) => TryGet(securityImpl, spanImpl);
    }

    internal static SecurityCoordinator Get(Security security, Span span, HttpContext context) => new(security, span, new HttpTransport(context));

    internal static SecurityCoordinator Get(Security security, Span span, HttpTransport transport) => new(security, span, transport);

    internal static Dictionary<string, object>? ExtractHeadersFromRequest(IHeaderDictionary headers) => ExtractHeaders(headers.Keys, key => GetHeaderValueForWaf(headers, key));

    private static object GetHeaderAsArray(StringValues value) => value.Count == 1 ? value[0] : value;

    private static object GetHeaderValueForWaf(IHeaderDictionary headers, string currentKey) => GetHeaderAsArray(headers[currentKey]);

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

            Reporter.TryReport(result, result.ShouldBlock);
        }
    }

    internal void ReportAndBlock(IResult? result, Action telemetrySucessReport)
    {
        if (result is not null)
        {
            Reporter.TryReport(result, result.ShouldBlock);

            telemetrySucessReport.Invoke();
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
        var queryStringDic = new Dictionary<string, List<string>>(request.Query?.Count ?? 0);
        // a query string like ?test&[$slice} only fills the key part in dotnetcore and in IIS it only fills the value part, it's been decided to make it a key always

        if (request.Query is not null)
        {
            foreach (var kvp in request.Query)
            {
                var value = kvp.Value;
                var currentKey = kvp.Key ?? string.Empty; // sometimes key can be null

                if (!queryStringDic.TryGetValue(currentKey, out var list))
                {
                    queryStringDic.Add(currentKey, [value]);
                }
                else
                {
                    list.Add(value);
                }
            }
        }

        var addressesDictionary = new Dictionary<string, object>
        {
            { AddressesConstants.RequestMethod, request.Method },
            { AddressesConstants.ResponseStatus, request.HttpContext.Response.StatusCode.ToString() },
            { AddressesConstants.RequestUriRaw, request.GetUrlForWaf() },
            { AddressesConstants.RequestClientIp, _localRootSpan.GetTag(Tags.HttpClientIp) ?? _localRootSpan.GetTag(Tags.NetworkClientIp) }
        };

        AddAddressIfDictionaryHasElements(AddressesConstants.RequestQuery, queryStringDic);
        if (headersDic != null)
        {
            AddAddressIfDictionaryHasElements(AddressesConstants.RequestHeaderNoCookies, headersDic);
        }

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

    internal sealed class HttpTransport(HttpContext context) : HttpTransportBase
    {
        public override HttpContext Context { get; } = context;

        internal override bool IsBlocked
        {
            get => GetItems()?.TryGetValue(BlockingAction.BlockDefaultActionName, out var value) == true && value is true;
        }

        internal override int? StatusCode
        {
            get
            {
                try
                {
                    return Context.Response.StatusCode;
                }
                catch (Exception e) when (e is NullReferenceException or ObjectDisposedException)
                {
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug(e, "Exception while trying to access StatusCode of a Context.Response.");
                    }

                    IsHttpContextDisposed = true;
                    return null;
                }
            }
        }

        internal override IDictionary<string, object>? RouteData => Context.GetRouteData()?.Values;

        internal override bool ReportedExternalWafsRequestHeaders
        {
            get => GetItems()?.TryGetValue(ReportedExternalWafsRequestHeadersStr, out var value) == true && value is true;

            set
            {
                var items = GetItems();
                if (items is not null)
                {
                    items[ReportedExternalWafsRequestHeadersStr] = value;
                }
            }
        }

        private IDictionary<object, object>? GetItems()
        {
            if (IsHttpContextDisposed)
            {
                return null;
            }

            // In some situations the HttpContext could have already been Uninitialized,
            // thus throwing an exception when trying to access the Items
            try
            {
                return Context.Items;
            }
            catch (Exception e) when (e is ObjectDisposedException or NullReferenceException)
            {
                Log.Debug(e, "Exception while trying to access Items of a Context.");
                IsHttpContextDisposed = true;
                return null;
            }
        }

        internal override void MarkBlocked()
        {
            var items = GetItems();
            if (items is not null)
            {
                items[BlockingAction.BlockDefaultActionName] = true;
            }
        }

        internal override IHeadersCollection? GetRequestHeaders()
        {
            try
            {
                return new HeadersCollectionAdapter(Context.Request.Headers);
            }
            catch (Exception e) when (e is ObjectDisposedException or NullReferenceException)
            {
                Log.Debug(e, "Exception while trying to access Items of a Context.");
                IsHttpContextDisposed = true;
                return null;
            }
        }

        internal override IHeadersCollection GetResponseHeaders() => new HeadersCollectionAdapter(Context.Response.Headers);
    }
}
#endif
