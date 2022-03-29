// <copyright file="WrongPropertyNameAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ReferenceType.ProxiesDefinitions.WrongPropertyName
{
    public abstract class WrongPropertyNameAbstractClass
    {
        public abstract class PublicStaticGetReferenceTypeAbstractClass
        {
            public abstract string NotPublicStaticGetReferenceType { get; }
        }

        public abstract class InternalStaticGetReferenceTypeAbstractClass
        {
            public abstract string NotInternalStaticGetReferenceType { get; }
        }

        public abstract class ProtectedStaticGetReferenceTypeAbstractClass
        {
            public abstract string NotProtectedStaticGetReferenceType { get; }
        }

        public abstract class PrivateStaticGetReferenceTypeAbstractClass
        {
            public abstract string NotPrivateStaticGetReferenceType { get; }
        }

        // *
        public abstract class PublicStaticGetSetReferenceTypeAbstractClass
        {
            public abstract string NotPublicStaticGetSetReferenceType { get; set; }
        }

        public abstract class InternalStaticGetSetReferenceTypeAbstractClass
        {
            public abstract string NotInternalStaticGetSetReferenceType { get; set; }
        }

        public abstract class ProtectedStaticGetSetReferenceTypeAbstractClass
        {
            public abstract string NotProtectedStaticGetSetReferenceType { get; set; }
        }

        public abstract class PrivateStaticGetSetReferenceTypeAbstractClass
        {
            public abstract string NotPrivateStaticGetSetReferenceType { get; set; }
        }

        // *
        public abstract class PublicGetReferenceTypeAbstractClass
        {
            public abstract string NotPublicGetReferenceType { get; }
        }

        public abstract class InternalGetReferenceTypeAbstractClass
        {
            public abstract string NotInternalGetReferenceType { get; }
        }

        public abstract class ProtectedGetReferenceTypeAbstractClass
        {
            public abstract string NotProtectedGetReferenceType { get; }
        }

        public abstract class PrivateGetReferenceTypeAbstractClass
        {
            public abstract string NotPrivateGetReferenceType { get; }
        }

        // *
        public abstract class PublicGetSetReferenceTypeAbstractClass
        {
            public abstract string NotPublicGetSetReferenceType { get; set; }
        }

        public abstract class InternalGetSetReferenceTypeAbstractClass
        {
            public abstract string NotInternalGetSetReferenceType { get; set; }
        }

        public abstract class ProtectedGetSetReferenceTypeAbstractClass
        {
            public abstract string NotProtectedGetSetReferenceType { get; set; }
        }

        public abstract class PrivateGetSetReferenceTypeAbstractClass
        {
            public abstract string NotPrivateGetSetReferenceType { get; set; }
        }
    }
}
