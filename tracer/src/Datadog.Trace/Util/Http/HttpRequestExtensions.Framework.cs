// <copyright file="HttpRequestExtensions.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System.Web;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet;

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
        /// To avoid this, use <see cref="BuildUrlForSpan(HttpRequest, QueryStringManager)"/> instead.
        /// </para>
        /// </summary>
        /// <param name="request">The <see cref="HttpRequest"/> to get the Uri of.</param>
        /// <param name="queryStringManager">The <see cref="QueryStringManager"/> for obfuscation/quantization.</param>
        /// <returns>The retrieved Url.</returns>
        internal static string GetUrlForSpan(this HttpRequest request, QueryStringManager queryStringManager)
            => HttpRequestUtils.GetUrl(RequestDataHelper.GetUrl(request), queryStringManager);

        /// <summary>
        /// Builds the Url from the <paramref name="request"/>.
        /// <para>
        /// Note that this will <em>bypass</em> the caching behavior of the <see cref="HttpRequest.Url"/> property.
        /// </para>
        /// </summary>
        /// <param name="request">The <see cref="HttpRequest"/> to build the <c>Uri</c> from.</param>
        /// <param name="queryStringManager">The <see cref="QueryStringManager"/> for obfuscation/quantization.</param>
        /// <returns>The built Url.</returns>
        /// <remarks>While not <em>required</em> to be set this is controlled by <see cref="Configuration.ConfigurationKeys.FeatureFlags.BypassHttpRequestUrlCachingEnabled"/>.</remarks>
        internal static string BuildUrlForSpan(this HttpRequest request, QueryStringManager queryStringManager)
            => HttpRequestUtils.GetUrl(RequestDataHelper.BuildUrl(request), queryStringManager);
    }
}
#endif
