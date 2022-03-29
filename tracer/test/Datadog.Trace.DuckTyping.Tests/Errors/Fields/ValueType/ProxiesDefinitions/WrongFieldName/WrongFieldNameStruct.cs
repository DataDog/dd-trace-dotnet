// <copyright file="WrongFieldNameStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ValueType.ProxiesDefinitions.WrongFieldName
{
    public struct WrongFieldNameStruct
    {
        [DuckCopy]
        public struct PublicStaticReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "publicStaticReadonlyValueTypeField")]
            public int PublicStaticReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct InternalStaticReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "internalStaticReadonlyValueTypeField")]
            public int InternalStaticReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "protectedStaticReadonlyValueTypeField")]
            public int ProtectedStaticReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "privateStaticReadonlyValueTypeField")]
            public int PrivateStaticReadonlyValueTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicStaticValueTypeFieldStruct
        {
            [DuckField(Name = "publicStaticValueTypeField")]
            public int PublicStaticValueTypeField;
        }

        [DuckCopy]
        public struct InternalStaticValueTypeFieldStruct
        {
            [DuckField(Name = "internalStaticValueTypeField")]
            public int InternalStaticValueTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticValueTypeFieldStruct
        {
            [DuckField(Name = "protectedStaticValueTypeField")]
            public int ProtectedStaticValueTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticValueTypeFieldStruct
        {
            [DuckField(Name = "privateStaticValueTypeField")]
            public int PrivateStaticValueTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "publicReadonlyValueTypeField")]
            public int PublicReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct InternalReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "internalReadonlyValueTypeField")]
            public int InternalReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct ProtectedReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "protectedReadonlyValueTypeField")]
            public int ProtectedReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct PrivateReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "privateReadonlyValueTypeField")]
            public int PrivateReadonlyValueTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicValueTypeFieldStruct
        {
            [DuckField(Name = "publicValueTypeField")]
            public int PublicValueTypeField;
        }

        [DuckCopy]
        public struct InternalValueTypeFieldStruct
        {
            [DuckField(Name = "internalValueTypeField")]
            public int InternalValueTypeField;
        }

        [DuckCopy]
        public struct ProtectedValueTypeFieldStruct
        {
            [DuckField(Name = "protectedValueTypeField")]
            public int ProtectedValueTypeField;
        }

        [DuckCopy]
        public struct PrivateValueTypeFieldStruct
        {
            [DuckField(Name = "privateValueTypeField")]
            public int PrivateValueTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicStaticNullableIntFieldStruct
        {
            [DuckField(Name = "publicStaticNullableIntField")]
            public int? PublicStaticNullableIntField;
        }

        [DuckCopy]
        public struct PrivateStaticNullableIntFieldStruct
        {
            [DuckField(Name = "privateStaticNullableIntField")]
            public int? PrivateStaticNullableIntField;
        }

        [DuckCopy]
        public struct PublicNullableIntFieldStruct
        {
            [DuckField(Name = "publicNullableIntField")]
            public int? PublicNullableIntField;
        }

        [DuckCopy]
        public struct PrivateNullableIntFieldStruct
        {
            [DuckField(Name = "privateNullableIntField")]
            public int? PrivateNullableIntField;
        }
    }
}
