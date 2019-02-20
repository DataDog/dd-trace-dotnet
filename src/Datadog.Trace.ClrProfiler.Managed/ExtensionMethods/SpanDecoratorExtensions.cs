using System.Data;
using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.ClrProfiler.Services;
using Datadog.Trace.Enums;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ClrProfiler.ExtensionMethods
{
    internal static class SpanDecoratorExtensions
    {
        private static readonly ISpanDecorator _webTypeDecorator = new ActionBlockSpanDecorator(span => span.Type = SpanTypes.Web);
        private static readonly ISpanDecorator _sqlTypeDecorator = new ActionBlockSpanDecorator(span => span.Type = SpanTypes.Sql);

        public static ISpanDecorator HttpMethodDecorator(this IHasHttpMethod methodSource)
            => new ActionBlockSpanDecorator(span => span.Tag(Tags.HttpMethod, methodSource.GetHttpMethod()?.ToUpperInvariant() ?? "GET"));

        public static ISpanDecorator HttpHostHeaderDecorator(this IHasHttpHeaders headerSource)
            => new ActionBlockSpanDecorator(span => span.Tag(Tags.HttpRequestHeadersHost, headerSource.GetHeaderValue("Host")));

        public static ISpanDecorator HttpUrlDecorator(this IHasHttpUrl urlSource)
            => new ActionBlockSpanDecorator(span => span.Tag(Tags.HttpUrl, urlSource.GetRawUrl()?.ToLowerInvariant() ?? string.Empty));

        public static ISpanDecorator WebTypeDecorator() => _webTypeDecorator;

        public static ISpanDecorator AllWebSpanDecorator(this IHttpSpanDecoratable source)
            => new CompositeSpanDecorator(
                                          source.HttpMethodDecorator(),
                                          source.HttpHostHeaderDecorator(),
                                          source.HttpUrlDecorator(),
                                          WebTypeDecorator());

        public static ISpanDecorator ResourceNameDecorator<T>(this T source)
            where T : IHasResourceNameSuffixResolver, IHasHttpMethod
            => ResourceNameDecorator(source, source);

        public static ISpanDecorator ResourceNameDecorator(this IHasResourceNameSuffixResolver source, IHasHttpMethod methodSource)
            => new ResourceNameMethodSuffixDecorator(source, methodSource);

        public static ISpanDecorator SqlTypeDecorator() => _sqlTypeDecorator;

        public static ISpanDecorator DbConnectionStringDecorator(this IDbCommand source)
            => new DbConnectionStringDecorator(() => source?.Connection?.ConnectionString);

        public static ISpanDecorator AllDbCommandSpanDecorator(this IDbCommand source)
            => new CompositeSpanDecorator(
                                          source.DbConnectionStringDecorator(),
                                          new ActionBlockSpanDecorator(span => span.ResourceName = source.CommandText),
                                          SqlTypeDecorator());

        public static void DecorateWith(this ISpan span, ISpanDecorator decorator)
            => decorator.Decorate(span);
    }
}
