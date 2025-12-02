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
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Web;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.AppSec.Coordinator;

internal readonly partial struct SecurityCoordinator
{
    private const string WebApiControllerHandlerTypeFullname = "System.Web.Http.WebHost.HttpControllerHandler";
    private static readonly Lazy<Action<IResult, HttpStatusCode, string>?> _throwHttpResponseRedirectException = new(CreateThrowHttpResponseExceptionDynMethForRedirect);
    private static readonly Lazy<Action<IResult, HttpStatusCode, string, string>?> _throwHttpResponseException = new(CreateThrowHttpResponseExceptionDynMeth);

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
        if (HttpContext.Current is not { } current)
        {
            if (!_nullContextReported)
            {
                Log.Warning("Can't instantiate SecurityCoordinator.Framework as no transport has been provided and HttpContext.Current null, make sure HttpContext is available");
                _nullContextReported = true;
            }
            else
            {
                Log.Debug("Can't instantiate SecurityCoordinator.Framework as no transport has been provided and HttpContext.Current null, make sure HttpContext is available");
            }

            return null;
        }

        var transport = new HttpTransport(current);

        return new SecurityCoordinator(security, span, transport);
    }

    internal static SecurityCoordinator? TryGetSafe(Security security, Span span) => TryGet(security, span);

    internal static SecurityCoordinator Get(Security security, Span span, HttpContext context) => new(security, span, new HttpTransport(context));

    internal static SecurityCoordinator Get(Security security, Span span, HttpTransport transport) => new(security, span, transport);

    private static Action<IResult, HttpStatusCode, string, string>? CreateThrowHttpResponseExceptionDynMeth()
    {
        try
        {
            var dynMethod = new DynamicMethod(
                "ThrowHttpResponseExceptionDynMeth",
                typeof(void),
                [typeof(IResult), typeof(HttpStatusCode), typeof(string), typeof(string)],
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
            il.Emit(OpCodes.Ldarg_2);
            var encodingType = typeof(Encoding);
            var encodingUtf8Prop = encodingType.GetProperty("UTF8");
            il.EmitCall(OpCodes.Call, encodingUtf8Prop.GetMethod, null);
            // media type
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Newobj, contentCtor);
            il.EmitCall(OpCodes.Callvirt, messageContentProperty.SetMethod, null);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Throw);
            return (Action<IResult, HttpStatusCode, string, string>)dynMethod.CreateDelegate(typeof(Action<IResult, HttpStatusCode, string, string>));
        }
        catch (Exception e)
        {
            Log.Warning("An error occured while trying to write the IL for generating an HttpResponseException with message {Message}", e.Message);
            return null;
        }
    }

    private static Action<IResult, HttpStatusCode, string>? CreateThrowHttpResponseExceptionDynMethForRedirect()
    {
        try
        {
            var dynMethod = new DynamicMethod(
                "ThrowHttpResponseRedirectExceptionDynMeth",
                typeof(void),
                [typeof(IResult), typeof(HttpStatusCode), typeof(string)],
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
            il.Emit(OpCodes.Ldarg_2);

            il.EmitCall(OpCodes.Callvirt, tryAddWithoutValidationMethod, null);

            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Throw);

            return (Action<IResult, HttpStatusCode, string>)dynMethod.CreateDelegate(typeof(Action<IResult, HttpStatusCode, string>));
        }
        catch (Exception e)
        {
            Log.Warning("An error occured while trying to write the IL for generating an HttpResponseException with message {Message}", e.Message);
            return null;
        }
    }

    /// <summary>
    /// What this is doing is:
    /// var httpException = new HttpResponseException(statuscode of the delegate)
    /// httpException._innerException = new BlockException()
    /// httpException.Response loaded
    /// </summary>
    /// <param name="method">dynamic method</param>
    /// <returns>beginning of the il to be shared</returns>
    private static ILGenerator? GetBaseIlForThrowingHttpResponseException(DynamicMethod method)
    {
        // new HttpResponseException(statuscode)
        var exceptionType = Type.GetType("System.Web.Http.HttpResponseException, System.Web.Http");

        if (exceptionType == null)
        {
            return null;
        }

        var blockException = typeof(BlockException);
        var blockExceptionCtor = blockException.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, [typeof(IResult)], null);
        var exceptionCtor = exceptionType.GetConstructor([typeof(HttpStatusCode)]);
        var exceptionResponseProperty = exceptionType.GetProperty("Response");
        var setValueMethod = typeof(FieldInfo).GetMethod("SetValue", [typeof(object), typeof(object)]);
        var getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle");
        var getFieldMethod = typeof(Type).GetMethod("GetField", [typeof(string), typeof(BindingFlags)], null);
        var getBaseType = typeof(Type).GetProperty("BaseType");

        var il = method.GetILGenerator();
        il.DeclareLocal(exceptionType);
        // status code loading
        il.Emit(OpCodes.Ldarg_1);
        // new HttpResponseException(statuscode)
        il.Emit(OpCodes.Newobj, exceptionCtor);
        il.Emit(OpCodes.Stloc_0);
        il.Emit(OpCodes.Ldloc_0);
        // typeof(HttpResponseException)
        il.Emit(OpCodes.Ldtoken, exceptionType);
        il.EmitCall(OpCodes.Call, getTypeFromHandle, null);
        // .BaseType
        il.EmitCall(OpCodes.Callvirt, getBaseType.GetMethod, null);

        // .GetField("_innerException", BindingFlags.Instance | BindingFlags.NonPublic)
        il.Emit(OpCodes.Ldstr, "_innerException");
        il.Emit(OpCodes.Ldc_I4_S, (int)(BindingFlags.Instance | BindingFlags.NonPublic));
        il.EmitCall(OpCodes.Callvirt, getFieldMethod, null);
        // loads httpexception
        il.Emit(OpCodes.Ldloc_0);
        // loads IResult
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Newobj, blockExceptionCtor); // loads blockexception
        il.EmitCall(OpCodes.Callvirt, setValueMethod, null);

        il.Emit(OpCodes.Ldloc_0);
        // httpResponseException.Response
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

    internal Dictionary<string, object>? GetBodyFromRequest()
    {
        // When accessing the property Form of a HttpRequest might throw a ValidationException if regular validation is enabled.
        // Granular validation would not throw an exception when accessing Form but it will throw it when accessing GetValues or Get.
        // The keys property never throws any exception.
        var form = RequestDataHelper.GetForm(_httpTransport.Context.Request);

        if (form is null)
        {
            return null;
        }

        var formData = new Dictionary<string, object>(form.Keys.Count);
        foreach (string key in form.Keys)
        {
            // key could be null, but it's not a valid key in a dictionary
            // Using [] instead of Add to avoid potential duplicate key
            // but it does mean there's a (tiny) chance of overwriting the key
            var values = RequestDataHelper.GetNameValueCollectionValue(form, key);

            if (values is not null)
            {
                formData[key ?? string.Empty] = values;
            }
        }

        return formData;
    }

    internal object? GetPathParams() => ObjectExtractor.Extract(_httpTransport.Context.Request.RequestContext.RouteData.Values);

    /// <summary>
    /// Framework can do it all at once, but framework only unfortunately
    /// </summary>
    internal void BlockAndReport(Dictionary<string, object> args, bool lastWafCall = false, bool isInHttpTracingModule = false)
    {
        var sessionId = _httpTransport.Context.Session?.SessionID;
        var result = RunWaf(args, lastWafCall, sessionId: sessionId);
        if (result is not null)
        {
            var reporting = Reporter.MakeReportingFunction(result);

            if (result.ShouldBlock)
            {
                ChooseBlockingMethodAndBlock(result, reporting, result.BlockInfo, result.RedirectInfo, isInHttpTracingModule);
            }

            // here we assume if we haven't blocked we'll have collected the correct status elsewhere
            reporting(null, result.ShouldBlock);
        }
    }

    internal void BlockAndReport(IResult? result)
    {
        if (result is null)
        {
            return;
        }

        var reporting = Reporter.MakeReportingFunction(result);

        if (result.ShouldBlock)
        {
            ChooseBlockingMethodAndBlock(result, reporting, result.BlockInfo, result.RedirectInfo);
        }

        // here we assume if we haven't blocked we'll have collected the correct http status elsewhere
        reporting(null, result.ShouldBlock);
    }

    internal void ReportAndBlock(IResult? result, Action telemetrySucessReport)
    {
        if (result is not null)
        {
            var reporting = Reporter.MakeReportingFunction(result);
            reporting(null, result.ShouldBlock);
            telemetrySucessReport.Invoke();

            if (result.ShouldBlock)
            {
                ChooseBlockingMethodAndBlock(result, reporting, result.BlockInfo, result.RedirectInfo);

                // chooseBlockingMethodAndBlock doesn't throw for all non webapi contexts and just ends the request flow.
                // For webapi we need to throw a HttpWebResponseException to block the flow. In the context of rasp instrumentations,
                // we need to throw in any case to not only block the request but any code execution after the instrumentation points

                if (result.BlockInfo is not null)
                {
                    throw new BlockException(result, result.RedirectInfo ?? result.BlockInfo);
                }
            }
        }
    }

    private void ChooseBlockingMethodAndBlock(IResult result, Action<int?, bool> reporting, Dictionary<string, object?>? blockInfo, Dictionary<string, object?>? redirectInfo, bool inTracingHttpModule = false)
    {
        var headers = RequestDataHelper.GetHeaders(_httpTransport.Context.Request) ?? new NameValueCollection();
        var blockingAction = _security.GetBlockingAction([headers["Accept"]], blockInfo, redirectInfo);
        var isWebApiRequest = _httpTransport.Context.CurrentHandler?.GetType().FullName == WebApiControllerHandlerTypeFullname;
        if (isWebApiRequest && !inTracingHttpModule)
        {
            if (!blockingAction.IsRedirect && _throwHttpResponseException.Value is { } throwException)
            {
                // in the normal case reporting will be by the caller function after we block
                // in the webapi case we block with an exception, so can't report afterwards
                reporting(blockingAction.StatusCode, true);
                throwException(result, (HttpStatusCode)blockingAction.StatusCode, blockingAction.ResponseContent, blockingAction.ContentType);
            }
            else if (blockingAction.IsRedirect && _throwHttpResponseRedirectException.Value is { } throwRedirectException)
            {
                // in the normal case reporting will be by the caller function after we block
                // in the webapi case we block with an exception, so can't report afterwards
                reporting(blockingAction.StatusCode, true);
                throwRedirectException(result, (HttpStatusCode)blockingAction.StatusCode, blockingAction.RedirectLocation);
            }
        }

        // we will only hit this next line if we didn't throw
        WriteAndEndResponse(blockingAction);
    }

    private void WriteAndEndResponse(BlockingAction blockingAction)
    {
        var httpResponse = _httpTransport.Context.Response;

        if (httpResponse.HeadersWritten)
        {
            Log.Warning("Headers have already been written, unable to modify response and set status code to {0}", blockingAction.StatusCode.ToString());
            return;
        }

        httpResponse.Clear();
        httpResponse.Cookies.Clear();

        // cant clear headers, on some iis version we get a platform not supported exception
        if (Reporter.CanAccessHeaders)
        {
            var keys = httpResponse.Headers.Keys.Cast<string>().ToList();
            foreach (var key in keys)
            {
                httpResponse.Headers.Remove(key);
            }
        }

        if (blockingAction.IsRedirect)
        {
            httpResponse.Redirect(blockingAction.RedirectLocation, false);

            // Redirect() set a status code (301 or 302) and ends the response,
            // but we want to set the status code to the one we have in the blocking action
            httpResponse.StatusCode = blockingAction.StatusCode;
        }
        else
        {
            httpResponse.StatusCode = blockingAction.StatusCode;
            httpResponse.ContentType = blockingAction.ContentType;
            httpResponse.Write(blockingAction.ResponseContent);
        }

        httpResponse.Flush();
        httpResponse.Close();
        _httpTransport.Context.ApplicationInstance.CompleteRequest();
    }

    public Dictionary<string, object> GetBasicRequestArgsForWaf()
    {
        var request = _httpTransport.Context.Request;
        var headers = request.Headers;
        var headersDic = ExtractHeaders(headers.AllKeys, key => GetHeaderValueForWaf(headers, key));
        var cookiesDic = ExtractCookiesFromRequest(request);

        var queryString = RequestDataHelper.GetQueryString(request);
        Dictionary<string, string[]>? queryDic = null;

        if (queryString is not null)
        {
            queryDic = new Dictionary<string, string[]>(queryString.AllKeys.Length);
            foreach (var originalKey in queryString.AllKeys)
            {
                var values = RequestDataHelper.GetNameValueCollectionValues(queryString, originalKey);
                if (values is not null)
                {
                    if (string.IsNullOrEmpty(originalKey))
                    {
                        foreach (var value in values)
                        {
                            if (!queryDic.ContainsKey(value))
                            {
                                queryDic.Add(value, []);
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
            }
        }

        var dict = new Dictionary<string, object>(capacity: 7)
        {
            { AddressesConstants.RequestMethod, request.HttpMethod },
            { AddressesConstants.ResponseStatus, request.RequestContext.HttpContext.Response.StatusCode.ToString() },
            { AddressesConstants.RequestClientIp, _localRootSpan.GetTag(Tags.HttpClientIp) ?? _localRootSpan.GetTag(Tags.NetworkClientIp) }
        };

        var url = RequestDataHelper.GetUrl(request);
        if (url is not null)
        {
            dict[AddressesConstants.RequestUriRaw] = url.PathAndQuery;
        }

        if (headersDic is not null)
        {
            dict[AddressesConstants.RequestHeaderNoCookies] = headersDic;
        }

        if (cookiesDic is not null)
        {
            dict[AddressesConstants.RequestCookies] = cookiesDic;
        }

        if (queryDic is not null)
        {
            dict[AddressesConstants.RequestQuery] = queryDic;
        }

        if (_localRootSpan.Context.TraceContext.Tags.GetTag(Tags.User.Id) is { } userIdTag)
        {
            dict.Add(AddressesConstants.UserId, userIdTag);
        }

        return dict;
    }

    private static object GetHeaderAsArray(string[] value) => value.Length == 1 ? value[0] : value;

    private static object GetHeaderValueForWaf(NameValueCollection headers, string currentKey) => GetHeaderAsArray(RequestDataHelper.GetNameValueCollectionValues(headers, currentKey) ?? []);

    private static void GetCookieKeyValueFromIndex(HttpCookieCollection cookies, int i, out string key, out string value)
    {
        var cookie = cookies[i];
        key = cookie.Name;
        value = cookie.Value;
    }

    public Dictionary<string, object> GetResponseHeadersForWaf()
    {
        var response = _httpTransport.Context.Response;
        var headersDic = new Dictionary<string, object>(response.Headers.Keys.Count);
        var headerKeys = response.Headers.Keys;
        foreach (string originalKey in headerKeys)
        {
            var keyForDictionary = originalKey ?? string.Empty;
            if (!keyForDictionary.Equals("cookie", StringComparison.OrdinalIgnoreCase))
            {
                keyForDictionary = keyForDictionary.ToLowerInvariant();
                if (!headersDic.ContainsKey(keyForDictionary))
                {
                    headersDic.Add(keyForDictionary, GetHeaderAsArray(response.Headers.GetValues(originalKey)));
                }
                else
                {
                    Log.Warning("Header {Key} couldn't be added as argument to the waf", keyForDictionary);
                }
            }
        }

        return headersDic;
    }

    internal sealed class HttpTransport(HttpContext context) : HttpTransportBase
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<HttpTransport>();

        private static bool _canReadHttpResponseHeaders = true;

        internal override bool IsBlocked => Context.Items[BlockingAction.BlockDefaultActionName] is true;

        internal override int? StatusCode => Context.Response.StatusCode;

        public override HttpContext Context { get; } = context;

        internal override IDictionary<string, object>? RouteData => Context.Request.RequestContext.RouteData?.Values;

        internal override bool ReportedExternalWafsRequestHeaders
        {
            get => Context.Items[ReportedExternalWafsRequestHeadersStr] is true;
            set => Context.Items[ReportedExternalWafsRequestHeadersStr] = value;
        }

        internal override void MarkBlocked() => Context.Items[BlockingAction.BlockDefaultActionName] = true;

        internal override IHeadersCollection GetRequestHeaders() => new NameValueHeadersCollection(Context.Request.Headers);

        internal override IHeadersCollection GetResponseHeaders()
        {
            if (_canReadHttpResponseHeaders)
            {
                try
                {
                    return new NameValueHeadersCollection(Context.Response.Headers);
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
