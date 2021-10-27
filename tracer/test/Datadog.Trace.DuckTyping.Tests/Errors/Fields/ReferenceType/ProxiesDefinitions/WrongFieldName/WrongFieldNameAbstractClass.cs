// <copyright file="WrongFieldNameAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ReferenceType.ProxiesDefinitions.WrongFieldName
{
    public abstract class WrongFieldNameAbstractClass
    {
        public abstract class PublicStaticReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "publicStaticReadonlyReferenceTypeField")]
            public abstract string PublicStaticReadonlyReferenceTypeField { get; }
        }

        public abstract class InternalStaticReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "internalStaticReadonlyReferenceTypeField")]
            public abstract string InternalStaticReadonlyReferenceTypeField { get; }
        }

        public abstract class ProtectedStaticReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "protectedStaticReadonlyReferenceTypeField")]
            public abstract string ProtectedStaticReadonlyReferenceTypeField { get; }
        }

        public abstract class PrivateStaticReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "privateStaticReadonlyReferenceTypeField")]
            public abstract string PrivateStaticReadonlyReferenceTypeField { get; }
        }

        // *

        public abstract class PublicStaticReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "publicStaticReferenceTypeField")]
            public abstract string PublicStaticReferenceTypeField { get; set; }
        }

        public abstract class InternalStaticReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "internalStaticReferenceTypeField")]
            public abstract string InternalStaticReferenceTypeField { get; set; }
        }

        public abstract class ProtectedStaticReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "protectedStaticReferenceTypeField")]
            public abstract string ProtectedStaticReferenceTypeField { get; set; }
        }

        public abstract class PrivateStaticReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "privateStaticReferenceTypeField")]
            public abstract string PrivateStaticReferenceTypeField { get; set; }
        }

        // *

        public abstract class PublicReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "publicReadonlyReferenceTypeField")]
            public abstract string PublicReadonlyReferenceTypeField { get; }
        }

        public abstract class InternalReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "internalReadonlyReferenceTypeField")]
            public abstract string InternalReadonlyReferenceTypeField { get; }
        }

        public abstract class ProtectedReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "protectedReadonlyReferenceTypeField")]
            public abstract string ProtectedReadonlyReferenceTypeField { get; }
        }

        public abstract class PrivateReadonlyReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "privateReadonlyReferenceTypeField")]
            public abstract string PrivateReadonlyReferenceTypeField { get; }
        }

        // *

        public abstract class PublicReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "publicReferenceTypeField")]
            public abstract string PublicReferenceTypeField { get; set; }
        }

        public abstract class InternalReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "internalReferenceTypeField")]
            public abstract string InternalReferenceTypeField { get; set; }
        }

        public abstract class ProtectedReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "protectedReferenceTypeField")]
            public abstract string ProtectedReferenceTypeField { get; set; }
        }

        public abstract class PrivateReferenceTypeFieldAbstractClass
        {
            [DuckField(Name = "privateReferenceTypeField")]
            public abstract string PrivateReferenceTypeField { get; set; }
        }
    }
}
