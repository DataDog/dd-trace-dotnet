// <copyright file="HttpRequestExtensions.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System.Web;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet;

#nullable enable

namespace Datadog.Trace.Util.Http
{
    internal static partial class HttpRequestExtensions
    {
        internal static string GetUrlForSpan(this IHttpRequestMessage request, QueryStringManager queryStringManager)
            => HttpRequestUtils.GetUrl(request.RequestUri, queryStringManager);

        internal static string GetUrlForSpan(this HttpRequestBase request, QueryStringManager queryStringManager)
            => HttpRequestUtils.GetUrl(request.Url, queryStringManager);

        /// <summary>
        /// Gets the Url from the <paramref name="request"/>.
        /// <para>
        /// Note that this will <em>CACHE</em> the <c>Uri</c> of the <paramref name="request"/>
        /// for all future callers (example the customer's application) if we are the first to call <see cref="HttpRequest.Url"/>.
        /// </para>
        /// <para>
        /// To bypass this set <paramref name="bypassHttpRequestUrl"/> to <c>true</c> which is controlled by
        /// <see cref="Configuration.ConfigurationKeys.FeatureFlags.BypassHttpRequestUrlCachingEnabled"/>.
        /// </para>
        /// </summary>
        /// <param name="request">The <see cref="HttpRequest"/> to get the Uri of.</param>
        /// <param name="queryStringManager">The <see cref="QueryStringManager"/> for obfuscation/quantization.</param>
        /// <param name="bypassHttpRequestUrl">Whether or not to call access HttpRequest.Url (which caches the URL in HttpRequest).</param>
        /// <returns>The retrieved Url.</returns>
        /// <seealso cref="Configuration.ConfigurationKeys.FeatureFlags.BypassHttpRequestUrlCachingEnabled"/>
        internal static string GetUrlForSpan(this HttpRequest request, QueryStringManager queryStringManager, bool bypassHttpRequestUrl)
        {
            var url = bypassHttpRequestUrl ? RequestDataHelper.BuildUrl(request) : RequestDataHelper.GetUrl(request);

            if (url is not null)
            {
                return HttpRequestUtils.GetUrl(url, queryStringManager);
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
#endif
