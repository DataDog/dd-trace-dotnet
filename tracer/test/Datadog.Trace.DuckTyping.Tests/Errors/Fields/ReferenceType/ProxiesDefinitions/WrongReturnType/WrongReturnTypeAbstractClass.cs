// <copyright file="WrongReturnTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ReferenceType.ProxiesDefinitions.WrongReturnType
{
    public abstract class WrongReturnTypeAbstractClass
    {
        public abstract class PublicStaticReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicStaticReadonlyReferenceTypeField")]
            public abstract int PublicStaticReadonlyReferenceTypeField { get; }
        }

        public abstract class InternalStaticReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalStaticReadonlyReferenceTypeField")]
            public abstract int InternalStaticReadonlyReferenceTypeField { get; }
        }

        public abstract class ProtectedStaticReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedStaticReadonlyReferenceTypeField")]
            public abstract int ProtectedStaticReadonlyReferenceTypeField { get; }
        }

        public abstract class PrivateStaticReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateStaticReadonlyReferenceTypeField")]
            public abstract int PrivateStaticReadonlyReferenceTypeField { get; }
        }

        // *

        public abstract class PublicStaticReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicStaticReferenceTypeField")]
            public abstract int PublicStaticReferenceTypeField { get; set; }
        }

        public abstract class InternalStaticReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalStaticReferenceTypeField")]
            public abstract int InternalStaticReferenceTypeField { get; set; }
        }

        public abstract class ProtectedStaticReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedStaticReferenceTypeField")]
            public abstract int ProtectedStaticReferenceTypeField { get; set; }
        }

        public abstract class PrivateStaticReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateStaticReferenceTypeField")]
            public abstract int PrivateStaticReferenceTypeField { get; set; }
        }

        // *

        public abstract class PublicReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicReadonlyReferenceTypeField")]
            public abstract int PublicReadonlyReferenceTypeField { get; }
        }

        public abstract class InternalReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalReadonlyReferenceTypeField")]
            public abstract int InternalReadonlyReferenceTypeField { get; }
        }

        public abstract class ProtectedReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedReadonlyReferenceTypeField")]
            public abstract int ProtectedReadonlyReferenceTypeField { get; }
        }

        public abstract class PrivateReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateReadonlyReferenceTypeField")]
            public abstract int PrivateReadonlyReferenceTypeField { get; }
        }

        // *

        public abstract class PublicReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_publicReferenceTypeField")]
            public abstract int PublicReferenceTypeField { get; set; }
        }

        public abstract class InternalReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_internalReferenceTypeField")]
            public abstract int InternalReferenceTypeField { get; set; }
        }

        public abstract class ProtectedReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_protectedReferenceTypeField")]
            public abstract int ProtectedReferenceTypeField { get; set; }
        }

        public abstract class PrivateReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "_privateReferenceTypeField")]
            public abstract int PrivateReferenceTypeField { get; set; }
        }
    }
}
