namespace Datadog.Trace.DuckTyping.Tests.Properties.ValueType.ProxiesDefinitions
{
#pragma warning disable 649

    [DuckCopy]
    internal struct ObscureDuckTypeStruct
    {
        public int PublicStaticGetValueType;
        public int InternalStaticGetValueType;
        public int ProtectedStaticGetValueType;
        public int PrivateStaticGetValueType;

        public int PublicStaticGetSetValueType;
        public int InternalStaticGetSetValueType;
        public int ProtectedStaticGetSetValueType;
        public int PrivateStaticGetSetValueType;

        public int PublicGetValueType;
        public int InternalGetValueType;
        public int ProtectedGetValueType;
        public int PrivateGetValueType;

        public int PublicGetSetValueType;
        public int InternalGetSetValueType;
        public int ProtectedGetSetValueType;
        public int PrivateGetSetValueType;
    }
}
