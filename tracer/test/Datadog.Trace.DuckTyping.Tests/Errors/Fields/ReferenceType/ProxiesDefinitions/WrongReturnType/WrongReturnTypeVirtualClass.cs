// <copyright file="WrongReturnTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ReferenceType.ProxiesDefinitions.WrongReturnType
{
    public class WrongReturnTypeVirtualClass
    {
        public class PublicStaticReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticReadonlyReferenceTypeField")]
            public virtual int PublicStaticReadonlyReferenceTypeField { get; }
        }

        public class InternalStaticReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalStaticReadonlyReferenceTypeField")]
            public virtual int InternalStaticReadonlyReferenceTypeField { get; }
        }

        public class ProtectedStaticReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedStaticReadonlyReferenceTypeField")]
            public virtual int ProtectedStaticReadonlyReferenceTypeField { get; }
        }

        public class PrivateStaticReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticReadonlyReferenceTypeField")]
            public virtual int PrivateStaticReadonlyReferenceTypeField { get; }
        }

        // *

        public class PublicStaticReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticReferenceTypeField")]
            public virtual int PublicStaticReferenceTypeField { get; set; }
        }

        public class InternalStaticReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalStaticReferenceTypeField")]
            public virtual int InternalStaticReferenceTypeField { get; set; }
        }

        public class ProtectedStaticReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedStaticReferenceTypeField")]
            public virtual int ProtectedStaticReferenceTypeField { get; set; }
        }

        public class PrivateStaticReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticReferenceTypeField")]
            public virtual int PrivateStaticReferenceTypeField { get; set; }
        }

        // *

        public class PublicReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicReadonlyReferenceTypeField")]
            public virtual int PublicReadonlyReferenceTypeField { get; }
        }

        public class InternalReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalReadonlyReferenceTypeField")]
            public virtual int InternalReadonlyReferenceTypeField { get; }
        }

        public class ProtectedReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedReadonlyReferenceTypeField")]
            public virtual int ProtectedReadonlyReferenceTypeField { get; }
        }

        public class PrivateReadonlyReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateReadonlyReferenceTypeField")]
            public virtual int PrivateReadonlyReferenceTypeField { get; }
        }

        // *

        public class PublicReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicReferenceTypeField")]
            public virtual int PublicReferenceTypeField { get; set; }
        }

        public class InternalReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalReferenceTypeField")]
            public virtual int InternalReferenceTypeField { get; set; }
        }

        public class ProtectedReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedReferenceTypeField")]
            public virtual int ProtectedReferenceTypeField { get; set; }
        }

        public class PrivateReferenceTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateReferenceTypeField")]
            public virtual int PrivateReferenceTypeField { get; set; }
        }
    }
}
