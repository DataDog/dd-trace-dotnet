namespace Datadog.Trace.DuckTyping.Tests.Properties.TypeChaining.ProxiesDefinitions
{
#pragma warning disable 649

    [DuckCopy]
    public struct ObscureDuckTypeStruct
    {
        public DummyFieldStruct PublicStaticGetSelfType;
        public DummyFieldStruct InternalStaticGetSelfType;
        public DummyFieldStruct ProtectedStaticGetSelfType;
        public DummyFieldStruct PrivateStaticGetSelfType;

        public DummyFieldStruct PublicStaticGetSetSelfType;
        public DummyFieldStruct InternalStaticGetSetSelfType;
        public DummyFieldStruct ProtectedStaticGetSetSelfType;
        public DummyFieldStruct PrivateStaticGetSetSelfType;

        public DummyFieldStruct PublicGetSelfType;
        public DummyFieldStruct InternalGetSelfType;
        public DummyFieldStruct ProtectedGetSelfType;
        public DummyFieldStruct PrivateGetSelfType;

        public DummyFieldStruct PublicGetSetSelfType;
        public DummyFieldStruct InternalGetSetSelfType;
        public DummyFieldStruct ProtectedGetSetSelfType;
        public DummyFieldStruct PrivateGetSetSelfType;
    }
}
