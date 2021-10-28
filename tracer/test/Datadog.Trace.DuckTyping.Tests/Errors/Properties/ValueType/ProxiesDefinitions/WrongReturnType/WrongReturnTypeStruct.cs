// <copyright file="WrongReturnTypeStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ValueType.ProxiesDefinitions.WrongReturnType
{
#pragma warning disable 649
    internal struct WrongReturnTypeStruct
    {
        [DuckCopy]
        public struct PublicStaticGetValueTypeStruct
        {
            public char PublicStaticGetValueType;
        }

        [DuckCopy]
        public struct InternalStaticGetValueTypeStruct
        {
            public char InternalStaticGetValueType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetValueTypeStruct
        {
            public char ProtectedStaticGetValueType;
        }

        [DuckCopy]
        public struct PrivateStaticGetValueTypeStruct
        {
            public char PrivateStaticGetValueType;
        }

        [DuckCopy]
        public struct PublicStaticGetSetValueTypeStruct
        {
            public char PublicStaticGetSetValueType;
        }

        [DuckCopy]
        public struct InternalStaticGetSetValueTypeStruct
        {
            public char InternalStaticGetSetValueType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetSetValueTypeStruct
        {
            public char ProtectedStaticGetSetValueType;
        }

        [DuckCopy]
        public struct PrivateStaticGetSetValueTypeStruct
        {
            public char PrivateStaticGetSetValueType;
        }

        [DuckCopy]
        public struct PublicGetValueTypeStruct
        {
            public char PublicGetValueType;
        }

        [DuckCopy]
        public struct InternalGetValueTypeStruct
        {
            public char InternalGetValueType;
        }

        [DuckCopy]
        public struct ProtectedGetValueTypeStruct
        {
            public char ProtectedGetValueType;
        }

        [DuckCopy]
        public struct PrivateGetValueTypeStruct
        {
            public char PrivateGetValueType;
        }

        [DuckCopy]
        public struct PublicGetSetValueTypeStruct
        {
            public char PublicGetSetValueType;
        }

        [DuckCopy]
        public struct InternalGetSetValueTypeStruct
        {
            public char InternalGetSetValueType;
        }

        [DuckCopy]
        public struct ProtectedGetSetValueTypeStruct
        {
            public char ProtectedGetSetValueType;
        }

        [DuckCopy]
        public struct PrivateGetSetValueTypeStruct
        {
            public char PrivateGetSetValueType;
        }
    }
}
