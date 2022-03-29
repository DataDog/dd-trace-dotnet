// <copyright file="WrongPropertyNameStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ValueType.ProxiesDefinitions.WrongPropertyName
{
#pragma warning disable 649
    internal struct WrongPropertyNameStruct
    {
        [DuckCopy]
        public struct PublicStaticGetValueTypeStruct
        {
            public int NotPublicStaticGetValueType;
        }

        [DuckCopy]
        public struct InternalStaticGetValueTypeStruct
        {
            public int NotInternalStaticGetValueType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetValueTypeStruct
        {
            public int NotProtectedStaticGetValueType;
        }

        [DuckCopy]
        public struct PrivateStaticGetValueTypeStruct
        {
            public int NotPrivateStaticGetValueType;
        }

        [DuckCopy]
        public struct PublicStaticGetSetValueTypeStruct
        {
            public int NotPublicStaticGetSetValueType;
        }

        [DuckCopy]
        public struct InternalStaticGetSetValueTypeStruct
        {
            public int NotInternalStaticGetSetValueType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetSetValueTypeStruct
        {
            public int NotProtectedStaticGetSetValueType;
        }

        [DuckCopy]
        public struct PrivateStaticGetSetValueTypeStruct
        {
            public int NotPrivateStaticGetSetValueType;
        }

        [DuckCopy]
        public struct PublicGetValueTypeStruct
        {
            public int NotPublicGetValueType;
        }

        [DuckCopy]
        public struct InternalGetValueTypeStruct
        {
            public int NotInternalGetValueType;
        }

        [DuckCopy]
        public struct ProtectedGetValueTypeStruct
        {
            public int NotProtectedGetValueType;
        }

        [DuckCopy]
        public struct PrivateGetValueTypeStruct
        {
            public int NotPrivateGetValueType;
        }

        [DuckCopy]
        public struct PublicGetSetValueTypeStruct
        {
            public int NotPublicGetSetValueType;
        }

        [DuckCopy]
        public struct InternalGetSetValueTypeStruct
        {
            public int NotInternalGetSetValueType;
        }

        [DuckCopy]
        public struct ProtectedGetSetValueTypeStruct
        {
            public int NotProtectedGetSetValueType;
        }

        [DuckCopy]
        public struct PrivateGetSetValueTypeStruct
        {
            public int NotPrivateGetSetValueType;
        }
    }
}
