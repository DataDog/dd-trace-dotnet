// <copyright file="HttpRequestExtensions.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETFRAMEWORK

using System.Collections.Generic;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util.Http
{
    internal static partial class HttpRequestExtensions
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HttpRequestExtensions));

        internal static Dictionary<string, object> PrepareArgsForWaf(this HttpRequest request)
        {
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

            var dict = new Dictionary<string, object>(capacity: 5)
            {
                {
                    AddressesConstants.RequestMethod, request.HttpMethod
                },
                {
                    AddressesConstants.RequestUriRaw, request.Url.AbsoluteUri
                },
                {
                    AddressesConstants.RequestQuery, queryDic
                },
                {
                    AddressesConstants.RequestHeaderNoCookies, headersDic
                },
                {
                    AddressesConstants.RequestCookies, cookiesDic
                }
            };

            return dict;
        }

        internal static string GetUrlWithQueryString(this IHttpRequestMessage request, string obfuscatorPattern)
        {
            var queryString = Obfuscate(request.RequestUri.Query, obfuscatorPattern);
            return HttpRequestUtils.GetUrl(request.RequestUri.Scheme, request.RequestUri.Host, string.Empty, request.RequestUri.AbsolutePath, queryString);
        }

        internal static string GetUrlWithQueryString(this HttpRequestBase request, string obfuscatorPattern)
        {
            var queryString = Obfuscate(request.Url.Query, obfuscatorPattern);
            return HttpRequestUtils.GetUrl(request.Url.Scheme, request.Url.Host, string.Empty, request.Url.AbsolutePath, queryString);
        }

        private static string Obfuscate(string queryString, string obfuscatorPattern)
        {
            var obfuscator = QueryStringObfuscator.Instance(obfuscatorPattern);
            return obfuscator.Obfuscate(queryString);
        }
    }
}
#endif
