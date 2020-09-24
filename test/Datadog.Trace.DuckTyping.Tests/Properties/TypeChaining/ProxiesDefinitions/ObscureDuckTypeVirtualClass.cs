namespace Datadog.Trace.DuckTyping.Tests.Properties.TypeChaining.ProxiesDefinitions
{
    public class ObscureDuckTypeVirtualClass
    {
        public virtual IDummyFieldObject PublicStaticGetSelfType { get; }

        public virtual IDummyFieldObject InternalStaticGetSelfType { get; }

        public virtual IDummyFieldObject ProtectedStaticGetSelfType { get; }

        public virtual IDummyFieldObject PrivateStaticGetSelfType { get; }

        // *

        public virtual IDummyFieldObject PublicStaticGetSetSelfType { get; set; }

        public virtual IDummyFieldObject InternalStaticGetSetSelfType { get; set; }

        public virtual IDummyFieldObject ProtectedStaticGetSetSelfType { get; set; }

        public virtual IDummyFieldObject PrivateStaticGetSetSelfType { get; set; }

        // *

        public virtual IDummyFieldObject PublicGetSelfType { get; }

        public virtual IDummyFieldObject InternalGetSelfType { get; }

        public virtual IDummyFieldObject ProtectedGetSelfType { get; }

        public virtual IDummyFieldObject PrivateGetSelfType { get; }

        // *

        public virtual IDummyFieldObject PublicGetSetSelfType { get; set; }

        public virtual IDummyFieldObject InternalGetSetSelfType { get; set; }

        public virtual IDummyFieldObject ProtectedGetSetSelfType { get; set; }

        public virtual IDummyFieldObject PrivateGetSetSelfType { get; set; }
    }
}
