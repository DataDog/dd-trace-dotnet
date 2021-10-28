// <copyright file="ValidVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ReferenceType.ProxiesDefinitions.Valid
{
    public class ValidVirtualClass
    {
        public class PublicStaticGetReferenceTypeVirtualClass
        {
            public virtual string PublicStaticGetReferenceType { get; }
        }

        public class InternalStaticGetReferenceTypeVirtualClass
        {
            public virtual string InternalStaticGetReferenceType { get; }
        }

        public class ProtectedStaticGetReferenceTypeVirtualClass
        {
            public virtual string ProtectedStaticGetReferenceType { get; }
        }

        public class PrivateStaticGetReferenceTypeVirtualClass
        {
            public virtual string PrivateStaticGetReferenceType { get; }
        }

        // *

        public class PublicStaticGetSetReferenceTypeVirtualClass
        {
            public virtual string PublicStaticGetSetReferenceType { get; set; }
        }

        public class InternalStaticGetSetReferenceTypeVirtualClass
        {
            public virtual string InternalStaticGetSetReferenceType { get; set; }
        }

        public class ProtectedStaticGetSetReferenceTypeVirtualClass
        {
            public virtual string ProtectedStaticGetSetReferenceType { get; set; }
        }

        public class PrivateStaticGetSetReferenceTypeVirtualClass
        {
            public virtual string PrivateStaticGetSetReferenceType { get; set; }
        }

        // *

        public class PublicGetReferenceTypeVirtualClass
        {
            public virtual string PublicGetReferenceType { get; }
        }

        public class InternalGetReferenceTypeVirtualClass
        {
            public virtual string InternalGetReferenceType { get; }
        }

        public class ProtectedGetReferenceTypeVirtualClass
        {
            public virtual string ProtectedGetReferenceType { get; }
        }

        public class PrivateGetReferenceTypeVirtualClass
        {
            public virtual string PrivateGetReferenceType { get; }
        }

        // *

        public class PublicGetSetReferenceTypeVirtualClass
        {
            public virtual string PublicGetSetReferenceType { get; set; }
        }

        public class InternalGetSetReferenceTypeVirtualClass
        {
            public virtual string InternalGetSetReferenceType { get; set; }
        }

        public class ProtectedGetSetReferenceTypeVirtualClass
        {
            public virtual string ProtectedGetSetReferenceType { get; set; }
        }

        public class PrivateGetSetReferenceTypeVirtualClass
        {
            public virtual string PrivateGetSetReferenceType { get; set; }
        }

        // *

        public class IndexVirtualClass
        {
            public virtual string this[string index]
            {
                get => default;

                set { }
            }
        }
    }
}
