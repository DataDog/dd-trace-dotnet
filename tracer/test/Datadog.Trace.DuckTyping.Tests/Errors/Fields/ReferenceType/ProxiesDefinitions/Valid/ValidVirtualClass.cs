// <copyright file="ValidVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ReferenceType.ProxiesDefinitions.Valid
{
    public class ValidVirtualClass
    {
        public class PublicStaticReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticReadonlyReferenceTypeField")]
            public virtual string PublicStaticReadonlyReferenceTypeField { get; }
        }

        public class InternalStaticReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalStaticReadonlyReferenceTypeField")]
            public virtual string InternalStaticReadonlyReferenceTypeField { get; }
        }

        public class ProtectedStaticReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedStaticReadonlyReferenceTypeField")]
            public virtual string ProtectedStaticReadonlyReferenceTypeField { get; }
        }

        public class PrivateStaticReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticReadonlyReferenceTypeField")]
            public virtual string PrivateStaticReadonlyReferenceTypeField { get; }
        }

        // *

        public class PublicStaticReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticReferenceTypeField")]
            public virtual string PublicStaticReferenceTypeField { get; set; }
        }

        public class InternalStaticReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalStaticReferenceTypeField")]
            public virtual string InternalStaticReferenceTypeField { get; set; }
        }

        public class ProtectedStaticReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedStaticReferenceTypeField")]
            public virtual string ProtectedStaticReferenceTypeField { get; set; }
        }

        public class PrivateStaticReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticReferenceTypeField")]
            public virtual string PrivateStaticReferenceTypeField { get; set; }
        }

        // *

        public class PublicReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicReadonlyReferenceTypeField")]
            public virtual string PublicReadonlyReferenceTypeField { get; }
        }

        public class InternalReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalReadonlyReferenceTypeField")]
            public virtual string InternalReadonlyReferenceTypeField { get; }
        }

        public class ProtectedReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedReadonlyReferenceTypeField")]
            public virtual string ProtectedReadonlyReferenceTypeField { get; }
        }

        public class PrivateReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateReadonlyReferenceTypeField")]
            public virtual string PrivateReadonlyReferenceTypeField { get; }
        }

        // *

        public class PublicReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicReferenceTypeField")]
            public virtual string PublicReferenceTypeField { get; set; }
        }

        public class InternalReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalReferenceTypeField")]
            public virtual string InternalReferenceTypeField { get; set; }
        }

        public class ProtectedReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedReferenceTypeField")]
            public virtual string ProtectedReferenceTypeField { get; set; }
        }

        public class PrivateReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateReferenceTypeField")]
            public virtual string PrivateReferenceTypeField { get; set; }
        }
    }
}
