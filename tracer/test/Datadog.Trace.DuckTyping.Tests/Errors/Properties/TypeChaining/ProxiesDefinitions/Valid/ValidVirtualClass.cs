// <copyright file="ValidVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.Valid
{
    public class ValidVirtualClass
    {
        public class PublicStaticGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject PublicStaticGetSelfType { get; }
        }

        public class InternalStaticGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject InternalStaticGetSelfType { get; }
        }

        public class ProtectedStaticGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject ProtectedStaticGetSelfType { get; }
        }

        public class PrivateStaticGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject PrivateStaticGetSelfType { get; }
        }

        // *

        public class PublicStaticGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject PublicStaticGetSetSelfType { get; set; }
        }

        public class InternalStaticGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject InternalStaticGetSetSelfType { get; set; }
        }

        public class ProtectedStaticGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject ProtectedStaticGetSetSelfType { get; set; }
        }

        public class PrivateStaticGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject PrivateStaticGetSetSelfType { get; set; }
        }

        // *

        public class PublicGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject PublicGetSelfType { get; }
        }

        public class InternalGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject InternalGetSelfType { get; }
        }

        public class ProtectedGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject ProtectedGetSelfType { get; }
        }

        public class PrivateGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject PrivateGetSelfType { get; }
        }

        // *

        public class PublicGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject PublicGetSetSelfType { get; set; }
        }

        public class InternalGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject InternalGetSetSelfType { get; set; }
        }

        public class ProtectedGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject ProtectedGetSetSelfType { get; set; }
        }

        public class PrivateGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject PrivateGetSetSelfType { get; set; }
        }
    }
}
