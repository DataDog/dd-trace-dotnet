// <copyright file="SecurityCoordinator.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Coordinator;

internal readonly partial struct SecurityCoordinator
{
    private static readonly bool? UsingIntegratedPipeline;
    private readonly HttpContext _context = null;

    static SecurityCoordinator()
    {
        if (UsingIntegratedPipeline == null)
        {
            try
            {
                UsingIntegratedPipeline = TryGetUsingIntegratedPipelineBool();
            }
            catch (Exception ex)
            {
                UsingIntegratedPipeline = false;
                Log.Error(ex, "Unable to query the IIS pipeline. Request and response information may be limited.");
            }
        }
    }

    internal SecurityCoordinator(Security security, HttpContext context, Span span)
    {
        _context = context;
        _security = security;
        _localRootSpan = TryGetRoot(span);
        _httpTransport = new HttpTransport(context);
    }

    private bool CanAccessHeaders => UsingIntegratedPipeline is true or null;

    /// <summary>
    /// ! This method should be called from within a try-catch block !
    /// If the application is running in partial trust, then trying to call this method will result in
    /// a SecurityException to be thrown at the method CALLSITE, not inside the <c>TryGetUsingIntegratedPipelineBool(..)</c> method itself.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryGetUsingIntegratedPipelineBool() => HttpRuntime.UsingIntegratedPipeline;

    internal Dictionary<string, object> GetBodyFromRequest()
    {
        var formData = new Dictionary<string, object>(_context.Request.Form.Keys.Count);
        foreach (string key in _context.Request.Form.Keys)
        {
            formData.Add(key, _context.Request.Form[key]);
        }

        return formData;
    }

    internal IDictionary<string, object> GetPathParams() => _context.Request.RequestContext.RouteData.Values.ToDictionary(c => c.Key, c => c.Value);

    /// <summary>
    /// Initializes a new instance of the <see cref="Coordinator.SecurityCoordinator"/> class.
    /// framework can do it all at once, but framework only unfortunately
    /// </summary>
    internal void CheckAndBlock(Dictionary<string, object> args)
    {
        using var result = RunWaf(args);
        if (result.ShouldBeReported)
        {
            var blocked = false;
            if (result.ShouldBlock)
            {
                blocked = WriteAndEndResponse();
                _httpTransport.MarkBlocked();
            }

            Report(result, blocked);
        }
    }

    private bool WriteAndEndResponse()
    {
        var httpResponse = _context.Response;
        httpResponse.Clear();
        httpResponse.Cookies.Clear();

        var template = _security.Settings.BlockedJsonTemplate;
        if (CanAccessHeaders)
        {
            // cant clear headers, on some iis version we get a platform not supported exception
            var keys = httpResponse.Headers.Keys.Cast<string>().ToList();
            foreach (var key in keys)
            {
                httpResponse.Headers.Remove(key);
            }

            if (_context.Request.Headers["Accept"] == "text/html")
            {
                httpResponse.ContentType = "text/html";
                template = _security.Settings.BlockedHtmlTemplate;
            }
            else
            {
                httpResponse.ContentType = "application/json";
            }
        }
        else
        {
            httpResponse.ContentType = "application/json";
        }

        httpResponse.StatusCode = 403;
        httpResponse.Write(template);

        httpResponse.Flush();
        httpResponse.Close();
        _context.ApplicationInstance.CompleteRequest();
        return true;
    }

    public Dictionary<string, object> GetBasicRequestArgsForWaf()
    {
        var request = _context.Request;
        var headersDic = new Dictionary<string, string[]>(request.Headers.Keys.Count);
        var headerKeys = request.Headers.Keys;
        foreach (string originalKey in headerKeys)
        {
            var keyForDictionary = originalKey ?? string.Empty;
            if (!keyForDictionary.Equals("cookie", System.StringComparison.OrdinalIgnoreCase))
            {
                keyForDictionary = keyForDictionary.ToLowerInvariant();
                if (!headersDic.ContainsKey(keyForDictionary))
                {
                    headersDic.Add(keyForDictionary, request.Headers.GetValues(originalKey));
                }
                else
                {
                    Log.Warning("Header {key} couldn't be added as argument to the waf", keyForDictionary);
                }
            }
        }

        var cookiesDic = new Dictionary<string, List<string>>(request.Cookies.AllKeys.Length);
        for (var i = 0; i < request.Cookies.Count; i++)
        {
            var cookie = request.Cookies[i];
            var keyForDictionary = cookie.Name ?? string.Empty;
            var keyExists = cookiesDic.TryGetValue(keyForDictionary, out var value);
            if (!keyExists)
            {
                cookiesDic.Add(keyForDictionary, new List<string> { cookie.Value ?? string.Empty });
            }
            else
            {
                value.Add(cookie.Value);
            }
        }

        var queryDic = new Dictionary<string, string[]>(request.QueryString.AllKeys.Length);
        foreach (var originalKey in request.QueryString.AllKeys)
        {
            var values = request.QueryString.GetValues(originalKey);
            if (string.IsNullOrEmpty(originalKey))
            {
                foreach (var v in values)
                {
                    if (!queryDic.ContainsKey(v))
                    {
                        queryDic.Add(v, new string[0]);
                    }
                }
            }
            else if (!queryDic.ContainsKey(originalKey))
            {
                queryDic.Add(originalKey, values);
            }
            else
            {
                Log.Warning("Query string {key} couldn't be added as argument to the waf", originalKey);
            }
        }

        var dict = new Dictionary<string, object>(capacity: 7)
        {
            { AddressesConstants.RequestMethod, request.HttpMethod },
            { AddressesConstants.RequestUriRaw, request.Url.AbsoluteUri },
            { AddressesConstants.RequestQuery, queryDic },
            { AddressesConstants.ResponseStatus, request.RequestContext.HttpContext.Response.StatusCode.ToString() },
            { AddressesConstants.RequestHeaderNoCookies, headersDic },
            { AddressesConstants.RequestCookies, cookiesDic },
            { AddressesConstants.RequestClientIp, _localRootSpan.GetTag(Tags.HttpClientIp) }
        };

        return dict;
    }

    internal class HttpTransport : HttpTransportBase
    {
        private const string WafKey = "waf";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<HttpTransport>();

        private static bool _canReadHttpResponseHeaders = true;

        private readonly HttpContext _context;

        public HttpTransport(HttpContext context) => _context = context;

        internal override bool IsBlocked => _context.Items["block"] is true;

        internal override void MarkBlocked() => _context.Items["block"] = true;

        internal override IContext GetAdditiveContext() => _context.Items[WafKey] as IContext;

        internal override void SetAdditiveContext(IContext additiveContext) => _context.Items[WafKey] = additiveContext;

        internal override IHeadersCollection GetRequestHeaders() => new NameValueHeadersCollection(_context.Request.Headers);

        internal override IHeadersCollection GetResponseHeaders()
        {
            if (_canReadHttpResponseHeaders)
            {
                try
                {
                    var headers = _context.Response.Headers;
                    return new NameValueHeadersCollection(_context.Response.Headers);
                }
                catch (PlatformNotSupportedException ex)
                {
                    // Despite the HttpRuntime.UsingIntegratedPipeline check, we can still fail to access response headers, for example when using Sitefinity: "This operation requires IIS integrated pipeline mode"
                    Log.Warning(ex, "Unable to access response headers when creating header tags. Disabling for the rest of the application lifetime.");
                    _canReadHttpResponseHeaders = false;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting HTTP headers to create header tags.");
                }
            }

            return new NameValueHeadersCollection(new NameValueCollection());
        }
    }
}
#endif
