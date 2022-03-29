// <copyright file="WrongPropertyNameStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ReferenceType.ProxiesDefinitions.WrongPropertyName
{
#pragma warning disable 649

    internal struct WrongPropertyNameStruct
    {
        [DuckCopy]
        public struct PublicStaticGetReferenceTypeStruct
        {
            public string NotPublicStaticGetReferenceType;
        }

        [DuckCopy]
        public struct InternalStaticGetReferenceTypeStruct
        {
            public string NotInternalStaticGetReferenceType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetReferenceTypeStruct
        {
            public string NotProtectedStaticGetReferenceType;
        }

        [DuckCopy]
        public struct PrivateStaticGetReferenceTypeStruct
        {
            public string NotPrivateStaticGetReferenceType;
        }

        [DuckCopy]
        public struct PublicStaticGetSetReferenceTypeStruct
        {
            public string NotPublicStaticGetSetReferenceType;
        }

        [DuckCopy]
        public struct InternalStaticGetSetReferenceTypeStruct
        {
            public string NotInternalStaticGetSetReferenceType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetSetReferenceTypeStruct
        {
            public string NotProtectedStaticGetSetReferenceType;
        }

        [DuckCopy]
        public struct PrivateStaticGetSetReferenceTypeStruct
        {
            public string NotPrivateStaticGetSetReferenceType;
        }

        [DuckCopy]
        public struct PublicGetReferenceTypeStruct
        {
            public string NotPublicGetReferenceType;
        }

        [DuckCopy]
        public struct InternalGetReferenceTypeStruct
        {
            public string NotInternalGetReferenceType;
        }

        [DuckCopy]
        public struct ProtectedGetReferenceTypeStruct
        {
            public string NotProtectedGetReferenceType;
        }

        [DuckCopy]
        public struct PrivateGetReferenceTypeStruct
        {
            public string NotPrivateGetReferenceType;
        }

        [DuckCopy]
        public struct PublicGetSetReferenceTypeStruct
        {
            public string NotPublicGetSetReferenceType;
        }

        [DuckCopy]
        public struct InternalGetSetReferenceTypeStruct
        {
            public string NotInternalGetSetReferenceType;
        }

        [DuckCopy]
        public struct ProtectedGetSetReferenceTypeStruct
        {
            public string NotProtectedGetSetReferenceType;
        }

        [DuckCopy]
        public struct PrivateGetSetReferenceTypeStruct
        {
            public string NotPrivateGetSetReferenceType;
        }
    }
}
