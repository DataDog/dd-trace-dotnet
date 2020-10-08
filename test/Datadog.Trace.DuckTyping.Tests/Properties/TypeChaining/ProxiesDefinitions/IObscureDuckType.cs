using System;

namespace Datadog.Trace.DuckTyping.Tests.Properties.TypeChaining.ProxiesDefinitions
{
    public interface IObscureDuckType
    {
        IDummyFieldObject PublicStaticGetSelfType { get; }

        IDummyFieldObject InternalStaticGetSelfType { get; }

        IDummyFieldObject ProtectedStaticGetSelfType { get; }

        IDummyFieldObject PrivateStaticGetSelfType { get; }

        // *

        IDummyFieldObject PublicStaticGetSetSelfType { get; set; }

        IDummyFieldObject InternalStaticGetSetSelfType { get; set; }

        IDummyFieldObject ProtectedStaticGetSetSelfType { get; set; }

        IDummyFieldObject PrivateStaticGetSetSelfType { get; set; }

        // *

        IDummyFieldObject PublicGetSelfType { get; }

        IDummyFieldObject InternalGetSelfType { get; }

        IDummyFieldObject ProtectedGetSelfType { get; }

        IDummyFieldObject PrivateGetSelfType { get; }

        // *

        IDummyFieldObject PublicGetSetSelfType { get; set; }

        IDummyFieldObject InternalGetSetSelfType { get; set; }

        IDummyFieldObject ProtectedGetSetSelfType { get; set; }

        IDummyFieldObject PrivateGetSetSelfType { get; set; }

        // *

        IDummyFieldObject PrivateDummyGetSetSelfType { get; set; }
    }
}
