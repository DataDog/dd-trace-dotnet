// <copyright file="WrongReturnTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ReferenceType.ProxiesDefinitions.WrongReturnType
{
    public abstract class WrongReturnTypeAbstractClass
    {
        public abstract class PublicStaticGetReferenceTypeAbstractClass
        {
            public abstract string[] PublicStaticGetReferenceType { get; }
        }

        public abstract class InternalStaticGetReferenceTypeAbstractClass
        {
            public abstract string[] InternalStaticGetReferenceType { get; }
        }

        public abstract class ProtectedStaticGetReferenceTypeAbstractClass
        {
            public abstract string[] ProtectedStaticGetReferenceType { get; }
        }

        public abstract class PrivateStaticGetReferenceTypeAbstractClass
        {
            public abstract string[] PrivateStaticGetReferenceType { get; }
        }

        // *
        public abstract class PublicStaticGetSetReferenceTypeAbstractClass
        {
            public abstract string[] PublicStaticGetSetReferenceType { get; set; }
        }

        public abstract class InternalStaticGetSetReferenceTypeAbstractClass
        {
            public abstract string[] InternalStaticGetSetReferenceType { get; set; }
        }

        public abstract class ProtectedStaticGetSetReferenceTypeAbstractClass
        {
            public abstract string[] ProtectedStaticGetSetReferenceType { get; set; }
        }

        public abstract class PrivateStaticGetSetReferenceTypeAbstractClass
        {
            public abstract string[] PrivateStaticGetSetReferenceType { get; set; }
        }

        // *
        public abstract class PublicGetReferenceTypeAbstractClass
        {
            public abstract string[] PublicGetReferenceType { get; }
        }

        public abstract class InternalGetReferenceTypeAbstractClass
        {
            public abstract string[] InternalGetReferenceType { get; }
        }

        public abstract class ProtectedGetReferenceTypeAbstractClass
        {
            public abstract string[] ProtectedGetReferenceType { get; }
        }

        public abstract class PrivateGetReferenceTypeAbstractClass
        {
            public abstract string[] PrivateGetReferenceType { get; }
        }

        // *
        public abstract class PublicGetSetReferenceTypeAbstractClass
        {
            public abstract string[] PublicGetSetReferenceType { get; set; }
        }

        public abstract class InternalGetSetReferenceTypeAbstractClass
        {
            public abstract string[] InternalGetSetReferenceType { get; set; }
        }

        public abstract class ProtectedGetSetReferenceTypeAbstractClass
        {
            public abstract string[] ProtectedGetSetReferenceType { get; set; }
        }

        public abstract class PrivateGetSetReferenceTypeAbstractClass
        {
            public abstract string[] PrivateGetSetReferenceType { get; set; }
        }

        // *
        public abstract class IndexAbstractClass
        {
            public abstract string[] this[string index] { get; set; }
        }
    }
}
