// <copyright file="WrongFieldNameVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ReferenceType.ProxiesDefinitions.WrongFieldName
{
    public class WrongFieldNameVirtualClass
    {
        public class PublicStaticReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "publicStaticReadonlyReferenceTypeField")]
            public virtual string PublicStaticReadonlyReferenceTypeField { get; }
        }

        public class InternalStaticReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "internalStaticReadonlyReferenceTypeField")]
            public virtual string InternalStaticReadonlyReferenceTypeField { get; }
        }

        public class ProtectedStaticReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "protectedStaticReadonlyReferenceTypeField")]
            public virtual string ProtectedStaticReadonlyReferenceTypeField { get; }
        }

        public class PrivateStaticReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "privateStaticReadonlyReferenceTypeField")]
            public virtual string PrivateStaticReadonlyReferenceTypeField { get; }
        }

        // *

        public class PublicStaticReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "publicStaticReferenceTypeField")]
            public virtual string PublicStaticReferenceTypeField { get; set; }
        }

        public class InternalStaticReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "internalStaticReferenceTypeField")]
            public virtual string InternalStaticReferenceTypeField { get; set; }
        }

        public class ProtectedStaticReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "protectedStaticReferenceTypeField")]
            public virtual string ProtectedStaticReferenceTypeField { get; set; }
        }

        public class PrivateStaticReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "privateStaticReferenceTypeField")]
            public virtual string PrivateStaticReferenceTypeField { get; set; }
        }

        // *

        public class PublicReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "publicReadonlyReferenceTypeField")]
            public virtual string PublicReadonlyReferenceTypeField { get; }
        }

        public class InternalReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "internalReadonlyReferenceTypeField")]
            public virtual string InternalReadonlyReferenceTypeField { get; }
        }

        public class ProtectedReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "protectedReadonlyReferenceTypeField")]
            public virtual string ProtectedReadonlyReferenceTypeField { get; }
        }

        public class PrivateReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "privateReadonlyReferenceTypeField")]
            public virtual string PrivateReadonlyReferenceTypeField { get; }
        }

        // *

        public class PublicReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "publicReferenceTypeField")]
            public virtual string PublicReferenceTypeField { get; set; }
        }

        public class InternalReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "internalReferenceTypeField")]
            public virtual string InternalReferenceTypeField { get; set; }
        }

        public class ProtectedReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "protectedReferenceTypeField")]
            public virtual string ProtectedReferenceTypeField { get; set; }
        }

        public class PrivateReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "privateReferenceTypeField")]
            public virtual string PrivateReferenceTypeField { get; set; }
        }
    }
}
