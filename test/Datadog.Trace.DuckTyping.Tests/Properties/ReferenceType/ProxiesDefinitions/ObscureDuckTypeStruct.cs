namespace Datadog.Trace.DuckTyping.Tests.Properties.ReferenceType.ProxiesDefinitions
{
#pragma warning disable 649

    [DuckCopy]
    internal struct ObscureDuckTypeStruct
    {
        public readonly string ReadonlyFieldIgnored;

        public string PublicStaticGetReferenceType;
        public string InternalStaticGetReferenceType;
        public string ProtectedStaticGetReferenceType;
        public string PrivateStaticGetReferenceType;

        public string PublicStaticGetSetReferenceType;
        public string InternalStaticGetSetReferenceType;
        public string ProtectedStaticGetSetReferenceType;
        public string PrivateStaticGetSetReferenceType;

        public string PublicGetReferenceType;
        public string InternalGetReferenceType;
        public string ProtectedGetReferenceType;
        public string PrivateGetReferenceType;

        public string PublicGetSetReferenceType;
        public string InternalGetSetReferenceType;
        public string ProtectedGetSetReferenceType;
        public string PrivateGetSetReferenceType;
    }
}
