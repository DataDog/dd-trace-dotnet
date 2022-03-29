// <copyright file="WrongReturnTypeStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ReferenceType.ProxiesDefinitions.WrongReturnType
{
#pragma warning disable 649

    internal struct WrongReturnTypeStruct
    {
        [DuckCopy]
        public struct ReadonlyFieldIgnoredStruct
        {
            public readonly string[] ReadonlyFieldIgnored;
        }

        [DuckCopy]
        public struct PublicStaticGetReferenceTypeStruct
        {
            public string[] PublicStaticGetReferenceType;
        }

        [DuckCopy]
        public struct InternalStaticGetReferenceTypeStruct
        {
            public string[] InternalStaticGetReferenceType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetReferenceTypeStruct
        {
            public string[] ProtectedStaticGetReferenceType;
        }

        [DuckCopy]
        public struct PrivateStaticGetReferenceTypeStruct
        {
            public string[] PrivateStaticGetReferenceType;
        }

        [DuckCopy]
        public struct PublicStaticGetSetReferenceTypeStruct
        {
            public string[] PublicStaticGetSetReferenceType;
        }

        [DuckCopy]
        public struct InternalStaticGetSetReferenceTypeStruct
        {
            public string[] InternalStaticGetSetReferenceType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetSetReferenceTypeStruct
        {
            public string[] ProtectedStaticGetSetReferenceType;
        }

        [DuckCopy]
        public struct PrivateStaticGetSetReferenceTypeStruct
        {
            public string[] PrivateStaticGetSetReferenceType;
        }

        [DuckCopy]
        public struct PublicGetReferenceTypeStruct
        {
            public string[] PublicGetReferenceType;
        }

        [DuckCopy]
        public struct InternalGetReferenceTypeStruct
        {
            public string[] InternalGetReferenceType;
        }

        [DuckCopy]
        public struct ProtectedGetReferenceTypeStruct
        {
            public string[] ProtectedGetReferenceType;
        }

        [DuckCopy]
        public struct PrivateGetReferenceTypeStruct
        {
            public string[] PrivateGetReferenceType;
        }

        [DuckCopy]
        public struct PublicGetSetReferenceTypeStruct
        {
            public string[] PublicGetSetReferenceType;
        }

        [DuckCopy]
        public struct InternalGetSetReferenceTypeStruct
        {
            public string[] InternalGetSetReferenceType;
        }

        [DuckCopy]
        public struct ProtectedGetSetReferenceTypeStruct
        {
            public string[] ProtectedGetSetReferenceType;
        }

        [DuckCopy]
        public struct PrivateGetSetReferenceTypeStruct
        {
            public string[] PrivateGetSetReferenceType;
        }
    }
}
