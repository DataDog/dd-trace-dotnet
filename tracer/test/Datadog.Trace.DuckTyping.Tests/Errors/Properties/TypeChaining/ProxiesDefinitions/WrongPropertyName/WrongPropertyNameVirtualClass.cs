// <copyright file="WrongPropertyNameVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.WrongPropertyName
{
    public class WrongPropertyNameVirtualClass
    {
        public class PublicStaticGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotPublicStaticGetSelfType { get; }
        }

        public class InternalStaticGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotInternalStaticGetSelfType { get; }
        }

        public class ProtectedStaticGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotProtectedStaticGetSelfType { get; }
        }

        public class PrivateStaticGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotPrivateStaticGetSelfType { get; }
        }

        // *

        public class PublicStaticGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotPublicStaticGetSetSelfType { get; set; }
        }

        public class InternalStaticGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotInternalStaticGetSetSelfType { get; set; }
        }

        public class ProtectedStaticGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotProtectedStaticGetSetSelfType { get; set; }
        }

        public class PrivateStaticGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotPrivateStaticGetSetSelfType { get; set; }
        }

        // *

        public class PublicGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotPublicGetSelfType { get; }
        }

        public class InternalGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotInternalGetSelfType { get; }
        }

        public class ProtectedGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotProtectedGetSelfType { get; }
        }

        public class PrivateGetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotPrivateGetSelfType { get; }
        }

        // *

        public class PublicGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotPublicGetSetSelfType { get; set; }
        }

        public class InternalGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotInternalGetSetSelfType { get; set; }
        }

        public class ProtectedGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotProtectedGetSetSelfType { get; set; }
        }

        public class PrivateGetSetSelfTypeVirtualClass
        {
            public virtual IDummyFieldObject NotPrivateGetSetSelfType { get; set; }
        }
    }
}
