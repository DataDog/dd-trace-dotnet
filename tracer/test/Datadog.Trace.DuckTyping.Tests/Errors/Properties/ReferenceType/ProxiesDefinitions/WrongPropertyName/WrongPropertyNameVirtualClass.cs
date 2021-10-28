// <copyright file="WrongPropertyNameVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ReferenceType.ProxiesDefinitions.WrongPropertyName
{
    public class WrongPropertyNameVirtualClass
    {
        public class PublicStaticGetReferenceTypeVirtualClass
        {
            public virtual string NotPublicStaticGetReferenceType { get; }
        }

        public class InternalStaticGetReferenceTypeVirtualClass
        {
            public virtual string NotInternalStaticGetReferenceType { get; }
        }

        public class ProtectedStaticGetReferenceTypeVirtualClass
        {
            public virtual string NotProtectedStaticGetReferenceType { get; }
        }

        public class PrivateStaticGetReferenceTypeVirtualClass
        {
            public virtual string NotPrivateStaticGetReferenceType { get; }
        }

        // *

        public class PublicStaticGetSetReferenceTypeVirtualClass
        {
            public virtual string NotPublicStaticGetSetReferenceType { get; set; }
        }

        public class InternalStaticGetSetReferenceTypeVirtualClass
        {
            public virtual string NotInternalStaticGetSetReferenceType { get; set; }
        }

        public class ProtectedStaticGetSetReferenceTypeVirtualClass
        {
            public virtual string NotProtectedStaticGetSetReferenceType { get; set; }
        }

        public class PrivateStaticGetSetReferenceTypeVirtualClass
        {
            public virtual string NotPrivateStaticGetSetReferenceType { get; set; }
        }

        // *

        public class PublicGetReferenceTypeVirtualClass
        {
            public virtual string NotPublicGetReferenceType { get; }
        }

        public class InternalGetReferenceTypeVirtualClass
        {
            public virtual string NotInternalGetReferenceType { get; }
        }

        public class ProtectedGetReferenceTypeVirtualClass
        {
            public virtual string NotProtectedGetReferenceType { get; }
        }

        public class PrivateGetReferenceTypeVirtualClass
        {
            public virtual string NotPrivateGetReferenceType { get; }
        }

        // *

        public class PublicGetSetReferenceTypeVirtualClass
        {
            public virtual string NotPublicGetSetReferenceType { get; set; }
        }

        public class InternalGetSetReferenceTypeVirtualClass
        {
            public virtual string NotInternalGetSetReferenceType { get; set; }
        }

        public class ProtectedGetSetReferenceTypeVirtualClass
        {
            public virtual string NotProtectedGetSetReferenceType { get; set; }
        }

        public class PrivateGetSetReferenceTypeVirtualClass
        {
            public virtual string NotPrivateGetSetReferenceType { get; set; }
        }
    }
}
