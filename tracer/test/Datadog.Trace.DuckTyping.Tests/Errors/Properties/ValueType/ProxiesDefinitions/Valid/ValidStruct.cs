// <copyright file="ValidStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.ValueType.ProxiesDefinitions.Valid
{
#pragma warning disable 649
    internal struct ValidStruct
    {
        [DuckCopy]
        public struct PublicStaticGetValueTypeStruct
        {
            public int PublicStaticGetValueType;
        }

        [DuckCopy]
        public struct InternalStaticGetValueTypeStruct
        {
            public int InternalStaticGetValueType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetValueTypeStruct
        {
            public int ProtectedStaticGetValueType;
        }

        [DuckCopy]
        public struct PrivateStaticGetValueTypeStruct
        {
            public int PrivateStaticGetValueType;
        }

        [DuckCopy]
        public struct PublicStaticGetSetValueTypeStruct
        {
            public int PublicStaticGetSetValueType;
        }

        [DuckCopy]
        public struct InternalStaticGetSetValueTypeStruct
        {
            public int InternalStaticGetSetValueType;
        }

        [DuckCopy]
        public struct ProtectedStaticGetSetValueTypeStruct
        {
            public int ProtectedStaticGetSetValueType;
        }

        [DuckCopy]
        public struct PrivateStaticGetSetValueTypeStruct
        {
            public int PrivateStaticGetSetValueType;
        }

        [DuckCopy]
        public struct PublicGetValueTypeStruct
        {
            public int PublicGetValueType;
        }

        [DuckCopy]
        public struct InternalGetValueTypeStruct
        {
            public int InternalGetValueType;
        }

        [DuckCopy]
        public struct ProtectedGetValueTypeStruct
        {
            public int ProtectedGetValueType;
        }

        [DuckCopy]
        public struct PrivateGetValueTypeStruct
        {
            public int PrivateGetValueType;
        }

        [DuckCopy]
        public struct PublicGetSetValueTypeStruct
        {
            public int PublicGetSetValueType;
        }

        [DuckCopy]
        public struct InternalGetSetValueTypeStruct
        {
            public int InternalGetSetValueType;
        }

        [DuckCopy]
        public struct ProtectedGetSetValueTypeStruct
        {
            public int ProtectedGetSetValueType;
        }

        [DuckCopy]
        public struct PrivateGetSetValueTypeStruct
        {
            public int PrivateGetSetValueType;
        }
    }
}
