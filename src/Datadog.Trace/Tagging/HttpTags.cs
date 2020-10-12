using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class HttpTags : CommonTags
    {
        internal static readonly IProperty<string>[] HttpTagsProperties =
            CommonTagsProperties.Concat(
                new Property<HttpTags, string>(Trace.Tags.HttpStatusCode, t => t.HttpStatusCode, (t, v) => t.HttpStatusCode = v),
                new Property<HttpTags, string>(HttpClientHandlerTypeKey, t => t.HttpClientHandlerType, (t, v) => t.HttpClientHandlerType = v),
                new Property<HttpTags, string>(Trace.Tags.SpanKind, t => t.SpanKind, (t, v) => t.SpanKind = v),
                new Property<HttpTags, string>(Trace.Tags.HttpMethod, t => t.HttpMethod, (t, v) => t.HttpMethod = v),
                new Property<HttpTags, string>(Trace.Tags.HttpUrl, t => t.HttpUrl, (t, v) => t.HttpUrl = v),
                new Property<HttpTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName, (t, v) => t.InstrumentationName = v));

        internal static readonly IProperty<double?>[] HttpMetricsProperties =
            CommonMetricsProperties.Concat(
                new Property<HttpTags, double?>(Trace.Tags.Analytics, t => t.AnalyticsSampleRate, (t, v) => t.AnalyticsSampleRate = v));

        private const string HttpClientHandlerTypeKey = "http-client-handler-type";

        public string SpanKind { get; set; }

        public string HttpMethod { get; set; }

        public string HttpUrl { get; set; }

        public string InstrumentationName { get; set; }

        public string HttpClientHandlerType { get; set; }

        public string HttpStatusCode { get; set; }

        public double? AnalyticsSampleRate { get; set; }

        public static string ConvertStatusCodeToString(int statusCode)
        {
            if (statusCode == 200)
            {
                return "200";
            }

            if (statusCode == 302)
            {
                return "302";
            }

            if (statusCode == 401)
            {
                return "401";
            }

            if (statusCode == 403)
            {
                return "403";
            }

            if (statusCode == 404)
            {
                return "404";
            }

            if (statusCode == 500)
            {
                return "500";
            }

            if (statusCode == 503)
            {
                return "503";
            }

            return statusCode.ToString();
        }

        protected override IProperty<string>[] GetAdditionalTags() => HttpTagsProperties;

        protected override IProperty<double?>[] GetAdditionalMetrics() => HttpMetricsProperties;
    }
}
