// <copyright file="SecurityCoordinator.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Web;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AspNet;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Coordinator;

internal readonly partial struct SecurityCoordinator
{
    private const string WebApiControllerHandlerTypeFullname = "System.Web.Http.WebHost.HttpControllerHandler";

    private static readonly bool? UsingIntegratedPipeline;
    private static readonly Lazy<Action<HttpStatusCode, string, string>?> _throwHttpResponseException = new(CreateThrowHttpResponseExceptionDynMeth);
    private static readonly Lazy<Action<HttpStatusCode, string>?> _throwHttpResponseRedirectException = new(CreateThrowHttpResponseExceptionDynMethForRedirect);

    private readonly HttpContext _context;

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

    private static Action<HttpStatusCode, string, string>? CreateThrowHttpResponseExceptionDynMeth()
    {
        try
        {
            var dynMethod = new DynamicMethod(
                "ThrowHttpResponseExceptionDynMeth",
                typeof(void),
                new[] { typeof(HttpStatusCode), typeof(string), typeof(string) },
                typeof(SecurityCoordinator).Module,
                true);
            var il = GetBaseIlForThrowingHttpResponseException(dynMethod);
            if (il == null)
            {
                return null;
            }

            var messageType = Type.GetType("System.Net.Http.HttpResponseMessage, System.Net.Http, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            if (messageType == null)
            {
                return null;
            }

            var contentType = Type.GetType("System.Net.Http.StringContent, System.Net.Http, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            if (contentType == null)
            {
                return null;
            }

            var messageContentProperty = messageType.GetProperty("Content");
            // StringContent(String content, Encoding, String mediaType)
            var contentCtor = contentType.GetConstructor(new[] { typeof(string), typeof(Encoding), typeof(string) });

            // body's content
            il.Emit(OpCodes.Ldarg_1);
            var encodingType = typeof(Encoding);
            var encodingUtf8Prop = encodingType.GetProperty("UTF8");
            il.EmitCall(OpCodes.Call, encodingUtf8Prop.GetMethod, null);
            // media type
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Newobj, contentCtor);
            il.EmitCall(OpCodes.Callvirt, messageContentProperty.SetMethod, null);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Throw);
            return (Action<HttpStatusCode, string, string>)dynMethod.CreateDelegate(typeof(Action<HttpStatusCode, string, string>));
        }
        catch (Exception e)
        {
            Log.Warning("An error occured while trying to write the IL for generating an HttpResponseException with message {Message}", e.Message);
            return null;
        }
    }

    private static Action<HttpStatusCode, string>? CreateThrowHttpResponseExceptionDynMethForRedirect()
    {
        try
        {
            var dynMethod = new DynamicMethod(
                "ThrowHttpResponseRedirectExceptionDynMeth",
                typeof(void),
                new[] { typeof(HttpStatusCode), typeof(string), typeof(string) },
                typeof(SecurityCoordinator).Module,
                true);
            var il = GetBaseIlForThrowingHttpResponseException(dynMethod);
            if (il == null)
            {
                return null;
            }

            var messageType = Type.GetType("System.Net.Http.HttpResponseMessage, System.Net.Http, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            if (messageType == null)
            {
                return null;
            }

            var headerProperty = messageType.GetProperty("Headers");
            if (headerProperty == null)
            {
                return null;
            }

            var httpResponseHeadersType = Type.GetType("System.Net.Http.Headers.HttpResponseHeaders, System.Net.Http, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            if (httpResponseHeadersType == null)
            {
                return null;
            }

            var tryAddWithoutValidationMethod = httpResponseHeadersType.GetMethod("TryAddWithoutValidation", new[] { typeof(string), typeof(string) });
            if (tryAddWithoutValidationMethod == null)
            {
                return null;
            }

            il.EmitCall(OpCodes.Callvirt, headerProperty.GetMethod, null);
            // location
            il.Emit(OpCodes.Ldstr, "Location");
            il.Emit(OpCodes.Ldarg_1);

            il.EmitCall(OpCodes.Callvirt, tryAddWithoutValidationMethod, null);

            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Throw);

            return (Action<HttpStatusCode, string>)dynMethod.CreateDelegate(typeof(Action<HttpStatusCode, string>));
        }
        catch (Exception e)
        {
            Log.Warning("An error occured while trying to write the IL for generating an HttpResponseException with message {Message}", e.Message);
            return null;
        }
    }

    private static ILGenerator? GetBaseIlForThrowingHttpResponseException(DynamicMethod method)
    {
        var exceptionType = Type.GetType("System.Web.Http.HttpResponseException, System.Web.Http");
        if (exceptionType == null)
        {
            return null;
        }

        var exceptionCtor = exceptionType.GetConstructor(new[] { typeof(HttpStatusCode) });
        var exceptionResponseProperty = exceptionType.GetProperty("Response");

        var il = method.GetILGenerator();
        il.DeclareLocal(exceptionType);
        // status code loading
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Newobj, exceptionCtor);
        il.Emit(OpCodes.Stloc_0);
        il.Emit(OpCodes.Ldloc_0);
        il.EmitCall(OpCodes.Callvirt, exceptionResponseProperty.GetMethod, null);
        return il;
    }

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
    /// Framework can do it all at once, but framework only unfortunately
    /// </summary>
    internal void CheckAndBlock(Dictionary<string, object> args)
    {
        var result = RunWaf(args);
        if (result?.ShouldBeReported is true)
        {
            var reporting = MakeReportingFunction(result.Data, result.AggregatedTotalRuntime, result.AggregatedTotalRuntimeWithBindings);

            if (result.ShouldBlock)
            {
                ChooseBlockingMethodAndBlock(result.Actions[0], reporting);
            }

            // here we assume if the we haven't blocked we'll have collected the correct status elsewhere
            reporting(null, result.ShouldBlock);
        }
    }

    /// <summary>
    /// Run the WAF on addresses with arguments and return the result without blocking
    /// </summary>
    internal IResult? Check(Dictionary<string, object> args)
    {
        var result = RunWaf(args);
        if (result?.ShouldBeReported is true)
        {
            var reporting = MakeReportingFunction(result.Data, result.AggregatedTotalRuntime, result.AggregatedTotalRuntimeWithBindings);
            reporting(null, result.ShouldBlock);
        }

        return result;
    }

    private Action<int?, bool> MakeReportingFunction(string triggerData, ulong aggregatedTotalRuntime, ulong aggregatedTotalRuntimeWithBindings)
    {
        var securityCoordinator = this;
        return (status, blocked) =>
        {
            if (blocked)
            {
                securityCoordinator._httpTransport.MarkBlocked();
            }

            securityCoordinator.Report(triggerData, aggregatedTotalRuntime, aggregatedTotalRuntimeWithBindings, blocked, status);
        };
    }

    private void ChooseBlockingMethodAndBlock(string blockActionId, Action<int?, bool> reporting)
    {
        var blockingAction = _security.GetBlockingAction(blockActionId, new[] { _context.Request.Headers["Accept"] });
        var isWebApiRequest = _context.CurrentHandler?.GetType().FullName == WebApiControllerHandlerTypeFullname;
        if (isWebApiRequest)
        {
            if (!blockingAction.IsRedirect && _throwHttpResponseException.Value is { } throwException)
            {
                // in the normal case reporting will be by the caller function after we block
                // in the webapi case we block with an exception, so can't report afterwards
                reporting(blockingAction.StatusCode, true);
                throwException((HttpStatusCode)blockingAction.StatusCode, blockingAction.ResponseContent, blockingAction.ContentType);
            }
            else if (blockingAction.IsRedirect && _throwHttpResponseRedirectException.Value is { } throwRedirectException)
            {
                // in the normal case reporting will be by the caller function after we block
                // in the webapi case we block with an exception, so can't report afterwards
                reporting(blockingAction.StatusCode, true);
                throwRedirectException((HttpStatusCode)blockingAction.StatusCode, blockingAction.RedirectLocation);
            }
        }

        // we will only hit this next line if we didn't throw
        WriteAndEndResponse(blockingAction);
    }

    private void WriteAndEndResponse(BlockingAction blockingAction)
    {
        var httpResponse = _context.Response;
        httpResponse.Clear();
        httpResponse.Cookies.Clear();

        // cant clear headers, on some iis version we get a platform not supported exception
        if (CanAccessHeaders)
        {
            var keys = httpResponse.Headers.Keys.Cast<string>().ToList();
            foreach (var key in keys)
            {
                httpResponse.Headers.Remove(key);
            }
        }

        httpResponse.StatusCode = blockingAction.StatusCode;

        if (blockingAction.IsRedirect)
        {
            httpResponse.Redirect(blockingAction.RedirectLocation, blockingAction.IsPermanentRedirect);
        }
        else
        {
            httpResponse.ContentType = blockingAction.ContentType;
            httpResponse.Write(blockingAction.ResponseContent);
        }

        httpResponse.Flush();
        httpResponse.Close();
        _context.ApplicationInstance.CompleteRequest();
    }

    public Dictionary<string, object> GetBasicRequestArgsForWaf()
    {
        var request = _context.Request;
        var headersDic = new Dictionary<string, string[]>(request.Headers.Keys.Count);
        var headerKeys = request.Headers.Keys;
        foreach (string originalKey in headerKeys)
        {
            var keyForDictionary = originalKey?.ToLowerInvariant() ?? string.Empty;
            if (keyForDictionary != "cookie")
            {
                if (!headersDic.ContainsKey(keyForDictionary))
                {
                    headersDic.Add(keyForDictionary, request.Headers.GetValues(originalKey));
                }
                else
                {
                    Log.Warning("Header {Key} couldn't be added as argument to the waf", keyForDictionary);
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
                Log.Warning("Query string {Key} couldn't be added as argument to the waf", originalKey);
            }
        }

        var dict = new Dictionary<string, object>(capacity: 7)
        {
            { AddressesConstants.RequestMethod, request.HttpMethod },
            { AddressesConstants.RequestUriRaw, request.Url.PathAndQuery },
            { AddressesConstants.RequestQuery, queryDic },
            { AddressesConstants.ResponseStatus, request.RequestContext.HttpContext.Response.StatusCode.ToString() },
            { AddressesConstants.RequestHeaderNoCookies, headersDic },
            { AddressesConstants.RequestCookies, cookiesDic },
            { AddressesConstants.RequestClientIp, _localRootSpan.GetTag(Tags.HttpClientIp) }
        };
        if (_localRootSpan.Context.TraceContext.Tags.GetTag(Tags.User.Id) is { } userIdTag)
        {
            dict.Add(AddressesConstants.UserId, userIdTag);
        }

        return dict;
    }

    public Dictionary<string, string[]> GetResponseHeadersForWaf()
    {
        var response = _context.Response;
        var headersDic = new Dictionary<string, string[]>(response.Headers.Keys.Count);
        var headerKeys = response.Headers.Keys;
        foreach (string originalKey in headerKeys)
        {
            var keyForDictionary = originalKey ?? string.Empty;
            if (!keyForDictionary.Equals("cookie", System.StringComparison.OrdinalIgnoreCase))
            {
                keyForDictionary = keyForDictionary.ToLowerInvariant();
                if (!headersDic.ContainsKey(keyForDictionary))
                {
                    headersDic.Add(keyForDictionary, response.Headers.GetValues(originalKey));
                }
                else
                {
                    Log.Warning("Header {Key} couldn't be added as argument to the waf", keyForDictionary);
                }
            }
        }

        return headersDic;
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

        internal override IContext? GetAdditiveContext() => _context.Items[WafKey] as IContext;

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
