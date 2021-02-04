using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ServiceFabric
{
    internal class RemotingTags : InstrumentationTags
    {
        protected static readonly IProperty<string>[] RemotingTagProperties =
            InstrumentationTagsProperties.Concat(
                new Property<RemotingTags, string?>(TagNames.Uri, t => t.Uri, (t, v) => t.Uri = v),
                new Property<RemotingTags, string?>(TagNames.MethodName, t => t.MethodName, (t, v) => t.MethodName = v),
                new Property<RemotingTags, string?>(TagNames.MethodId, t => t.MethodId, (t, v) => t.MethodId = v),
                new Property<RemotingTags, string?>(TagNames.InterfaceId, t => t.InterfaceId, (t, v) => t.InterfaceId = v),
                new Property<RemotingTags, string?>(TagNames.InvocationId, t => t.InvocationId, (t, v) => t.InvocationId = v));

        public RemotingTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        public override string? SpanKind { get; }

        public string? Uri { get; set; }

        public string? MethodName { get; set; }

        public string? MethodId { get; set; }

        public string? InterfaceId { get; set; }

        public string? InvocationId { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => RemotingTagProperties;

        public static class TagNames
        {
            public const string Uri = "service-remoting.uri";
            public const string MethodName = "service-remoting.method-name";
            public const string MethodId = "service-remoting.method-id";
            public const string InterfaceId = "service-remoting.interface-id";
            public const string InvocationId = "service-remoting.invocation-id";
        }
    }
}
